// 枠組みが持たない担保を確かめる一続きの実行。求める値と出た値を突き合わせ、合否を終了コードで
// 返す。確かめる相手を通さずに直に呼ぶ。

import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

import {
    CAPPED_CODE, bundlesOf, derivedLimitOf, groupedCheckGap, isReady, judgeResult,
    killDescendants, listedCheckGap, listedChecks, pathsTouch, runCapped, verdictOf,
} from './checks.mjs';

const root = resolve(import.meta.dirname, '..');
process.chdir(root);

// 上限に達した検査を打ち切る道を通す題材。孫が生まれてから打ち切り、その番号の相手が残らない
// ことを見る。
const work = mkdtempSync(join(tmpdir(), 'pmx-editor-mcp-stub-'));
const sleeps = 'setInterval(() => {}, 1000);';
const spawnsGrandchildAt = (path) => {
    const grandchild = `require('node:fs').writeFileSync(${JSON.stringify(path)},`
        + ` String(process.pid)); ${sleeps}`;

    return ['-e', 'require(\'node:child_process\').spawn(process.execPath,'
        + ` ['-e', ${JSON.stringify(grandchild)}], { stdio: 'ignore' }); ${sleeps}`];
};
const born = join(work, 'born.txt');
const wholeBorn = join(work, 'whole.txt');

const idIn = (path) => {
    try {
        return Number.parseInt(readFileSync(path, 'utf8').trim(), 10) || 0;
    } catch {
        return 0;
    }
};

/** その置き場へ番号が書かれるまで待つ。 */
async function idBornIn(path) {
    for (let at = 0; at < 100; at++) {
        const id = idIn(path);
        if (id > 0) return id;

        await new Promise((wake) => setTimeout(wake, 50));
    }

    return 0;
}

// 実行ごと止めた回の始末を通す木。打ち切りの題材と同時に立てる。
const whole = spawn('node', spawnsGrandchildAt(wholeBorn), { stdio: 'ignore' });

// どれも自分の子だけを相手にするので、同時に走らせてよい。
const never = new AbortController();
const bornCut = new AbortController();
const began = process.hrtime.bigint();
const [capped, tree, , uncapped, unlimited, missing] = await Promise.all([
    runCapped(never.signal, { file: 'node', args: ['-e', sleeps], limitSeconds: 0.5 }),
    // 孫が生まれた時点で打ち切り、木を辿った先まで終わることを見る。
    runCapped(bornCut.signal, { file: 'node', args: spawnsGrandchildAt(born), limitSeconds: 0 }),
    idBornIn(born).then((id) => { bornCut.abort(); return id; }),
    runCapped(never.signal, { file: 'node', args: ['-e', 'process.exit(3)'], limitSeconds: 9 }),
    runCapped(never.signal, { file: 'node', args: ['-e', 'process.exit(5)'], limitSeconds: 0 }),
    // 起こせない相手。誤りと終わりの両方が知らされる道を通す。
    runCapped(never.signal, { file: 'pmx-editor-mcp-居ない相手', args: [], limitSeconds: 9 }),
]);
const cutSeconds = Number(process.hrtime.bigint() - began) / 1e9;

const grandchild = idIn(born);
const wholeGrandchild = await idBornIn(wholeBorn);
killDescendants(process.pid);
rmSync(work, { recursive: true, force: true });

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

const grandchildLeft = grandchild > 0 && await stillAlive(grandchild);
const wholeLeft = wholeGrandchild > 0 && await stillAlive(wholeGrandchild);
// 残ってしまった相手は、この題材が始末する。
for (const one of [grandchildLeft ? grandchild : 0, wholeLeft ? wholeGrandchild : 0, whole.pid]) {
    if (one) spawnSync('taskkill', ['/T', '/F', '/PID', String(one)], { stdio: 'ignore' });
}

// 手順書の一覧と群の割り当ては、食い違いの向きが2つある。実物の並びを起点に片側だけを崩して渡す。
const doc = 'docs/conventions/verification.md';
const section = '## 常設の検査';
const names = listedChecks(doc, section);
const fewer = names.slice(0, -1);
const added = [...names, '足した題材'];
const grouped = { '題材の群': names };

const unlisted = listedCheckGap(doc, section, added);
const unowned = listedCheckGap(doc, section, fewer);
const ungrouped = groupedCheckGap(grouped, added);
const unknown = groupedCheckGap(grouped, fewer);

// 導いた値は、回ごとに最も長い束の和を足したものになる。
const derived = derivedLimitOf([
    [{ name: '作る題材', limitSeconds: 7, bundle: '作る題材' }],
    [{ name: '長い題材', limitSeconds: 11, bundle: '束' },
        { name: '同じ束の題材', limitSeconds: 13, bundle: '束' },
        { name: '別の束の題材', limitSeconds: 17, bundle: '別の束' }],
]);
const bundles = bundlesOf([
    { name: 'あ', bundle: '束' }, { name: 'い' }, { name: 'う', bundle: '束' }]);

const wrong = [];
for (const item of [
    { about: '上限に達した検査を打ち切ること', wanted: CAPPED_CODE, got: capped.code },
    { about: '走り切るのを待たずに止めること', wanted: true, got: cutSeconds < 5 },
    { about: '知らせを受けて打ち切ること', wanted: CAPPED_CODE, got: tree.code },
    { about: '打ち切る前に子孫が生まれていること', wanted: true, got: grandchild > 0 },
    { about: '打ち切った検査の子孫を残さないこと', wanted: false, got: grandchildLeft },
    { about: '実行ごと止める前に子孫が生まれていること', wanted: true, got: wholeGrandchild > 0 },
    { about: '実行ごと止めた回に子孫を残さないこと', wanted: false, got: wholeLeft },
    { about: '上限の中で終わった検査', wanted: 3, got: uncapped.code },
    { about: '上限を持たない検査', wanted: 5, got: unlimited.code },
    { about: '起こせない相手の終了コード', wanted: 1, got: missing.code },
    { about: '起こせない相手の名前が手がかりに載ること', wanted: true,
        got: missing.said.includes('pmx-editor-mcp-居ない相手') },
    { about: '上限に達して走り切った検査', wanted: CAPPED_CODE,
        got: judgeResult({ code: 0, seconds: 1.5 }, 1) },
    { about: '上限の中で走り切った検査', wanted: 0,
        got: judgeResult({ code: 0, seconds: 0.5 }, 1) },
    { about: '走らせていない検査', wanted: 0,
        got: judgeResult({ skipped: true, code: 0, seconds: 9 }, 1) },
    { about: '落ちた検査がある実行', wanted: 1,
        got: verdictOf({ failed: ['落ちる題材'], skipped: [], over: false }) },
    { about: 'すべて通った実行', wanted: 0,
        got: verdictOf({ failed: [], skipped: [], over: false }) },
    { about: '走らせていない検査がある実行', wanted: 1,
        got: verdictOf({ failed: [], skipped: ['走らせない題材'], over: false }) },
    { about: '上限を超えた実行', wanted: 1,
        got: verdictOf({ failed: [], skipped: [], over: true }) },
    { about: '出来上がりが揃った検査の門', wanted: true,
        got: isReady('作った出来上がり', ['なし', '作った出来上がり']) },
    { about: '出来上がりが揃わない検査の門', wanted: false,
        got: isReady('作った出来上がり', ['なし']) },
    { about: '導いた値を検査ごとの上限から出すこと', wanted: 31, got: derived },
    { about: '束の数', wanted: 2, got: bundles.size },
    { about: '同じ束を1つにまとめること', wanted: 2, got: bundles.get('束').length },
    { about: '束を指していない検査を自分だけの束へ入れること', wanted: 1,
        got: bundles.get('い').length },
    { about: '入力に当たる道', wanted: true,
        got: pathsTouch(['docs/conventions/verification.md', 'scripts/題材.mjs'],
            ['src/*.cs', 'scripts/題材*']) },
    { about: '入力に当たらない道', wanted: false,
        got: pathsTouch(['docs/conventions/verification.md'], ['src/*.cs', 'scripts/題材*']) },
    { about: '手順書に無い名前', wanted: 1, got: unlisted.unlisted.length },
    { about: '手順書に無い名前だけを挙げること', wanted: 0, got: unlisted.unowned.length },
    { about: 'この実行器に無い名前', wanted: 1, got: unowned.unowned.length },
    { about: 'この実行器に無い名前だけを挙げること', wanted: 0, got: unowned.unlisted.length },
    { about: 'どの群にも無い検査', wanted: 1, got: ungrouped.ungrouped.length },
    { about: 'どの群にも無い検査だけを挙げること', wanted: 0, got: ungrouped.unknown.length },
    { about: '検査として在らない名前', wanted: 1, got: unknown.unknown.length },
    { about: '検査として在らない名前だけを挙げること', wanted: 0, got: unknown.ungrouped.length },
]) {
    if (String(item.got) === String(item.wanted)) continue;

    wrong.push(`${item.about}: ${item.wanted} ではなく ${item.got}`);
}

if (wrong.length === 0) process.exit(0);

console.log('集計の結末が違う: ' + wrong.join(' / '));
process.exit(1);
