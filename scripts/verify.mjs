// 検査を枠組み(node:test)で走らせる入口。常設の検査と実機に触る検査の両方がここを通る。
// 出来上がりを作る検査を1回目に、残りを2回目に走らせる。

import { spawnSync } from 'node:child_process';
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { cpus, tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { run } from 'node:test';
import { spec } from 'node:test/reporters';

import {
    NO_ARTIFACT, assertGroupedChecks, bundlesOf, derivedLimitOf, killDescendants,
    manifestOf, pathsTouch, selectGroups, splitIntoForms, verdictOf, weightOf,
} from './checks.mjs';

const root = resolve(import.meta.dirname, '..');
const helpers = pathToFileURL(join(root, 'scripts', 'checks.mjs')).href;

// 同時に走らせる束の数。
const atOnce = Math.max(1, Math.floor(cpus().length / 4));

/** 変えたものの道。追跡下の変更と、追跡外のファイルの両方を見る。 */
function changedPaths() {
    const said = [];
    for (const args of [['diff', '--name-only', 'HEAD'],
        ['ls-files', '--others', '--exclude-standard']]) {
        const ran = spawnSync('git', args, { cwd: root, encoding: 'utf8' });
        said.push(...(ran.stdout || '').split(/\r?\n/));
    }

    return said.filter((path) => path);
}

/** 束ごとの検査を1つのファイルへ書き出す。枠組みはファイルを単位に並列化する。 */
function writeBundleFiles(work, bundles, produced, afterFailure) {
    const files = [];
    let at = 0;
    // 長く掛かる束から並べる。枠組みは渡した順に、空きが出た数だけ起こす。
    const ordered = [...bundles.values()].sort(
        (left, right) => weightOf(right) - weightOf(left));
    for (const checks of ordered) {
        const path = join(work, `bundle-${at++}.test.mjs`);
        writeFileSync(path, [
            "import test from 'node:test';",
            "import { readFileSync } from 'node:fs';",
            `import { isReady, judgeResult, runCapped } from ${JSON.stringify(helpers)};`,
            '',
            `const checks = ${JSON.stringify(checks, null, 4)};`,
            `const produced = ${JSON.stringify(produced)};`,
            `const afterFailure = ${JSON.stringify(afterFailure || null)};`,
            '',
            'const started = new Map();',
            '',
            'function fellIn(results, form) {',
            '    let said;',
            '    try {',
            '        said = readFileSync(results, \'utf8\');',
            '    } catch {',
            '        return { code: 1, said: \'形ごとの結末が1件も書かれていない。\' };',
            '    }',
            '',
            '    for (const line of said.split(/\\r?\\n/)) {',
            '        const [named, code, fell] = line.split(\'\\t\');',
            '        if (named === form) return { code: Number(code), said: fell };',
            '    }',
            '',
            '    return { code: 1, said: \'この形の結末が書かれていない。\' };',
            '}',
            '',
            'for (const check of checks) {',
            '    test(check.name, async (t) => {',
            '        if (!isReady(check.needs, produced)) {',
            "            t.skip('要る出来上がりが揃っていないので始めない。');",
            '            return;',
            '        }',
            '',
            '        if (check.group) {',
            '            if (!started.has(check.group)) {',
            '                started.set(check.group, runCapped(t.signal, check.run));',
            '            }',
            '',
            '            const whole = await started.get(check.group);',
            '            if (whole.capped) {',
            '                throw new Error(`組ごと打ち切られた`',
            '                    + `(上限 ${check.run.limitSeconds} 秒)\\n${whole.said}`);',
            '            }',
            '',
            '            const fell = fellIn(check.results, check.form);',
            '            if (fell.code !== 0) {',
            '                throw new Error((fell.said || `終了コード ${fell.code}`)',
            '                    + (whole.code === 0 ? \'\' : `\\n${whole.said}`));',
            '            }',
            '',
            '            return;',
            '        }',
            '',
            '        const ran = await runCapped(t.signal, check);',
            '        const code = judgeResult(ran, check.limitSeconds);',
            '        if (code !== 0) {',
            '            if (afterFailure) {',
            '                await runCapped(new AbortController().signal, afterFailure);',
            '            }',
            '',
            '            throw new Error(`終了コード ${code}'
                + '(${ran.seconds.toFixed(1)}秒 / 上限 ${check.limitSeconds}秒)'
                + '\\n${ran.said}`);',
            '        }',
            '',
            '        if (check.produces) produced.push(check.produces);',
            '    });',
            '}',
            '',
        ].join('\n'), 'utf8');
        files.push(path);
    }

    return files;
}

/** 1回ぶんの実行。枠組みが束どうしを並列に走らせて報告を書き、こちらは結末を数える。 */
async function runOnce(work, checks, produced, signal, afterFailure) {
    const files = writeBundleFiles(work, bundlesOf(splitIntoForms(checks, work, atOnce)), produced,
        afterFailure);
    const failed = [];
    const skipped = [];

    const stream = run({ files, concurrency: atOnce, signal, isolation: 'process' });
    stream.on('test:fail', (data) => {
        if (!failed.includes(data.name)) failed.push(data.name);
    });
    stream.on('test:pass', (data) => {
        if (data.skip && !skipped.includes(data.name)) skipped.push(data.name);
    });

    for await (const line of stream.compose(spec)) process.stdout.write(line);

    return { failed, skipped };
}

/** 走らせる検査を選ぶ。 */
function selectChecks(manifest, all, paths) {
    const byName = new Map(manifest.checks.map((check) => [check.name, check]));
    let wanted;
    let scope;

    if (manifest.groups) {
        let groups;
        if (all) {
            groups = Object.keys(manifest.groups);
        } else {
            if (paths.length === 0) return { wanted: [], scope: '', nothingChanged: true };

            groups = selectGroups({
                paths,
                groupPaths: manifest.groupPaths,
                ungrouped: manifest.ungrouped,
                allGroups: Object.keys(manifest.groups),
            });
            if (groups.length === 0) return { wanted: [], scope: '', nothingChanged: true };

            console.log('変えたものから選んだ群: ' + groups.join('・'));
        }

        const chosen = new Set(groups.flatMap((group) => manifest.groups[group]));

        // 作る側が別の作る側を要ることがあるので、増えなくなるまで繰り返す。
        for (let added = true; added;) {
            added = false;
            for (const maker of manifest.makers) {
                if (chosen.has(maker.name)) continue;
                if (![...chosen].some((name) => byName.get(name).needs === maker.produces)) {
                    continue;
                }

                chosen.add(maker.name);
                added = true;
            }
        }

        wanted = manifest.checks.filter((check) => chosen.has(check.name));
        scope = groups.join('・');
    } else {
        wanted = [...manifest.checks];
        scope = manifest.scope;
    }

    for (const [name, unrelated] of Object.entries(manifest.conditional || {})) {
        const forIt = all || paths.length === 0
            || paths.some((path) => !pathsTouch([path], unrelated));
        if (forIt) continue;

        console.log(`${name} は走らせない(見ている綴りを組み立てるものを変えていない)。`);
        wanted = wanted.filter((check) => check.name !== name);
    }

    return { wanted, scope };
}

async function main(argv) {
    const all = argv.includes('--all');
    const at = argv.indexOf('--set');
    if (at < 0 || !argv[at + 1]) {
        console.error('使い方: node scripts/verify.mjs --set standing|live [--all]');

        return 2;
    }

    process.chdir(root);

    // 束をまたいで持ち越す出来上がりの置き場。一覧が検査へ埋め込むので、読むより先に決める。
    const work = mkdtempSync(join(tmpdir(), 'pmx-editor-mcp-verify-'));
    process.env.PMX_EDITOR_MCP_WORK = work;

    try {
        return await verify(argv[at + 1], all, work);
    } finally {
        rmSync(work, { recursive: true, force: true });
    }
}

/** 一覧を読み、検査を選び、2回に分けて走らせて、実行を終わらせる終了コードを返す。 */
async function verify(set, all, work) {
    const manifest = manifestOf(set);
    Object.assign(process.env, manifest.environment || {});
    const names = manifest.checks.map((check) => check.name);
    if (manifest.groups) assertGroupedChecks(manifest.groups, names);

    const paths = all ? [] : changedPaths();
    const { wanted, scope, nothingChanged } = selectChecks(manifest, all, paths);
    if (nothingChanged) {
        console.log('変えたものが無いので、走らせる検査も無い。');

        return 0;
    }

    const runs = [wanted.filter((check) => check.stage === 1),
        wanted.filter((check) => check.stage !== 1)];
    const limit = derivedLimitOf(runs.map((checks) => splitIntoForms(checks, work, atOnce)));

    const began = process.hrtime.bigint();
    const stopper = new AbortController();
    // 導いた値に達した実行は、その時点で止める。
    let overran = false;
    const timer = setTimeout(() => {
        overran = true;
        // 枠組みが検査のファイルのプロセスを終わらせると、その先で走っている検査の本体は親を
        // 失って辿れなくなる。止める前に木を辿る。
        killDescendants(process.pid);
        stopper.abort();
    }, limit * 1000);

    const failed = [];
    const skipped = [];
    let ran = 0;
    try {
        let produced = [NO_ARTIFACT];
        for (const checks of runs) {
            if (checks.length === 0) continue;

            if (failed.length > 0 || skipped.length > 0 || overran) {
                skipped.push(...checks.map((check) => check.name));
                continue;
            }

            const said = await runOnce(work, checks, produced, stopper.signal,
                manifest.afterFailure);
            ran += splitIntoForms(checks, work, atOnce).length - said.skipped.length;
            failed.push(...said.failed);
            skipped.push(...said.skipped);
            produced = [...produced,
                ...checks.map((check) => check.produces).filter((made) => made)];
        }
    } finally {
        clearTimeout(timer);
    }

    const elapsed = Number(process.hrtime.bigint() - began) / 1e9;
    const took = `${elapsed.toFixed(1)}秒 / 上限 ${limit}秒`;
    const over = overran || elapsed > limit;

    console.log('');
    if (over) console.log(`上限を超えた: ${took}`);
    if (skipped.length > 0) console.log('走らせていない: ' + skipped.join('・'));
    if (failed.length > 0) console.log('不合格: ' + failed.join('・'));

    const verdict = verdictOf({ failed, skipped, over });
    if (verdict !== 0) return verdict;

    console.log(`${scope} の ${ran} 件をすべて合格(${took}`
        + `・全部で ${splitIntoForms(manifest.checks, work, atOnce).length} 件)`);

    return 0;
}

process.exitCode = await main(process.argv.slice(2));
