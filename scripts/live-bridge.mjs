// ブリッジの実機動作確認。
// ブリッジをMCPサーバーとして起こし、エディタとホストの稼働状態を動かしながら ping を呼んで、
// 接続先の決まり方と切断の知らせを確かめる。
// ブリッジは成功した接続を保ち、接続先を決め直すのは未接続のときだけである。したがって接続先の
// 決まり方を確かめるには、その前に接続を切る操作を挟み、1回呼んで切断を検出させる必要がある。
// 以下の操作列はこの性質を織り込んであり、接続を保っていない状態から始まる。

import { spawnSync } from "node:child_process";
import path from "node:path";
import process from "node:process";
import url from "node:url";
import { McpClient } from "./mcp-client.mjs";

const here = path.dirname(url.fileURLToPath(import.meta.url));

/** エディタとホストの操作役。稼働状態と画面を触るのはこの1本に寄せる。 */
const CONTROL_SCRIPT = path.join(here, "host-control.ps1");

/** 導入の前置。ホストを配置し、MCPサーバーとして起こす相手を書き出す。 */
const SETUP_SCRIPT = path.join(here, "acceptance-setup-dev.ps1");

/** ホストが応答することを確かめるツールの名前。ブリッジの実装が定める。 */
const PING = "ping";

/** エラーの本文が、コードと説明の間に置く区切り。ブリッジの実装が定める。 */
const ERROR_SEPARATOR = ": ";

/** 待受のパイプ名の付け方。ホスト側の実装が定める。 */
const PIPE_PREFIX = "pmx-editor-mcp-";

/** 接続先を名乗る行の書き出し。ブリッジの実装が定める。 */
const TARGET_PREFIX = "接続先: ";

/** 接続先が移ったことを知らせる行の書き出し。ブリッジの実装が定める。 */
const TARGET_CHANGED_PREFIX = "接続先が変わった: ";

/** ホストが応答したときに返る本文。ホスト側の実装が定める。 */
const PONG = "pong";

/**
 * 操作役と前置を待つ上限。操作役は1回の呼び出しの中で待ちを最大3つ重ねる(押す・待受が消える・
 * 状態区分が変わる)ので、その1つあたりの上限の3倍を上回る値を採る。ここが先に切れると、
 * 操作役が自分の上限で諦める前に外から打ち切ることになり、何が起きたのかが分からなくなる。
 */
const CONTROL_TIMEOUT_MS = 180000;

const EXIT_SUCCESS = 0;
const EXIT_FAILED = 1;
const EXIT_INPUT_UNAVAILABLE = 3;

/** 操作役か前置を呼び、書き出したものを返す。落ちたら、その言い分を添えて投げる。 */
function invokeScript(script, args) {
    const done = spawnSync("pwsh", ["-NoProfile", "-File", script, ...args], {
        encoding: "utf8",
        timeout: CONTROL_TIMEOUT_MS,
    });
    if (done.error !== undefined) {
        throw new Error("pwsh を起こせません: " + done.error.message);
    }
    if (done.status !== 0) {
        throw new Error(
            path.basename(script) + " が " + done.status + " で終わりました: "
                + ((done.stderr ?? "").trim() || "(何も言いませんでした)"));
    }

    return (done.stdout ?? "").trim();
}

/** エディタとホストを操作する。状態が落ち着くのを待つのは操作役の側である。 */
function control(action, editorProcessId) {
    const args = ["-Action", action];
    if (editorProcessId !== undefined) {
        args.push("-ProcessId", String(editorProcessId));
    }

    return invokeScript(CONTROL_SCRIPT, args);
}

/** エディタを1つ起こし、そのプロセスIDを返す。 */
function launchEditor() {
    const written = control("launch");
    const editor = Number(written.split(/\r?\n/).pop());
    if (!Number.isInteger(editor) || editor <= 0) {
        throw new Error("エディタのプロセスIDを読めません: " + written);
    }

    return editor;
}

function pipeNameOf(editorProcessId) {
    return PIPE_PREFIX + editorProcessId;
}

/**
 * 期待の言い分を確かめる。合っていれば何も起きず、違えばその場で投げる。
 * 見るのはブリッジが返した本文だけで、画面の見た目は材料にしない。
 */
function expect(said, wanted, what) {
    if (!said.includes(wanted)) {
        throw new Error(what + ": 「" + wanted + "」を含まない本文が返りました: " + said);
    }
}

function expectHead(said, wanted, what) {
    if (!said.startsWith(wanted)) {
        throw new Error(what + ": 「" + wanted + "」で始まらない本文が返りました: " + said);
    }
}

/**
 * 繋がっている相手を名乗って応答することを確かめる。名乗りは本文の先頭行に出る。
 * moved を与えると、そこから移ってきた知らせであることまで見る。
 */
function expectPong(said, editorProcessId, what, moved) {
    const head = moved === undefined
        ? TARGET_PREFIX + pipeNameOf(editorProcessId)
        : TARGET_CHANGED_PREFIX + pipeNameOf(moved) + " から " + pipeNameOf(editorProcessId)
            + " へ。";
    expectHead(said, head, what);
    expect(said, PONG, what);
}

async function main() {
    let server;
    try {
        server = JSON.parse(invokeScript(SETUP_SCRIPT, ["-Action", "prepare"]).split(/\r?\n/).pop());
    } catch (error) {
        console.error("導入の前置を行えません: " + error.message);
        return EXIT_INPUT_UNAVAILABLE;
    }

    const client = new McpClient({ command: server.command, arguments: server.arguments ?? [] });
    const ping = async () => (await client.callTool(PING, {})).text;
    const editors = [];
    try {
        // 前置が動いているエディタをすべて閉じるので、待ち受けている相手は無い。起こしたての
        // ブリッジも接続を保っていない。操作列はこの状態を開始条件にする。
        await client.start();

        expectHead(await ping(), "BRIDGE_NO_EDITOR" + ERROR_SEPARATOR, "エディタが1つも無いとき");

        editors.push(launchEditor());
        expectPong(await ping(), editors[0], "エディタが1つのとき");

        control("stop", editors[0]);
        expectHead(await ping(), "BRIDGE_CONNECTION_LOST" + ERROR_SEPARATOR, "停止の直後");
        expectHead(await ping(), "BRIDGE_NO_HOST" + ERROR_SEPARATOR, "待ち受ける相手が無いとき");

        // 接続は切断の検出で捨てられているので、ここでの1回は名乗りから始まる。
        control("start", editors[0]);
        expectPong(await ping(), editors[0], "開始し直したとき");

        // 2つ目を起こしても1つ目への接続は生きている。決め直させるため、接続を切ってから戻す。
        editors.push(launchEditor());
        control("stop", editors[0]);
        control("start", editors[0]);
        expectHead(await ping(), "BRIDGE_CONNECTION_LOST" + ERROR_SEPARATOR,
            "2つ待ち受ける前の停止の直後");

        const many = await ping();
        expectHead(many, "BRIDGE_MULTIPLE_HOSTS" + ERROR_SEPARATOR, "2つ待ち受けているとき");

        // 並ぶ順はプロセスIDの昇順で、起こした順ではない。後から起こしたエディタが小さい
        // プロセスIDを割り当てられることがあるので、比べる前にプロセスIDでそろえる。
        const listed = [...editors]
            .sort((a, b) => a - b)
            .map((editor) => many.indexOf(pipeNameOf(editor)));
        if (listed.some((at) => at < 0)) {
            throw new Error("2つ待ち受けているとき: 候補が両方は並んでいません: " + many);
        }
        if (listed[0] > listed[1]) {
            throw new Error("2つ待ち受けているとき: 候補がプロセスIDの昇順ではありません: " + many);
        }

        // 待ち受ける相手が1つに戻れば、決め直しはその1つを選ぶ。
        control("stop", editors[0]);
        expectPong(await ping(), editors[1], "1つに戻ったとき", editors[0]);

        // 同じエディタを起こし直すとプロセスIDが変わる。登録は何も触らない。
        const closed = editors[1];
        control("close", closed);
        editors[1] = launchEditor();
        expectHead(await ping(), "BRIDGE_CONNECTION_LOST" + ERROR_SEPARATOR,
            "繋いでいたエディタの終了後");
        expectPong(await ping(), editors[1], "起こし直したエディタへ", closed);
    } catch (error) {
        console.error(error.message);
        return EXIT_FAILED;
    } finally {
        await client.stop();
        for (const editor of editors) {
            try {
                control("close", editor);
            } catch {
                // 既に終わっているエディタは閉じられない。後始末の失敗で合否を変えない。
            }
        }
    }

    console.log("すべて合格");
    return EXIT_SUCCESS;
}

process.exit(await main());
