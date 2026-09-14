// 参照クライアントからの実機動作確認。
// Claude Code をMCPクライアントとして非対話で起こし、引数を取るツールを実際に呼ばせて、
// 綴ったスキーマのとおりに引数が渡ることを確かめる。
//
// 仕様への適合を見るだけでは足りない。MCPのツール定義は組の直下へ oneOf や allOf を置くことを
// 許しており(2025-11-25 までの版は inputSchema に type だけを要り、追加の綴りを禁じない。
// 2026-07-28 の版はそれを明文で許す)、仕様に照らす検査では合格する。しかし入れ子を解かない
// クライアントでは引数が1つも渡らない。ここで確かめるのは、綴った形が仕様に合っているかでは
// なく、クライアントが実際に呼べるかである。
//
// 確かめるのは引数が渡ることであって、モデルの出来ではない。呼ぶツールと引数を指示で名指しし、
// 呼び出しに乗った値の型までを見る。

import { spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import process from "node:process";
import url from "node:url";

const here = path.dirname(url.fileURLToPath(import.meta.url));

/** エディタとホストの操作役。稼働状態と画面を触るのはこの1本に寄せる。 */
const CONTROL_SCRIPT = path.join(here, "host-control.ps1");

/** 導入の前置。ホストを配置し、MCPサーバーとして起こす相手を書き出す。 */
const SETUP_SCRIPT = path.join(here, "acceptance-setup-dev.ps1");

/** 登録するMCPサーバーの名前。ツールの綴りはこの名前から組み立てられる。 */
const SERVER_NAME = "pmx-editor-mcp";

/** ホストまで届いたうえで呼び出しが通らなかったことを指す書き出し。ホスト側の実装が定める。 */
const TOOL_ERROR_PREFIX = "TOOL_";

/** ブリッジがホストへ届かなかったことを指す書き出し。ブリッジの実装が定める。 */
const BRIDGE_ERROR_PREFIX = "BRIDGE_";

/** 操作役と前置を待つ上限。中で待ちを重ねるので、1つあたりの上限を上回る値を採る。 */
const CONTROL_TIMEOUT_MS = 180000;

/** 参照クライアントを待つ上限。ツールを数件呼ぶ往復に、起動と後始末を足した値。 */
const CLIENT_TIMEOUT_MS = 300000;

const EXIT_SUCCESS = 0;
const EXIT_FAILED = 1;
const EXIT_INPUT_UNAVAILABLE = 3;

/**
 * 呼ばせるツールと、乗っていなければならない引数。
 * 組の直下を分岐で綴っていたときに引数が落ちたものから採る——落ちない形だけを並べると、
 * 落ちたことをこの検査が見逃す。
 */
const CASES = [
    {
        tool: "model_list_materials",
        // 真偽と数を1つずつ含む。綴りが読まれないと、真偽が文字列になってホストが弾く。
        arguments: { all: true, limit: 3 },
    },
    {
        tool: "model_list_morph_offsets",
        // 親を選ぶ綴りと自分を選ぶ綴りが重なる一覧。分岐がもっとも深くなる形である。
        arguments: { parentAll: true, all: true, limit: 3 },
    },
];

function invokeScript(script, args) {
    const done = spawnSync(
        "pwsh",
        ["-NoProfile", "-File", script, ...args],
        { encoding: "utf8", timeout: CONTROL_TIMEOUT_MS });
    if (done.error !== undefined && done.error !== null) {
        throw new Error(script + " を起こせません: " + done.error.message);
    }

    if (done.status !== 0) {
        throw new Error(script + " が " + done.status + " で終わりました。\n" + done.stderr);
    }

    return done.stdout.trim();
}

/**
 * 起こしたエディタを閉じる。閉じられたかを返す。
 */
function close(script, editor) {
    if (editor === null || !/^[1-9][0-9]*$/.test(editor)) {
        return true;
    }

    const done = spawnSync(
        "pwsh",
        ["-NoProfile", "-File", script, "-Action", "close", "-ProcessId", editor],
        { encoding: "utf8", timeout: CONTROL_TIMEOUT_MS });
    if (done.error !== undefined && done.error !== null) {
        console.error("エディタを閉じられませんでした: " + done.error.message);
        return false;
    }

    if (done.status !== 0) {
        console.error(
            "エディタを閉じられませんでした(" + done.status + ")。\n" + done.stderr);
        return false;
    }

    return true;
}

/** 取った一時の場所を消す。消せたかを返す。取れていない場所は触らない。 */
function discard(room) {
    if (room === null) {
        return true;
    }

    try {
        fs.rmSync(room, { recursive: true, force: true });
        return true;
    } catch (error) {
        console.error("一時の場所を消せませんでした: " + room + " " + error.message);
        return false;
    }
}

/** 呼び出しへ乗った値が、要る引数を型ごと満たすか。 */
function carries(actual, expected) {
    if (actual === null || typeof actual !== "object") {
        return false;
    }

    return Object.keys(expected).every(
        (name) => JSON.stringify(actual[name]) === JSON.stringify(expected[name]));
}

/** 参照クライアントの出力から、ツールの呼び出しと返りを拾う。 */
function readCalls(text) {
    const calls = [];
    const results = new Map();
    for (const line of text.split(/\r?\n/)) {
        if (line.trim() === "") {
            continue;
        }

        let message;
        try {
            message = JSON.parse(line);
        } catch {
            continue;
        }

        const blocks = message.message === undefined ? null : message.message.content;
        if (!Array.isArray(blocks)) {
            continue;
        }

        for (const block of blocks) {
            if (block.type === "tool_use") {
                calls.push({ id: block.id, name: block.name, input: block.input });
            }

            if (block.type === "tool_result") {
                results.set(
                    block.tool_use_id,
                    Array.isArray(block.content)
                        ? block.content.map((c) => String(c.text)).join("")
                        : String(block.content));
            }
        }
    }

    return calls.map((call) => ({ ...call, said: results.get(call.id) }));
}

let editor = null;
let room = null;
let code = EXIT_SUCCESS;
try {
    const prepared = invokeScript(SETUP_SCRIPT, ["-Action", "prepare"]).split(/\r?\n/).pop();
    const server = JSON.parse(prepared);

    editor = invokeScript(CONTROL_SCRIPT, ["-Action", "launch"]).trim();
    if (!/^[1-9][0-9]*$/.test(editor)) {
        throw new Error("エディタを起こせませんでした: " + editor);
    }

    room = fs.mkdtempSync(path.join(os.tmpdir(), "pmx-editor-mcp-client-"));
    const config = path.join(room, "mcp.json");
    fs.writeFileSync(
        config,
        JSON.stringify({
            mcpServers: {
                [SERVER_NAME]: { command: server.command, args: server.arguments },
            },
        }),
        "utf8");

    const named = CASES.map((c) => "mcp__" + SERVER_NAME + "__" + c.tool);
    const orders = CASES
        .map((c, i) => (i + 1) + ". " + "mcp__" + SERVER_NAME + "__" + c.tool + " を "
            + Object.entries(c.arguments).map(([n, v]) => n + "=" + JSON.stringify(v)).join(", ")
            + " の引数で1回だけ呼べ。")
        .join("\n");

    // 参照クライアントの入口はWindowsでは .cmd なので、シェルを通さないと起こせない。
    const quoted = (value) => "\"" + value + "\"";
    const said = spawnSync(
        "claude",
        [
            "-p",
            "--mcp-config", quoted(config),
            "--strict-mcp-config",
            "--allowedTools", quoted(named.join(",")),
            "--output-format", "stream-json",
            "--verbose",
        ],
        {
            encoding: "utf8",
            timeout: CLIENT_TIMEOUT_MS,
            shell: true,
            input: "次のツールを、書いてある引数のとおりに順に1回ずつ呼べ。結果は要らない。\n"
                + orders,
        });
    if (said.error !== undefined && said.error !== null) {
        console.error("参照クライアントを起こせません: " + said.error.message);
        code = EXIT_INPUT_UNAVAILABLE;
    } else if (said.status !== 0) {
        console.error("参照クライアントが " + said.status + " で終わりました。\n" + said.stderr);
        code = EXIT_FAILED;
    } else {
        const calls = readCalls(said.stdout);
        for (const probe of CASES) {
            const named1 = "mcp__" + SERVER_NAME + "__" + probe.tool;
            const call = calls.find(
                (c) => c.name === named1 && carries(c.input, probe.arguments));
            if (call === undefined) {
                const tried = calls
                    .filter((c) => c.name === named1)
                    .map((c) => JSON.stringify(c.input))
                    .join(" / ");
                console.error(
                    "引数が渡りませんでした: " + probe.tool
                        + "\n  要る引数: " + JSON.stringify(probe.arguments)
                        + "\n  乗った引数: " + (tried === "" ? "(呼ばれていない)" : tried));
                code = EXIT_FAILED;
                continue;
            }

            const said1 = call.said === undefined ? "" : call.said;
            const lines = said1.split(/\r?\n/).map((l) => l.trim()).filter((l) => l !== "");
            if (lines.length === 0) {
                console.error(
                    "呼び出しの返りが取れませんでした: " + probe.tool
                        + "\n  乗った引数: " + JSON.stringify(call.input));
                code = EXIT_FAILED;
                continue;
            }

            const line = lines[0];
            const refused = lines.some(
                (l) => l.startsWith(TOOL_ERROR_PREFIX) || l.startsWith(BRIDGE_ERROR_PREFIX));
            if (refused) {
                console.error("呼び出しが通りませんでした: " + probe.tool + "\n  " + said1);
                code = EXIT_FAILED;
                continue;
            }

            console.log(
                "OK " + probe.tool + " " + JSON.stringify(call.input) + " -> " + line);
        }
    }
} catch (error) {
    console.error(error.message);
    code = EXIT_FAILED;
} finally {
    if (!close(CONTROL_SCRIPT, editor)) {
        code = EXIT_FAILED;
    }

    if (!discard(room)) {
        code = EXIT_FAILED;
    }
}

process.exit(code);
