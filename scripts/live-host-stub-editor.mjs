// 実機動作確認を確かめるための、待受だけを持つエディタの代わり。
// 自分のプロセスIDから待受の名前とログの道を決め、状態の綴りを見て待受を開け閉めする。閉じよと
// 言われたら終わる。実機のエディタもホストも要らないので、常設の検査から走らせられる。
//
// 待受を実物と同じ名前で本当に開くのは、実行器が待受の有無を名前の一覧から見るからである。
// 一覧に現れるのは開いている待受だけなので、有無を作って見せることはできない。

import fs from "node:fs";
import net from "node:net";
import os from "node:os";
import path from "node:path";
import process from "node:process";

/** 待受のパイプ名の付け方。ホスト側の実装が定める。 */
const PIPE_PREFIX = "pmx-editor-mcp-";

/** 状態を持ち越す置き場の名前の書き出し。操作役の代わりと同じ綴りを使う。 */
const STATE_PREFIX = "pmx-editor-mcp-livehost-stub-";

/** 待受を残す違え方が、1つ目の停止でだけ効くようにする印の置き場。 */
const IGNORED_NAME = "pmx-editor-mcp-livehost-stub-ignored.txt";

/** 状態の綴り。待受を開けているか、閉じたか、終わるかを表す。 */
const RUNNING = "running";
const CLOSED = "closed";

/** 題材が違える形を伝える環境変数。操作役・待受・確認クライアントの代わりが同じ値を読む。 */
const BROKEN_NAME = "PMX_EDITOR_MCP_STUB_BROKEN";

/** 起動を1回ぶん記す行。実行器がこの書き出しで数える。 */
const STARTED_LINE = "プラグインを起動した: version=0.0.0-stub";

/** 常駐コネクタの取得と失効を記す行に共通する書き出し。ホスト側の実装が定める。 */
const CONNECTOR_MARK = "Cプラグインコネクタの";

const state = path.join(os.tmpdir(), STATE_PREFIX + process.pid + ".txt");
const marker = path.join(os.tmpdir(), IGNORED_NAME);
const log = path.join(os.tmpdir(), "pmx-editor-mcp-host-" + process.pid + ".log");
const broken = process.env[BROKEN_NAME] ?? "";

/** ホストのログへ1行足す。行の頭の時刻の書き方はホスト側の実装が定める。 */
function writeLog(body) {
    const at = new Date();
    const two = (value) => String(value).padStart(2, "0");
    const stamped = at.getFullYear() + "-" + two(at.getMonth() + 1) + "-" + two(at.getDate())
        + " " + two(at.getHours()) + ":" + two(at.getMinutes()) + ":" + two(at.getSeconds())
        + "." + String(at.getMilliseconds()).padStart(3, "0");
    fs.appendFileSync(log, stamped + " [1] " + body + "\n", "utf8");
}

let server = null;
let claimed = false;

function open() {
    if (server !== null) {
        return;
    }

    server = net.createServer();
    server.listen(path.join("\\\\.\\pipe\\", PIPE_PREFIX + process.pid));
}

function shut() {
    if (server === null) {
        return;
    }

    server.close();
    server = null;
}

function tick() {
    let wanted = RUNNING;
    try {
        wanted = fs.readFileSync(state, "utf8").trim();
    } catch {
        // 置き場がまだ無い回は、起こされた直後として開けたままにする。
    }

    if (wanted === CLOSED) {
        shut();
        fs.rmSync(state, { force: true });
        process.exit(0);
    }

    // 停めても待受を残すのが、待受の有無を見る側を違える形である。残すのは1つ目の停止だけに
    // する——どの停止でも残すと、切断を待つ側が待ちきれるまで実行が止まる。
    const stopped = wanted !== RUNNING;
    if (stopped && broken === "pipe" && !claimed && !fs.existsSync(marker)) {
        fs.writeFileSync(marker, String(process.pid), "utf8");
        claimed = true;
    }

    if (!stopped || claimed) {
        open();
    } else {
        shut();
    }
}

fs.writeFileSync(state, RUNNING, "utf8");
writeLog(STARTED_LINE);
if (broken === "log.started") {
    writeLog(STARTED_LINE);
}

writeLog(CONNECTOR_MARK + "取得");
open();
setInterval(tick, 50);
