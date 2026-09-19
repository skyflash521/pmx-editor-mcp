// 実機動作確認を確かめるための、待受へ繋がずに答えるだけの確認クライアント。
// 実物と同じ引数を受け、実物が書く言い分と終了コードを返すか、その1か所だけを違えて返す。
// 待受の有無だけは名前の一覧から本当に見る——繋げるかどうかで言い分が変わるので、そこを作って
// しまうと、待受を止める操作を確かめられなくなる。

import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import process from "node:process";

/** 待受のパイプ名の付け方。ホスト側の実装が定める。 */
const PIPE_PREFIX = "pmx-editor-mcp-";

/** 版が合わないハンドシェイクへホストが返すエラーコード。共通契約が定める。 */
const PROTOCOL_MISMATCH = -32001;

/** 題材が違える形を伝える環境変数。操作役・待受・この代わりが同じ値を読む。 */
const BROKEN_NAME = "PMX_EDITOR_MCP_STUB_BROKEN";

/** 常駐コネクタの取得と失効を記す行に共通する書き出し。ホスト側の実装が定める。 */
const CONNECTOR_MARK = "Cプラグインコネクタの";

/** 常駐コネクタを失効させる入口。実機ではデバッグ用の入口を開いたときだけ受け付ける。 */
const EXPIRE_METHOD = "debug_expire_connector";

/** 接続を保ったまま待つよう指示する語。実物の実装が定める。 */
const HOLD_WORD = "--hold";

const EXIT_OK = 0;
const EXIT_ERROR = 1;
const EXIT_CLOSED_AFTER_DISCONNECTING_ERROR = 3;
const EXIT_CLOSED_WHILE_HOLDING = 4;

function pipeNameOf(editor) {
    return PIPE_PREFIX + editor;
}

/** 待受が開いているか。名前の一覧から探す。 */
function listening(editor) {
    return fs.readdirSync("\\\\.\\pipe\\").includes(pipeNameOf(editor));
}

function logPathOf(editor) {
    return path.join(os.tmpdir(), "pmx-editor-mcp-host-" + editor + ".log");
}

/** ホストのログへ1行足す。行の頭の時刻の書き方はホスト側の実装が定める。 */
function writeLog(editor, body) {
    const at = new Date();
    const two = (value) => String(value).padStart(2, "0");
    const stamped = at.getFullYear() + "-" + two(at.getMonth() + 1) + "-" + two(at.getDate())
        + " " + two(at.getHours()) + ":" + two(at.getMinutes()) + ":" + two(at.getSeconds())
        + "." + String(at.getMilliseconds()).padStart(3, "0");
    fs.appendFileSync(logPathOf(editor), stamped + " [1] " + body + "\n", "utf8");
}

/**
 * 待受が閉じるまで待つ。上限は置かない——実物も、接続を保っている間は待ちの上限を外しており、
 * 諦める時刻を決めるのは呼ぶ側である。ここへ別の上限を置くと、どちらが先に切れるかで結末が
 * 変わる。
 */
async function waitClosed(editor) {
    while (listening(editor)) {
        await new Promise((resume) => setTimeout(resume, 100));
    }
}

/** 接続を保つ呼び出し。待受が閉じるのを待って、閉じられたことを言う。 */
async function hold(editor, broken) {
    console.log("接続しました: " + pipeNameOf(editor));
    console.log("接続を保持しています。");
    const toldPath = process.env.PMX_EDITOR_MCP_HOLDING_PATH;
    if (toldPath) fs.writeFileSync(toldPath, String(process.pid), "utf8");
    await waitClosed(editor);
    console.log("ホストが接続を切りました。");

    return broken === "client.holdCode" ? EXIT_OK : EXIT_CLOSED_WHILE_HOLDING;
}

/** 版の合わないハンドシェイクへの答え。断って切ることまでを言う。 */
function mismatched(broken) {
    console.log("handshake {\"protocol\":2} -> エラー " + PROTOCOL_MISMATCH);
    console.log("切断が要るエラー応答(" + PROTOCOL_MISMATCH
        + ")のあと、ホストが契約どおり接続を切りました。");

    return broken === "client.code" ? EXIT_ERROR : EXIT_CLOSED_AFTER_DISCONNECTING_ERROR;
}

/** 常駐コネクタを失効させる呼び出しへの答え。ホストが書く記録もここで足す。 */
function expired(editor, broken) {
    writeLog(editor, CONNECTOR_MARK + "失効");
    if (broken !== "log.renewal") {
        writeLog(editor, CONNECTOR_MARK + "取得");
    }

    console.log(EXPIRE_METHOD + " -> {\"renewed\":true}");

    return EXIT_OK;
}

async function main() {
    const args = process.argv.slice(2);
    const editor = Number(args[0]);
    const broken = process.env[BROKEN_NAME] ?? "";
    let words = args.slice(1);
    const holding = words[0] === HOLD_WORD;
    if (holding) {
        words = words.slice(1);
    }

    if (!listening(editor)) {
        console.error("接続または送受信に失敗しました: " + pipeNameOf(editor));

        return EXIT_ERROR;
    }

    if (holding) {
        return hold(editor, broken);
    }

    if (words.includes(EXPIRE_METHOD)) {
        return expired(editor, broken);
    }

    if (words.includes('{"protocol":2}')) {
        return mismatched(broken);
    }

    console.log(
        "接続しました: " + pipeNameOf(editor));

    return broken === "client.code" ? EXIT_ERROR : EXIT_OK;
}

process.exit(await main());
