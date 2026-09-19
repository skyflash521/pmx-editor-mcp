// 枠組みが持たない担保を確かめる一続きの実行。求める値と出た値を突き合わせ、合否を終了コードで
// 返す。確かめる相手を通さずに直に呼ぶ。

import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

import {
    CAPPED_CODE, bundlesOf, derivedLimitOf, groupedCheckGap, isReady, judgeResult,
    killDescendants, listedCheckGap, listedChecks, pathsTouch, runCapped, splitIntoForms,
    verdictOf, weightOf,
} from './checks.mjs';

const root = resolve(import.meta.dirname, '..');
process.chdir(root);

const sleeps = 'setInterval(() => {}, 1000);';

/** その置き場へ自分の番号を書いてから寝る孫を、1つ起こして寝る相手。 */
function spawnsGrandchildAt(path) {
    const grandchild = `require('node:fs').writeFileSync(${JSON.stringify(path)},`
        + ` String(process.pid)); ${sleeps}`;

    return ['-e', 'require(\'node:child_process\').spawn(process.execPath,'
        + ` ['-e', ${JSON.stringify(grandchild)}], { stdio: 'ignore' }); ${sleeps}`];
}

/** その置き場に書かれた番号。まだ無ければ0。 */
function idIn(path) {
    try {
        return Number.parseInt(readFileSync(path, 'utf8').trim(), 10) || 0;
    } catch {
        return 0;
    }
}

/** その置き場へ番号が書かれるまで待つ。 */
async function idBornIn(path) {
    for (let at = 0; at < 100; at++) {
        const id = idIn(path);
        if (id > 0) return id;

        await new Promise((wake) => setTimeout(wake, 50));
    }

    return 0;
}

/** その番号の相手が居るか。居なくなるまでのわずかな間は待つ。 */
async function stillAlive(pid) {
    for (let at = 0; at < 20; at++) {
        try {
            process.kill(pid, 0);
        } catch {
            return false;
        }

        await new Promise((wake) => setTimeout(wake, 50));
    }

    return true;
}

/**
 * 別のプロセスを起こして観る分。**呼ばれるまで走らせない**——名前を挙げるだけの呼ばれ方で、
 * 木を潰す副作用まで走らせない。一度だけ走らせて使い回す。
 */
let observed = null;
function observe() {
    return observed ??= (async () => {
        const work = mkdtempSync(join(tmpdir(), 'pmx-editor-mcp-stub-'));
        const born = join(work, 'born.txt');
        const wholeBorn = join(work, 'whole.txt');

        // 実行ごと止めた回の始末を通す木。打ち切りの題材と同時に立てる。
        const whole = spawn('node', spawnsGrandchildAt(wholeBorn), { stdio: 'ignore' });

        // どれも自分の子だけを相手にするので、同時に走らせてよい。
        const never = new AbortController();
        const bornCut = new AbortController();
        const began = process.hrtime.bigint();
        const [capped, tree, , uncapped, unlimited, missing] = await Promise.all([
            runCapped(never.signal, { file: 'node', args: ['-e', sleeps], limitSeconds: 0.5 }),
            // 孫が生まれた時点で打ち切り、木を辿った先まで終わることを見る。
            runCapped(bornCut.signal,
                { file: 'node', args: spawnsGrandchildAt(born), limitSeconds: 0 }),
            idBornIn(born).then((id) => { bornCut.abort(); return id; }),
            runCapped(never.signal,
                { file: 'node', args: ['-e', 'process.exit(3)'], limitSeconds: 9 }),
            runCapped(never.signal,
                { file: 'node', args: ['-e', 'process.exit(5)'], limitSeconds: 0 }),
            // 起こせない相手。誤りと終わりの両方が知らされる道を通す。
            runCapped(never.signal, { file: 'pmx-editor-mcp-居ない相手', args: [], limitSeconds: 9 }),
        ]);
        const cutSeconds = Number(process.hrtime.bigint() - began) / 1e9;

        const grandchild = idIn(born);
        const wholeGrandchild = await idBornIn(wholeBorn);
        killDescendants(process.pid);
        rmSync(work, { recursive: true, force: true });

        const grandchildLeft = grandchild > 0 && await stillAlive(grandchild);
        const wholeLeft = wholeGrandchild > 0 && await stillAlive(wholeGrandchild);
        // 残ってしまった相手は、この題材が始末する。
        for (const one of [grandchildLeft ? grandchild : 0, wholeLeft ? wholeGrandchild : 0,
            whole.pid]) {
            if (one) spawnSync('taskkill', ['/T', '/F', '/PID', String(one)], { stdio: 'ignore' });
        }

        return { capped, tree, uncapped, unlimited, missing, cutSeconds, grandchild,
            grandchildLeft, wholeGrandchild, wholeLeft };
    })();
}

// 手順書の一覧と群の割り当ては、食い違いの向きが2つある。実物の並びを起点に片側だけを崩して渡す。
const doc = 'docs/conventions/verification.md';
const section = '## 常設の検査';
const names = listedChecks(doc, section);
const fewer = names.slice(0, -1);
const added = [...names, '足した題材'];
const grouped = { '題材の群': names };

const unlisted = () => listedCheckGap(doc, section, added);
const unowned = () => listedCheckGap(doc, section, fewer);
const ungrouped = () => groupedCheckGap(grouped, added);
const unknown = () => groupedCheckGap(grouped, fewer);

const derived = () => derivedLimitOf([
    [{ name: '作る題材', limitSeconds: 7, bundle: '作る題材' }],
    [{ name: '長い題材', limitSeconds: 11, bundle: '束' },
        { name: '同じ束の題材', limitSeconds: 13, bundle: '束' },
        { name: '別の束の題材', limitSeconds: 17, bundle: '別の束' }],
]);
const bundles = () => bundlesOf([
    { name: 'あ', bundle: '束' }, { name: 'い' }, { name: 'う', bundle: '束' }]);

const splitting = (given) => splitIntoForms([{
    name: '割る題材', bundle: given.bundle ?? '割る題材', formsInOrder: given.formsInOrder ?? false,
    file: 'pwsh', args: ['-File', '題材'], limitSeconds: 12, forms: ['あ', 'い', 'う', 'え'],
    formArgument: '-Form', resultsArgument: '-Results',
}], '', 2);
const split = () => splitting({});
const inOrder = () => splitting({ formsInOrder: true });
const bundled = () => splitting({ bundle: '別の束' });
const groupsOf = (checks) => new Set(checks.map((check) => check.group)).size;

/**
 * 突き合わせる事柄。**出た値は呼ばれるまで求めない**——名前を挙げるだけの呼ばれ方と、形を絞った
 * 呼ばれ方で、要らない観測まで走らせない。
 */
const items = [
    { named: '打ち切りの終了コード', wanted: CAPPED_CODE,
        got: async () => (await observe()).capped.code },
    { named: '打ち切りの速さ', wanted: true, got: async () => (await observe()).cutSeconds < 5 },
    { named: '知らせによる打ち切り', wanted: CAPPED_CODE,
        got: async () => (await observe()).tree.code },
    { named: '打ち切り前の子孫', wanted: true, got: async () => (await observe()).grandchild > 0 },
    { named: '打ち切り後の子孫', wanted: false, got: async () => (await observe()).grandchildLeft },
    { named: '実行停止前の子孫', wanted: true,
        got: async () => (await observe()).wholeGrandchild > 0 },
    { named: '実行停止後の子孫', wanted: false, got: async () => (await observe()).wholeLeft },
    { named: '上限内の終了コード', wanted: 3, got: async () => (await observe()).uncapped.code },
    { named: '上限なしの終了コード', wanted: 5, got: async () => (await observe()).unlimited.code },
    { named: '起こせない相手の終了コード', wanted: 1,
        got: async () => (await observe()).missing.code },
    { named: '起こせない相手の手がかり', wanted: true,
        got: async () => (await observe()).missing.said.includes('pmx-editor-mcp-居ない相手') },
    { named: '上限超過の走り切り', wanted: CAPPED_CODE,
        got: () => judgeResult({ code: 0, seconds: 1.5 }, 1) },
    { named: '上限内の走り切り', wanted: 0, got: () => judgeResult({ code: 0, seconds: 0.5 }, 1) },
    { named: '未実行の判定', wanted: 0,
        got: () => judgeResult({ skipped: true, code: 0, seconds: 9 }, 1) },
    { named: '不合格ありの結末', wanted: 1,
        got: () => verdictOf({ failed: ['落ちる題材'], skipped: [], over: false }) },
    { named: '全合格の結末', wanted: 0,
        got: () => verdictOf({ failed: [], skipped: [], over: false }) },
    { named: '未実行ありの結末', wanted: 1,
        got: () => verdictOf({ failed: [], skipped: ['走らせない題材'], over: false }) },
    { named: '上限超過の結末', wanted: 1,
        got: () => verdictOf({ failed: [], skipped: [], over: true }) },
    { named: '門の通過', wanted: true,
        got: () => isReady('作った出来上がり', ['なし', '作った出来上がり']) },
    { named: '門の遮断', wanted: false, got: () => isReady('作った出来上がり', ['なし']) },
    { named: '導いた値', wanted: 31, got: derived },
    { named: '束の数', wanted: 2, got: () => bundles().size },
    { named: '同じ束の併合', wanted: 2, got: () => bundles().get('束').length },
    { named: '単独の束', wanted: 1, got: () => bundles().get('い').length },
    { named: '形へ割った件数', wanted: 4, got: () => split().length },
    { named: '形の組の数', wanted: 2, got: () => groupsOf(split()) },
    { named: '組ごとの束の分かれ', wanted: 2,
        got: () => new Set(split().map((check) => check.bundle)).size },
    { named: '形ごとの上限', wanted: 6, got: () => split()[0].limitSeconds },
    { named: '組へ渡す上限', wanted: 12, got: () => split()[0].run.limitSeconds },
    { named: '組の上限と形ごとの上限の和', wanted: true, got: () => {
        const sums = new Map();
        for (const check of split()) {
            sums.set(check.group, (sums.get(check.group) ?? 0) + check.limitSeconds);
        }

        return split().every((check) => sums.get(check.group) === check.run.limitSeconds);
    } },
    { named: '組を並べる見積り', wanted: 6,
        got: () => weightOf(split().filter((check) => check.group === split()[0].group)) },
    { named: '形を持たない検査の見積り', wanted: 5,
        got: () => weightOf([{ name: '割らない題材', limitSeconds: 5 }]) },
    { named: '組へ渡す形の並び', wanted: 'あ,う', got: () => split()[0].run.args.at(-1) },
    { named: '順に走る形の組の数', wanted: 1, got: () => groupsOf(inOrder()) },
    { named: '順に走る形の組の上限', wanted: 12, got: () => inOrder()[0].run.limitSeconds },
    { named: '順に走る形ごとの上限', wanted: 3, got: () => inOrder()[0].limitSeconds },
    { named: '順に走る組を並べる見積り', wanted: 12, got: () => weightOf(inOrder()) },
    { named: '宣言した束の継承', wanted: true,
        got: () => bundled().every((check) => check.bundle === '別の束') },
    { named: '宣言した束の組の数', wanted: 1, got: () => groupsOf(bundled()) },
    { named: '形を持たない検査', wanted: 1,
        got: () => splitIntoForms([{ name: '割らない題材', limitSeconds: 5 }], '', 2).length },
    { named: '道の一致', wanted: true,
        got: () => pathsTouch(['docs/conventions/verification.md', 'scripts/題材.mjs'],
            ['src/*.cs', 'scripts/題材*']) },
    { named: '道の不一致', wanted: false,
        got: () => pathsTouch(['docs/conventions/verification.md'], ['src/*.cs', 'scripts/題材*']) },
    { named: '手順書に無い名前', wanted: 1, got: () => unlisted().unlisted.length },
    { named: '手順書に無い名前の限定', wanted: 0, got: () => unlisted().unowned.length },
    { named: '実行器に無い名前', wanted: 1, got: () => unowned().unowned.length },
    { named: '実行器に無い名前の限定', wanted: 0, got: () => unowned().unlisted.length },
    { named: '群に無い検査', wanted: 1, got: () => ungrouped().ungrouped.length },
    { named: '群に無い検査の限定', wanted: 0, got: () => ungrouped().unknown.length },
    { named: '検査に無い名前', wanted: 1, got: () => unknown().unknown.length },
    { named: '検査に無い名前の限定', wanted: 0, got: () => unknown().ungrouped.length },
];

if (process.argv.includes('--list')) {
    console.log(items.map((item) => item.named).join('\n'));
    process.exit(0);
}

const formsAt = process.argv.indexOf('--forms');
const wanted = formsAt < 0 ? null : new Set(process.argv[formsAt + 1].split(','));
const resultsAt = process.argv.indexOf('--results');
const results = resultsAt < 0 ? null : process.argv[resultsAt + 1];

const wrong = [];
const said = [];
for (const item of items) {
    if (wanted && !wanted.has(item.named)) continue;

    const got = await item.got();
    const fell = String(got) === String(item.wanted)
        ? '' : `${item.named}: ${item.wanted} ではなく ${got}`;
    if (fell) wrong.push(fell);
    said.push(`${item.named}\t${fell ? 1 : 0}\t${fell}`);
}

if (results) writeFileSync(results, said.join('\n'), 'utf8');
if (wrong.length === 0) process.exit(0);

console.log('集計の結末が違う: ' + wrong.join(' / '));
process.exit(1);
