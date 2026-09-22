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
import { CASES, SERVER_NAME, named } from "./live-client-cases.mjs";

const here = path.dirname(url.fileURLToPath(import.meta.url));

/**
 * エディタとホストの操作役。稼働状態と画面を触るのはこの1本に寄せる。
 * 差し替えられるのは、この実行器そのものを実機のエディタ無しで確かめるためである——既定は
 * 実物で、開くのは実行時の引数に限る。
 */
let CONTROL_SCRIPT = path.join(here, "host-control.ps1");

/** 導入の前置。ホストを配置し、MCPサーバーとして起こす相手を書き出す。 */
let SETUP_SCRIPT = path.join(here, "acceptance-setup-dev.ps1");

/** 呼ばせる相手。参照クライアントの入口の綴りで、実行時の引数で差し替えられる。 */
let CLIENT_COMMAND = "claude";

/** 応答サイズ予算の文字数を与える環境変数。ホストとブリッジが同じ値を読む。 */
const BUDGET_NAME = "PMX_EDITOR_MCP_BUDGET_CHARS";

/**
 * 参照クライアントの停止で音も通知も出させない環境変数。Claude Code の flow プラグインの
 * Stop フックが読む。
 */
const UNATTENDED_NAME = "FLOW_UNATTENDED";

/**
 * この検査で与える応答サイズ予算。受理される下限を採る——画像は予算で測ってはならないので、
 * 測っていないことを見るには、どんなに軽いビューの画像でも予算を超える値が要る。この開発環境の
 * 実測では、モデルを読み込んでいない起動直後のビューでも詰めた文字は15,174文字だったので、
 * 下限の1万で足りる。既定のままだと、超えないぶん測っていても通ってしまう。
 */
const BUDGET_CHARS = "10000";

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
const EXIT_INVALID_ARGUMENTS = 2;
const EXIT_INPUT_UNAVAILABLE = 3;

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
                const told = Array.isArray(block.content) ? block.content : [block.content];
                results.set(block.tool_use_id, {
                    // 画像の塊には text が無い。文字へ均すと、画像で届いたのか文字列で届いたのかを
                    // 見分けられなくなる。
                    said: told
                        .filter((c) => c === null || typeof c !== "object" || c.type === "text")
                        .map((c) => (c !== null && typeof c === "object" ? String(c.text) : String(c)))
                        .join(""),
                    images: told.filter(
                        (c) => c !== null && typeof c === "object" && c.type === "image").length,
                });
            }
        }
    }

    return calls.map((call) => ({ ...call, ...(results.get(call.id) ?? {}) }));
}

/** 引数を読み分ける。差し替え点はどれも綴りで受け取り、中身は解さない。 */
function parseArguments(args) {
    const named1 = { "--control": null, "--setup": null, "--client": null };
    for (let at = 0; at < args.length; at += 2) {
        const name = args[at];
        const value = args[at + 1];
        if (value === undefined) {
            return { error: name + " に値がありません。" };
        }

        if (named1[name] === undefined) {
            return { error: "知らない引数: " + name };
        }

        named1[name] = value;
    }

    return { parsed: named1 };
}

const read = parseArguments(process.argv.slice(2));
if (read.error !== undefined) {
    console.error(read.error);
    console.error(
        "使い方: node live-client.mjs [--control <操作役のパス>] [--setup <前置のパス>]"
            + " [--client <呼ばせる相手の綴り>]");
    process.exit(EXIT_INVALID_ARGUMENTS);
}

if (read.parsed["--control"] !== null) {
    CONTROL_SCRIPT = read.parsed["--control"];
}

if (read.parsed["--setup"] !== null) {
    SETUP_SCRIPT = read.parsed["--setup"];
}

if (read.parsed["--client"] !== null) {
    CLIENT_COMMAND = read.parsed["--client"];
}

let editor = null;
let room = null;
let code = EXIT_SUCCESS;

const fell = new Set();

const walked = new Set();
try {
    // 起こす相手はこの環境を継ぐ。エディタの中のホストも、参照クライアントが起こすブリッジも
    // 同じ値を読むので、ここで置けば両方がそろう。
    process.env[BUDGET_NAME] = BUDGET_CHARS;

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

    const naming = CASES.map((c) => named(c.tool));
    const orders = CASES
        .map((c, i) => {
            const given = Object.entries(c.arguments)
                .map(([n, v]) => n + "=" + JSON.stringify(v)).join(", ");

            return (i + 1) + ". " + named(c.tool)
                + (given === "" ? " を引数無しで" : " を " + given + " の引数で") + "1回だけ呼べ。";
        })
        .join("\n");

    // 参照クライアントの入口はWindowsでは .cmd なので、シェルを通さないと起こせない。
    const quoted = (value) => "\"" + value + "\"";
    const said = spawnSync(
        CLIENT_COMMAND,
        [
            "-p",
            "--mcp-config", quoted(config),
            "--strict-mcp-config",
            "--allowedTools", quoted(naming.join(",")),
            "--output-format", "stream-json",
            "--verbose",
        ],
        {
            encoding: "utf8",
            timeout: CLIENT_TIMEOUT_MS,
            shell: true,
            env: { ...process.env, [UNATTENDED_NAME]: "1" },
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
            walked.add(CASES.indexOf(probe));
            const naming1 = named(probe.tool);
            const call = calls.find(
                (c) => c.name === naming1 && carries(c.input, probe.arguments));
            if (call === undefined) {
                const tried = calls
                    .filter((c) => c.name === naming1)
                    .map((c) => JSON.stringify(c.input))
                    .join(" / ");
                fell.add(0);
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
                fell.add(1);
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
                fell.add(2);
                console.error("呼び出しが通りませんでした: " + probe.tool + "\n  " + said1);
                code = EXIT_FAILED;
                continue;
            }

            const drawn = call.images === undefined ? 0 : call.images;
            if (probe.image && drawn !== 1) {
                fell.add(3);
                console.error(
                    "画像が画像として届きませんでした: " + probe.tool
                        + "\n  届いた画像の数: " + drawn
                        + "\n  返り: " + said1.slice(0, 200));
                code = EXIT_FAILED;
                continue;
            }

            if (!probe.image && drawn !== 0) {
                fell.add(4);
                console.error(
                    "画像を返さないツールが画像を返しました: " + probe.tool
                        + "\n  届いた画像の数: " + drawn);
                code = EXIT_FAILED;
                continue;
            }

            console.log(
                "OK " + probe.tool + " " + JSON.stringify(call.input) + " -> " + line
                    + (probe.image ? " + 画像1枚" : ""));
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

const toldPath = process.env.PMX_EDITOR_MCP_FELL_PATH;
if (toldPath) {
    fs.writeFileSync(toldPath, [...fell].join(","), "utf8");
}

const ranTold = process.env.PMX_EDITOR_MCP_RAN_PATH;
if (ranTold) {
    fs.writeFileSync(ranTold, [...walked].join(","), "utf8");
}

process.exit(code);
