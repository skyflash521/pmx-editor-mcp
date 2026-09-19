import { spawn, spawnSync } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { closeSync, openSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

/** 出来上がりを要さない検査の印。 */
export const NO_ARTIFACT = 'なし';

/** 上限に達した検査を打ち切ったことを表す終了コード。 */
export const CAPPED_CODE = 124;

/**
 * 検査を1件、別のプロセスで走らせる。上限に達したら相手を木ごと終わらせ、打ち切ったことを表す
 * 終了コードを返す。**上限を数えるのはここで、枠組みのタイムアウトではない**——枠組みは走っている
 * 相手を止めないうえ、超過の時点で次の検査を始めるので、後始末が次の検査と重なる。
 */
export function runCapped(signal, { file, args, limitSeconds }) {
    const began = process.hrtime.bigint();

    const told = join(tmpdir(), `pmx-editor-mcp-said-${randomUUID()}.log`);
    const sink = openSync(told, 'w');

    return new Promise((resolve) => {
        const child = spawn(file, args, { stdio: ['ignore', sink, sink] });
        let capped = false;

        const onAbort = () => {
            capped = true;
            spawn('taskkill', ['/T', '/F', '/PID', String(child.pid)], { stdio: 'ignore' });
        };

        const cut = limitSeconds > 0 ? setTimeout(onAbort, limitSeconds * 1000) : null;
        signal.addEventListener('abort', onAbort, { once: true });

        let settled = false;
        const done = (code, failure) => {
            if (settled) return;

            settled = true;
            if (cut) clearTimeout(cut);
            signal.removeEventListener('abort', onAbort);
            closeSync(sink);
            const said = code === 0 ? '' : readFileSync(told, 'utf8');
            rmSync(told, { force: true });
            const seconds = Number(process.hrtime.bigint() - began) / 1e9;
            resolve({ code, seconds, capped, said: (failure || '') + said });
        };

        child.on('error', (failure) => done(1, String(failure) + '\n'));
        child.on('close', (code, signal) => {
            if (capped) return done(CAPPED_CODE);
            done(code === null ? (signal ? CAPPED_CODE : 1) : code);
        });
    });
}

/** その相手の子孫を、木を辿って終わらせる。相手自身は終わらせない。 */
export function killDescendants(pid) {
    spawnSync('pwsh', ['-NoProfile', '-NonInteractive', '-File',
        'scripts/kill-descendants.ps1', '-Root', String(pid)], { stdio: 'ignore' });
}

/** その検査が要る出来上がりが揃っているか。 */
export function isReady(needs, produced) {
    return produced.includes(needs);
}

/** 走り終えた検査の終了コード。上限に達した検査は走り切っても通さない。 */
export function judgeResult(ran, limitSeconds) {
    if (ran.skipped) return 0;
    if (ran.code !== 0) return ran.code;
    if (limitSeconds > 0 && ran.seconds > limitSeconds) return CAPPED_CODE;

    return 0;
}

/** 実行を終わらせる終了コード。落ちた検査・走らせていない検査・上限超過のどれかがあれば1。 */
export function verdictOf({ failed, skipped, over }) {
    return (failed.length > 0 || skipped.length > 0 || over) ? 1 : 0;
}

/** 検査を束へ分ける。束を指していない検査は自分だけの束に入る。 */
export function bundlesOf(checks) {
    const bundles = new Map();
    for (const check of checks) {
        const name = check.bundle || check.name;
        if (!bundles.has(name)) bundles.set(name, []);
        bundles.get(name).push(check);
    }

    return bundles;
}

/** 形を持つ検査を、形1つにつき1件へ割る。形は組へ分け、組ごとに1回だけ相手を起こす。 */
export function splitIntoForms(checks, work, atOnce) {
    return checks.flatMap((check) => {
        if (!check.forms || check.forms.length === 0) return [check];

        const alone = check.formsInOrder || check.bundle !== check.name;
        const groups = [];
        check.forms.forEach((form, at) => {
            const which = alone ? 0 : at % atOnce;
            if (!groups[which]) groups[which] = [];
            groups[which].push(form);
        });

        return groups.filter((forms) => forms).flatMap((forms, at) => {
            const bundle = check.bundle === check.name
                ? `${check.name} — ${at}` : check.bundle;
            const group = `${check.name} — ${at}`;
            const results = work ? join(work, `forms-${randomUUID()}.tsv`) : '';
            const run = {
                file: check.file,
                args: [...check.args, check.resultsArgument, results,
                    check.formArgument, forms.join(',')],
                limitSeconds: check.limitSeconds,
            };

            return forms.map((form) => ({
                ...check,
                limitSeconds: check.limitSeconds / forms.length,
                share: check.limitSeconds / check.forms.length,
                name: `${check.name} — ${form}`,
                bundle,
                group,
                form,
                results,
                run,
            }));
        });
    });
}

/** 実行の全体を照らす値。 */
export function derivedLimitOf(runs) {
    let total = 0;
    for (const checks of runs) {
        const sums = [...bundlesOf(checks).values()].map(
            (bundle) => bundle.reduce((sum, check) => sum + check.limitSeconds, 0));
        total += sums.length === 0 ? 0 : Math.max(...sums);
    }

    return Math.round(total * 1000) / 1000;
}

/** 束を並べる順を決める見積り。形へ割った検査は、その組が受け持つ形の数だけを見込む。 */
export function weightOf(checks) {
    return checks.reduce((sum, check) => sum + (check.share ?? check.limitSeconds), 0);
}

/** 変えたものの道のどれかが、渡した形のどれかに当たるか。 */
export function pathsTouch(paths, patterns) {
    return paths.some((path) => patterns.some((pattern) => likeMatches(path, pattern)));
}

/** PowerShell の -like と同じ当たり方。`*` は区切りを越えて何文字にも当たり、`?` は1文字。 */
export function likeMatches(value, pattern) {
    const escaped = pattern.replace(/[.+^${}()|[\]\\]/g, '\\$&')
        .replace(/\*/g, '.*')
        .replace(/\?/g, '.');

    return new RegExp('^' + escaped + '$', 's').test(value);
}

/** 変えたものの道から、走らせる群の名前を返す。当たらない道が1つでもあれば全件の群。 */
export function selectGroups({ paths, groupPaths, ungrouped, allGroups }) {
    const chosen = [];
    for (const path of paths) {
        if (ungrouped.includes(path)) return [...allGroups];

        let hit = false;
        for (const [group, patterns] of Object.entries(groupPaths)) {
            if (!pathsTouch([path], patterns)) continue;

            if (!chosen.includes(group)) chosen.push(group);
            hit = true;
        }

        if (!hit) return [...allGroups];
    }

    return chosen;
}

/** 検査の一覧・束・群の割り当てを、それを持つ側に出させて読む。 */
export function manifestOf(set) {
    const root = resolve(import.meta.dirname, '..');
    const ran = spawnSync('pwsh', ['-NoProfile', '-NonInteractive', '-File',
        join('scripts', 'check-manifest.ps1'), '-Set', set],
    { cwd: root, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
    if (ran.status !== 0) {
        throw new Error('検査の一覧を読めない: ' + (ran.stdout || '') + (ran.stderr || ''));
    }

    return JSON.parse(ran.stdout);
}

