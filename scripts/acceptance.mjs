// 受入シナリオの実行器。
// 定義に並ぶシナリオを、MCPサーバーとして起こしたブリッジ越しに1段ずつ実行し、返った結果を
// 定義が書いた期待と突き合わせて合否を出す。
// シナリオの中身はこの実行器が決めず、定義に書かれたものだけを読む。
// 導入の前置は差し替え点で、経路ごとの前置スクリプトが受け持つ。

import { spawn, spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import process from "node:process";
import url from "node:url";

/** 要求と応答の jsonrpc に固定で置く値。 */
const JSONRPC_VERSION = "2.0";

/** MCPの版。クライアントが名乗り、サーバーが合わせる。 */
const MCP_PROTOCOL_VERSION = "2025-06-18";

/** 1件の応答を待つ上限。ホスト側の処理上限へ往復の余裕を足した値。 */
const RESPONSE_TIMEOUT_MS = 130000;

/** 待受のパイプ名の付け方。ホスト側の実装が定める。 */
const PIPE_PREFIX = "pmx-editor-mcp-";

/** 接続先を名乗る行の書き出し。ブリッジの実装が定める。 */
const TARGET_PREFIX = "接続先: ";

/** 接続先が移ったことを知らせる行の書き出し。ブリッジの実装が定める。 */
const TARGET_CHANGED_PREFIX = "接続先が変わった: ";

/** 警告の行の書き出し。ブリッジの実装が定める。 */
const WARNING_PREFIX = "警告: ";

/**
 * エディタとホストの操作役。画面と稼働状態を触るのはこの1本に寄せる。
 * 差し替えられるのは、実行器そのものを実機のエディタ無しで確かめるためである——既定は実物で、
 * 開くのは実行時の引数に限る。
 */
const CONTROL_SCRIPT = path.join(
    path.dirname(url.fileURLToPath(import.meta.url)), "host-control.ps1");

/** PNGの先頭に必ず並ぶ印。写した絵かどうかをこれで見分ける。 */
const PNG_SIGNATURE = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);

/** PNGの幅が置かれている位置。印8バイト・長さ4バイト・種別4バイトの次から並ぶ。 */
const PNG_WIDTH_OFFSET = 16;

const EXIT_SUCCESS = 0;
const EXIT_FAILED = 1;
const EXIT_INVALID_ARGUMENTS = 2;
const EXIT_INPUT_UNAVAILABLE = 3;

/**
 * 引数を読み分ける。前置のスクリプトとその引数は差し替え点なので、この実行器は中身を解さず
 * そのまま渡す。
 */
function parseArguments(args) {
    const parsed = { cases: null, setup: null, setupArgs: [], control: CONTROL_SCRIPT };
    const named = { "--cases": "cases", "--setup": "setup", "--control": "control" };
    for (let at = 0; at < args.length; at++) {
        const name = args[at];
        const value = args[at + 1];
        if (named[name] !== undefined) {
            if (value === undefined) {
                return { error: name + " に値がありません。" };
            }
            parsed[named[name]] = value;
            at += 1;
            continue;
        }
        if (name === "--setup-arg") {
            if (value === undefined) {
                return { error: "--setup-arg に値がありません。" };
            }
            parsed.setupArgs.push(value);
            at += 1;
            continue;
        }
        return { error: "知らない引数: " + name };
    }

    if (parsed.cases === null || parsed.setup === null) {
        return { error: "--cases と --setup は省けません。" };
    }

    return { parsed };
}

/**
 * PowerShellのスクリプトを起こし、書き出したものと、落ちたときの事情を返す。
 * 出力の文字コードは端末の設定で変わるので、読めない並びは読めないまま置いて、数と綴りだけを
 * 確かに読めるようにする。
 */
function invokeScript(script, args, timeoutMs) {
    const done = spawnSync(
        "pwsh",
        ["-NoProfile", "-File", script, ...args],
        { encoding: "utf8", timeout: timeoutMs });
    if (done.error !== undefined) {
        return { written: null, unavailable: "pwsh を起こせません: " + done.error.message };
    }
    if (done.status !== 0) {
        return {
            written: null,
            unavailable: script + " が " + done.status + " で終わりました: "
                + ((done.stderr ?? "").trim() || "(何も言いませんでした)"),
        };
    }

    return { written: (done.stdout ?? "").trim(), unavailable: null };
}

/**
 * 導入の前置を行い、MCPサーバーとして起こす相手を受け取る。前置が何をするかはこの実行器の
 * 知るところではなく、最後の行へ置いた組だけを読む。
 */
function prepare(setup, setupArgs) {
    const done = invokeScript(setup, ["-Action", "prepare", ...setupArgs], RESPONSE_TIMEOUT_MS);
    if (done.written === null) {
        return { server: null, unavailable: done.unavailable };
    }

    const lines = done.written.split("\n").map((line) => line.trim()).filter((line) => line !== "");
    if (lines.length === 0) {
        return { server: null, unavailable: setup + " が起こす相手を書きませんでした。" };
    }

    let server;
    try {
        server = JSON.parse(lines[lines.length - 1]);
    } catch (error) {
        return {
            server: null,
            unavailable: setup + " が書いた相手を読み解けません: " + error.message,
        };
    }

    if (server === null || typeof server !== "object" || typeof server.command !== "string"
        || !Array.isArray(server.arguments)) {
        return {
            server: null,
            unavailable: setup + " が書いた相手が command と arguments の組ではありません。",
        };
    }

    return { server, unavailable: null };
}

/** MCPサーバーへ stdio で繋ぐクライアント。要求は1件ずつ投げ、応答を識別子で対応づける。 */
class McpClient {
    constructor(server) {
        this._server = server;
        this._child = null;
        this._buffer = "";
        this._pending = new Map();
        this._nextId = 1;
        this._ended = null;
    }

    /** サーバーを起こし、初期化まで済ませる。 */
    async start() {
        // 診断は受け取り手の標準エラー出力へ素通しする。捨てると、落ちた原因を書いていても
        // 読めなくなる。
        const child = spawn(this._server.command, this._server.arguments, {
            stdio: ["pipe", "pipe", "inherit"],
        });
        this._child = child;
        this._buffer = "";
        this._pending = new Map();
        this._nextId = 1;
        this._ended = null;

        // 落とした相手の後始末が、起こし直した相手へ及ばないようにする。落ちるのを待ち切れずに
        // 起こし直すことがあるので、いま繋いでいる相手かどうかを見てから畳む。
        const finish = (reason) => {
            if (this._child === child) {
                this._end(reason);
            }
        };
        child.on("error", (error) => finish("MCPサーバーを起こせません: " + error.message));
        child.on("exit", (code) => finish("MCPサーバーが " + code + " で終わりました。"));
        child.stdout.setEncoding("utf8");
        child.stdout.on("data", (chunk) => {
            if (this._child === child) {
                this._receive(chunk);
            }
        });

        await this._request("initialize", {
            protocolVersion: MCP_PROTOCOL_VERSION,
            capabilities: {},
            clientInfo: { name: "acceptance", version: "1" },
        });
        this._notify("notifications/initialized", {});
    }

    /** サーバーを落とし、起こし直す。接続を保っていない状態から始めたい段で使う。 */
    async restart() {
        await this.stop();
        await this.start();
    }

    /** サーバーを落とす。 */
    stop() {
        if (this._child === null) {
            return Promise.resolve();
        }

        const child = this._child;
        this._child = null;
        if (child.exitCode !== null || child.signalCode !== null) {
            // もう終わっている相手は、終わるのを待つと知らせが来ないまま止まる。
            return Promise.resolve();
        }

        return new Promise((resolve) => {
            child.once("exit", () => resolve());
            child.kill();
            // 落ちない相手を待ち続けない。次の段はサーバーを起こし直すところから始まる。
            setTimeout(() => resolve(), 5000).unref();
        });
    }

    /** ツールを1件呼び、返った本文と誤りの印を返す。 */
    async callTool(name, args) {
        const response = await this._request("tools/call", { name, arguments: args });
        if (response.error !== undefined) {
            throw new Error(
                "MCPサーバーが要求を断りました(" + response.error.code + "): "
                    + response.error.message);
        }

        const result = response.result;
        if (result === null || typeof result !== "object" || !Array.isArray(result.content)) {
            throw new Error("ツールの結果が content の並びを持ちません。");
        }

        const texts = result.content
            .filter((block) => block !== null && typeof block === "object" && block.type === "text")
            .map((block) => String(block.text));
        if (texts.length === 0) {
            throw new Error("ツールの結果に本文がありません。");
        }

        return { isError: result.isError === true, text: texts.join("\n") };
    }

    _request(method, params) {
        const id = this._nextId;
        this._nextId += 1;

        return new Promise((resolve, reject) => {
            if (this._ended !== null) {
                reject(new Error(this._ended));
                return;
            }

            const timer = setTimeout(() => {
                this._pending.delete(id);
                reject(new Error(method + " の応答が時間内に返りませんでした。"));
            }, RESPONSE_TIMEOUT_MS);
            timer.unref();
            this._pending.set(id, { resolve, reject, timer });
            this._child.stdin.write(
                JSON.stringify({ jsonrpc: JSONRPC_VERSION, id, method, params }) + "\n");
        });
    }

    _notify(method, params) {
        this._child.stdin.write(
            JSON.stringify({ jsonrpc: JSONRPC_VERSION, method, params }) + "\n");
    }

    _receive(chunk) {
        this._buffer += chunk;
        for (;;) {
            const at = this._buffer.indexOf("\n");
            if (at < 0) {
                return;
            }

            const line = this._buffer.slice(0, at).trim();
            this._buffer = this._buffer.slice(at + 1);
            if (line === "") {
                continue;
            }

            let message;
            try {
                message = JSON.parse(line);
            } catch {
                // サーバーが診断を標準出力へ混ぜることがある。要求の応答ではないので読み飛ばす。
                continue;
            }

            if (message === null || typeof message !== "object" || message.id === undefined) {
                continue;
            }

            const waiting = this._pending.get(message.id);
            if (waiting === undefined) {
                continue;
            }

            this._pending.delete(message.id);
            clearTimeout(waiting.timer);
            waiting.resolve(message);
        }
    }

    _end(reason) {
        this._ended = reason;
        for (const waiting of this._pending.values()) {
            clearTimeout(waiting.timer);
            waiting.reject(new Error(reason));
        }
        this._pending.clear();
    }
}

/**
 * 覚えた値を差し込む。差し込む先は定義の中の `$from` の組で、覚えていなければ投げる。
 * 文字列は環境変数の名前も広げる——置き場を指す値は、ファイルの用意に渡す先とツールへ渡す先の
 * 両方に現れるので、片方だけを広げると別の場所を指すことになる。
 */
function fill(node, remembered) {
    if (Array.isArray(node)) {
        return node.map((item) => fill(item, remembered));
    }

    if (typeof node === "string") {
        return expand(node);
    }

    if (node === null || typeof node !== "object") {
        return node;
    }

    if (typeof node.$from === "string") {
        if (!remembered.has(node.$from)) {
            throw new Error("まだ覚えていない値を borrow しています: " + node.$from);
        }

        return remembered.get(node.$from);
    }

    const filled = {};
    for (const [name, value] of Object.entries(node)) {
        filled[name] = fill(value, remembered);
    }

    return filled;
}

/** 環境変数の名前を値へ広げる。広げられない名前があればその名前を投げる。 */
function expand(text) {
    return text.replace(/%([^%]+)%/g, (whole, name) => {
        const value = process.env[name];
        if (value === undefined) {
            throw new Error("環境変数が定義されていません: " + name);
        }

        return value;
    });
}

/** 応答の本文を、接続先の知らせ・値の行・警告の行へ分ける。 */
function split(text) {
    let notice = null;
    let body = text;
    if (text.startsWith(TARGET_PREFIX) || text.startsWith(TARGET_CHANGED_PREFIX)) {
        const at = text.indexOf("\n");
        notice = at < 0 ? text : text.slice(0, at);
        body = at < 0 ? "" : text.slice(at + 1);
    }

    const lines = body.split("\n");

    return {
        notice,
        value: lines[0],
        warnings: lines.slice(1)
            .filter((line) => line.startsWith(WARNING_PREFIX))
            .map((line) => line.slice(WARNING_PREFIX.length)),
    };
}

/** 道をたどって値を取り出す。たどれなければ undefined。 */
function select(value, dotted) {
    if (dotted === "") {
        return value;
    }

    let held = value;
    for (const step of dotted.split(".")) {
        if (held === null || typeof held !== "object") {
            return undefined;
        }
        held = Array.isArray(held) ? held[Number.parseInt(step, 10)] : held[step];
    }

    return held;
}

/** 値が同じか。並びと組は中身まで見る。 */
function same(left, right) {
    if (Array.isArray(left) || Array.isArray(right)) {
        if (!Array.isArray(left) || !Array.isArray(right) || left.length !== right.length) {
            return false;
        }

        return left.every((item, at) => same(item, right[at]));
    }

    if (left === null || right === null || typeof left !== "object" || typeof right !== "object") {
        return left === right;
    }

    const names = Object.keys(left);

    return names.length === Object.keys(right).length
        && names.every((name) => same(left[name], right[name]));
}

/** 写した絵の大きさ。PNGでなければ null。 */
function imageSize(base64) {
    let bytes;
    try {
        bytes = Buffer.from(base64, "base64");
    } catch {
        return null;
    }

    if (bytes.length < PNG_WIDTH_OFFSET + 8 || !bytes.subarray(0, 8).equals(PNG_SIGNATURE)) {
        return null;
    }

    return {
        width: bytes.readUInt32BE(PNG_WIDTH_OFFSET),
        height: bytes.readUInt32BE(PNG_WIDTH_OFFSET + 4),
    };
}

/** 大きさを「幅x高さ」で綴る。写し取った側と同じ綴り方にそろえる。 */
function describeSize(size) {
    return size.width + "x" + size.height;
}

/**
 * 絵の期待を確かめる。返った絵が写しより小さければ縮小の警告が要り、同じ大きさなら要らない
 * ——どちらであるかは、写した実寸と返った絵の実寸だけで決まる。
 */
function judgeImage(expected, parsed, remembered) {
    let value;
    try {
        value = JSON.parse(parsed.value);
    } catch (error) {
        return "絵を読み解けません: " + error.message;
    }

    if (typeof value !== "string" || value.length === 0) {
        return "絵が文字列で返りませんでした。";
    }

    const returned = imageSize(value);
    if (returned === null) {
        return "返ったものがPNGではありません。";
    }

    if (expected.capturedAs === undefined) {
        // 写しと結び付けない絵でも、縮めたと言うからには縮めた先を述べていなければならない。
        const named = parsed.warnings.some((warning) => warning.includes(describeSize(returned)));

        return parsed.warnings.length === 0 || named
            ? null
            : "警告が返った絵の寸法 " + describeSize(returned) + " を述べていません: "
                + parsed.warnings.join(" / ");
    }

    if (!remembered.has(expected.capturedAs)) {
        return "まだ写し取っていません: " + expected.capturedAs;
    }

    const captured = remembered.get(expected.capturedAs);
    if (returned.width > captured.width || returned.height > captured.height) {
        return "返った絵が写した実寸 " + describeSize(captured) + " より大きい: "
            + describeSize(returned);
    }

    const reduced = returned.width !== captured.width || returned.height !== captured.height;
    const warned = parsed.warnings.some((warning) => warning.includes(describeSize(captured))
        && warning.includes(describeSize(returned)));
    if (reduced && !warned) {
        return "縮めた絵に、元寸法 " + describeSize(captured) + " と縮小後寸法 "
            + describeSize(returned) + " を述べる警告が付いていません。";
    }

    if (!reduced && parsed.warnings.length !== 0) {
        return "縮めていない絵に警告が付きました: " + parsed.warnings.join(" / ");
    }

    return null;
}

/**
 * 取り出したイベントの期待を確かめる。並べた種別は、その順に現れることまで見る——起きた順に
 * 読み戻せることが要求なので、揃っているだけでは足りない。
 */
function judgeEvents(expected, parsed, remembered) {
    let value;
    try {
        value = JSON.parse(parsed.value);
    } catch (error) {
        return "イベントを読み解けません: " + error.message;
    }

    const events = select(value, "events");
    if (!Array.isArray(events)) {
        return "events の並びが返りませんでした。";
    }

    const wanted = fill(expected, remembered);
    const mine = events.filter((event) => event !== null && typeof event === "object"
        && event.sourceHandle === wanted.sourceHandle);

    let at = 0;
    for (const type of wanted.types) {
        while (at < mine.length && mine[at].type !== type) {
            at += 1;
        }

        if (at >= mine.length) {
            return "この発生元の操作が " + JSON.stringify(wanted.types)
                + " の順に取れていません: " + JSON.stringify(mine.map((e) => e.type));
        }

        at += 1;
    }

    return null;
}

/** 接続先の知らせの期待を確かめる。 */
function judgeNotice(expected, notice, remembered) {
    const wanted = fill(expected, remembered);
    if (wanted.editor !== undefined) {
        const line = TARGET_PREFIX + PIPE_PREFIX + wanted.editor;

        return notice === line ? null : "接続先の知らせが " + JSON.stringify(line)
            + " で始まりません: " + JSON.stringify(notice);
    }

    if (notice === null || !notice.startsWith(TARGET_CHANGED_PREFIX)) {
        return "接続先が移ったことを知らせていません: " + JSON.stringify(notice);
    }

    if (wanted.changedFrom !== undefined
        && !notice.includes(TARGET_CHANGED_PREFIX + PIPE_PREFIX + wanted.changedFrom + " から ")) {
        return "移る前の接続先が " + PIPE_PREFIX + wanted.changedFrom + " ではありません: " + notice;
    }

    if (wanted.changedTo !== undefined
        && !notice.includes(" から " + PIPE_PREFIX + wanted.changedTo + " へ。")) {
        return "移った先が " + PIPE_PREFIX + wanted.changedTo + " ではありません: " + notice;
    }

    return null;
}

/** 1件のツール呼び出しの結末。合っていれば null、違っていればその理由を返す。 */
function judge(expected, response, remembered) {
    const parsed = split(response.text);
    if (expected.ok !== undefined && expected.ok === response.isError) {
        return (expected.ok ? "成功するはずが断られました: " : "断るはずが成功しました: ")
            + parsed.value;
    }

    if (expected.code !== undefined && !parsed.value.startsWith(expected.code + ": ")) {
        return "断る理由が " + expected.code + " ではありません: " + parsed.value;
    }

    if (expected.notice !== undefined) {
        const broken = judgeNotice(expected.notice, parsed.notice, remembered);
        if (broken !== null) {
            return broken;
        }
    }

    if (expected.body !== undefined && parsed.value !== expected.body.equals) {
        return "本文が " + JSON.stringify(expected.body.equals) + " ではありません: "
            + JSON.stringify(parsed.value);
    }

    if (expected.values !== undefined) {
        let value;
        try {
            value = JSON.parse(parsed.value);
        } catch (error) {
            return "値を読み解けません: " + error.message;
        }

        for (const wanted of expected.values) {
            const taken = select(value, wanted.path);
            const compared = fill(wanted.equals, remembered);
            if (!same(taken, compared)) {
                return wanted.path + " が " + JSON.stringify(compared) + " ではありません: "
                    + JSON.stringify(taken);
            }
        }
    }

    if (expected.image !== undefined) {
        const broken = judgeImage(expected.image, parsed, remembered);
        if (broken !== null) {
            return broken;
        }
    }

    if (expected.events !== undefined) {
        const broken = judgeEvents(expected.events, parsed, remembered);
        if (broken !== null) {
            return broken;
        }
    }

    return null;
}

/** 覚えるものを控える。覚え方は段の側が書いた形に従う。 */
function remember(step, taken, remembered) {
    if (step.record === undefined) {
        return;
    }

    remembered.set(step.record.name, taken);
}

/** ツールの応答から覚える値を取り出す。 */
function takeFromResponse(step, response) {
    if (step.record === undefined) {
        return null;
    }

    const parsed = split(response.text);

    return select(JSON.parse(parsed.value), step.record.path);
}

/**
 * 動いているエディタのプロセスID。待受で数えない——ホストを停止させたエディタはパイプを
 * 持たないが、実行ファイルは掴んだまま残るので、閉じ残すと次の配置が失敗する。
 */
function listEditors(control) {
    const done = invokeScript(control, ["-Action", "editors"], RESPONSE_TIMEOUT_MS);
    if (done.written === null) {
        throw new Error(done.unavailable);
    }

    return done.written.split("\n")
        .map((line) => Number.parseInt(line.trim(), 10))
        .filter((id) => Number.isInteger(id));
}

/** エディタとホストの操作を1つ行い、覚える値があれば返す。 */
function operate(step, remembered, control) {
    if (step.action === "closeAll") {
        for (const editor of listEditors(control)) {
            const done = invokeScript(
                control,
                ["-Action", "close", "-ProcessId", String(editor)],
                RESPONSE_TIMEOUT_MS);
            if (done.written === null) {
                throw new Error(done.unavailable);
            }
        }

        return null;
    }

    const args = ["-Action", step.action];
    if (step.editor !== undefined) {
        args.push("-ProcessId", String(fill(step.editor, remembered)));
    }
    if (step.view !== undefined) {
        args.push("-View", step.view);
    }

    if (step.action === "capture") {
        args.push("-Path", path.join(os.tmpdir(), "pmx-editor-mcp-acceptance-view.png"));
    }

    const done = invokeScript(control, args, RESPONSE_TIMEOUT_MS);
    if (done.written === null) {
        throw new Error(done.unavailable);
    }

    const written = done.written;
    if (step.record === undefined) {
        return null;
    }

    if (step.record.shape === "size") {
        const measured = /^(\d+)x(\d+)$/.exec(written);
        if (measured === null) {
            throw new Error("写した大きさを読めません: " + written);
        }

        return {
            width: Number.parseInt(measured[1], 10),
            height: Number.parseInt(measured[2], 10),
        };
    }

    const number = Number.parseInt(written, 10);
    if (!Number.isInteger(number)) {
        throw new Error("操作の戻り値を数として読めません: " + written);
    }

    return number;
}

/** ファイルの用意と後始末を1つ行う。期待と違えばその理由を返す。 */
function handleFile(step) {
    const target = expand(step.path);
    if (step.action === "ensureDirectory") {
        fs.mkdirSync(target, { recursive: true });

        return null;
    }

    if (step.action === "ensureMissing") {
        fs.rmSync(target, { force: true, recursive: true });

        return null;
    }

    if (step.action === "removeTree") {
        fs.rmSync(target, { force: true, recursive: true });

        return null;
    }

    if (step.action === "expectPresent") {
        return fs.existsSync(target) ? null : "作られているはずのものがありません: " + target;
    }

    return fs.existsSync(target) ? "無いはずのものがあります: " + target : null;
}

/**
 * 1本のシナリオを走らせ、こなした段の数を数える。合っていれば理由は null、違っていればその理由を
 * 返す。数を返すのは、途中で飛ばした実行器を合格と見分けるためである。
 */
async function runScenario(scenario, client, remembered, control) {
    let done = 0;
    for (const step of scenario.steps) {
        const where = "段 " + (done + 1) + "(" + step.kind + ")";
        let broken;
        try {
            broken = await runStep(step, client, remembered, control);
        } catch (error) {
            broken = error.message;
        }

        if (broken !== null) {
            return { done, reason: where + ": " + broken };
        }

        done += 1;
    }

    return { done, reason: null };
}

/** 1つの段をこなす。合っていれば null、違っていればその理由を返す。 */
async function runStep(step, client, remembered, control) {
    if (step.kind === "tool") {
        const response = await client.callTool(step.tool, fill(step.arguments, remembered));
        const broken = judge(step.expect, response, remembered);
        if (broken !== null) {
            return step.tool + ": " + broken;
        }

        remember(step, takeFromResponse(step, response), remembered);

        return null;
    }

    if (step.kind === "control") {
        remember(step, operate(step, remembered, control), remembered);

        return null;
    }

    if (step.kind === "file") {
        return handleFile(step);
    }

    if (step.kind === "server") {
        await client.restart();

        return null;
    }

    return "知らない段の種別です。";
}

function readScenarios(from) {
    const read = JSON.parse(fs.readFileSync(from, "utf8"));
    if (read === null || typeof read !== "object" || !Array.isArray(read.scenarios)) {
        throw new Error("scenarios の並びを持たない。");
    }

    return read.scenarios;
}

const { parsed, error } = parseArguments(process.argv.slice(2));
if (error !== undefined) {
    console.error(error);
    console.error(
        "使い方: node acceptance.mjs --cases <定義のパス> --setup <前置のパス>"
            + " [--setup-arg <値>]... [--control <操作役のパス>]");
    process.exit(EXIT_INVALID_ARGUMENTS);
}

let scenarios;
try {
    scenarios = readScenarios(parsed.cases);
} catch (thrown) {
    console.error("定義を読めません(" + parsed.cases + "): " + thrown.message);
    process.exit(EXIT_INPUT_UNAVAILABLE);
}

if (scenarios.length === 0) {
    console.error("走らせるシナリオがありません。");
    process.exit(EXIT_INVALID_ARGUMENTS);
}

const prepared = prepare(parsed.setup, parsed.setupArgs);
if (prepared.server === null) {
    console.error("導入の前置に失敗しました: " + prepared.unavailable);
    process.exit(EXIT_INPUT_UNAVAILABLE);
}

const client = new McpClient(prepared.server);
const remembered = new Map();
let failed = null;
try {
    await client.start();
    for (const scenario of scenarios) {
        const ran = await runScenario(scenario, client, remembered, parsed.control);
        const where = "シナリオ" + scenario.id + " " + scenario.title
            + "(段 " + ran.done + "/" + scenario.steps.length + ")";
        if (ran.reason !== null) {
            console.log("不合格: " + where + " — " + ran.reason);
            failed = scenario;
            break;
        }

        console.log("合格: " + where);
    }
} catch (thrown) {
    console.error("走らせられませんでした: " + thrown.message);
    await client.stop();
    process.exit(EXIT_INPUT_UNAVAILABLE);
}

await client.stop();

console.log("");
console.log(
    "シナリオ: " + scenarios.length + " 本・合格 "
        + (failed === null ? scenarios.length : scenarios.indexOf(failed))
        + "・不合格 " + (failed === null ? 0 : 1));

process.exit(failed === null ? EXIT_SUCCESS : EXIT_FAILED);
