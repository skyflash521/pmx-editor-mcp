import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';

import {
    CAPPED_CODE, bundlesOf, derivedLimitOf, isReady, judgeResult, killDescendants,
    pathsTouch, runCapped, splitIntoForms, verdictOf, weightOf,
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

        const whole = spawn('node', spawnsGrandchildAt(wholeBorn), { stdio: 'ignore' });

        const never = new AbortController();
        const bornCut = new AbortController();
        const began = process.hrtime.bigint();
        const [capped, tree, , uncapped, unlimited, missing] = await Promise.all([
            runCapped(never.signal, { file: 'node', args: ['-e', sleeps], limitSeconds: 0.5 }),
            runCapped(bornCut.signal,
                { file: 'node', args: spawnsGrandchildAt(born), limitSeconds: 0 }),
            idBornIn(born).then((id) => { bornCut.abort(); return id; }),
            runCapped(never.signal,
                { file: 'node', args: ['-e', 'process.exit(3)'], limitSeconds: 9 }),
            runCapped(never.signal,
                { file: 'node', args: ['-e', 'process.exit(5)'], limitSeconds: 0 }),
            runCapped(never.signal, { file: 'pmx-editor-mcp-居ない相手', args: [], limitSeconds: 9 }),
        ]);
        const cutSeconds = Number(process.hrtime.bigint() - began) / 1e9;

        const grandchild = idIn(born);
        const wholeGrandchild = await idBornIn(wholeBorn);
        killDescendants(process.pid);
        rmSync(work, { recursive: true, force: true });

        const grandchildLeft = grandchild > 0 && await stillAlive(grandchild);
        const wholeLeft = wholeGrandchild > 0 && await stillAlive(wholeGrandchild);
        for (const one of [grandchildLeft ? grandchild : 0, wholeLeft ? wholeGrandchild : 0,
            whole.pid]) {
            if (one) spawnSync('taskkill', ['/T', '/F', '/PID', String(one)], { stdio: 'ignore' });
        }

        return { capped, tree, uncapped, unlimited, missing, cutSeconds, grandchild,
            grandchildLeft, wholeGrandchild, wholeLeft };
    })();
}

/** 題材の中だけで使う呼び名。区別が付けば足りるので、番号から作る。 */
const naming = (at) => String(at);

/** その数だけ並んだ呼び名。 */
const namings = (count) => Array.from({ length: count }, (unused, at) => naming(at));

const derived = () => derivedLimitOf([
    [{ name: naming(0), limitSeconds: 7, bundle: naming(0) }],
    [{ name: naming(1), limitSeconds: 11, bundle: naming(9) },
        { name: naming(2), limitSeconds: 13, bundle: naming(9) },
        { name: naming(3), limitSeconds: 17, bundle: naming(8) }],
]);
const bundles = () => bundlesOf([
    { name: naming(0), bundle: naming(9) }, { name: naming(1) },
    { name: naming(2), bundle: naming(9) }]);

const splitting = (given) => splitIntoForms([{
    name: naming(0), bundle: given.bundle ?? naming(0), formsInOrder: given.formsInOrder ?? false,
    file: 'pwsh', args: [], limitSeconds: 12, forms: namings(4),
    formArgument: '-Form', resultsArgument: '-Results',
}], '', 2);
const split = () => splitting({});
const inOrder = () => splitting({ formsInOrder: true });
const bundled = () => splitting({ bundle: naming(9) });
const groupsOf = (checks) => new Set(checks.map((check) => check.group)).size;

/** 題材の中だけで使う道。綴りの形だけが要るので、呼び名から作る。 */
const kept = (name) => name + '/' + name + '.mjs';

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
    { named: '上限超過の走り切り', wanted: CAPPED_CODE,
        got: () => judgeResult({ code: 0, seconds: 1.5 }, 1) },
    { named: '上限内の走り切り', wanted: 0, got: () => judgeResult({ code: 0, seconds: 0.5 }, 1) },
    { named: '未実行の判定', wanted: 0,
        got: () => judgeResult({ skipped: true, code: 0, seconds: 9 }, 1) },
    { named: '不合格ありの結末', wanted: 1,
        got: () => verdictOf({ failed: namings(1), skipped: [], over: false }) },
    { named: '全合格の結末', wanted: 0,
        got: () => verdictOf({ failed: [], skipped: [], over: false }) },
    { named: '未実行ありの結末', wanted: 1,
        got: () => verdictOf({ failed: [], skipped: namings(1), over: false }) },
    { named: '上限超過の結末', wanted: 1,
        got: () => verdictOf({ failed: [], skipped: [], over: true }) },
    { named: '門の通過', wanted: true, got: () => isReady(naming(1), namings(2)) },
    { named: '門の遮断', wanted: false, got: () => isReady(naming(1), namings(1)) },
    { named: '導いた値', wanted: 31, got: derived },
    { named: '束の数', wanted: 2, got: () => bundles().size },
    { named: '同じ束の併合', wanted: 2, got: () => bundles().get(naming(9)).length },
    { named: '単独の束', wanted: 1, got: () => bundles().get(naming(1)).length },
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
        got: () => weightOf([{ name: naming(0), limitSeconds: 5 }]) },
    { named: '組へ渡す形の並び', wanted: '0,2', got: () => split()[0].run.args.at(-1) },
    { named: '順に走る形の組の数', wanted: 1, got: () => groupsOf(inOrder()) },
    { named: '順に走る形の組の上限', wanted: 12, got: () => inOrder()[0].run.limitSeconds },
    { named: '順に走る形ごとの上限', wanted: 3, got: () => inOrder()[0].limitSeconds },
    { named: '順に走る組を並べる見積り', wanted: 12, got: () => weightOf(inOrder()) },
    { named: '宣言した束の継承', wanted: true,
        got: () => bundled().every((check) => check.bundle === naming(9)) },
    { named: '宣言した束の組の数', wanted: 1, got: () => groupsOf(bundled()) },
    { named: '形を持たない検査', wanted: 1,
        got: () => splitIntoForms([{ name: naming(0), limitSeconds: 5 }], '', 2).length },
    { named: '道の一致', wanted: true,
        got: () => pathsTouch(namings(2).map(kept), [kept(naming(9)), naming(1) + '*']) },
    { named: '道の不一致', wanted: false,
        got: () => pathsTouch([kept(naming(0))], [kept(naming(9)), naming(9) + '*']) },
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
