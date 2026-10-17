// MCPの確認クライアントを確かめるための、応答を作って返すだけのMCPサーバー。
// 確認クライアントが送る要求——初期化・ping の呼び出し・ツールの一覧——へ、契約どおりの応答を
// 返すか、1か所だけを違えて返す。

import process from "node:process";
import readline from "node:readline";

const JSONRPC_VERSION = "2.0";

const TOOLS = ["ping", "model_stub"];

/** 公開するツールの系統に触れる。 */
const INSTRUCTIONS = "作ったサーバーである。model_ は作ったツールを受け持つ。";

const PONG = "接続先: pmx-editor-mcp-1\npong";

/** 違え方の名前と、その違え方で ping の応答より前に標準出力へ混ぜる行。 */
const NOISES = {
    "stdout.nonjson": "作った診断の行である。",
    "stdout.blank": "",
};

const BROKEN = Object.keys(NOISES);

const EXIT_INVALID_ARGUMENTS = 2;

function parseArguments(args) {
    let broken = "";
    for (let at = 0; at < args.length; at++) {
        if (args[at] !== "--broken") {
            continue;
        }

        broken = args[at + 1] ?? "";
        if (broken !== "" && !BROKEN.includes(broken)) {
            return { error: "知らない違え方: " + broken };
        }
    }

    return { broken };
}

function reply(id, result) {
    process.stdout.write(JSON.stringify({ jsonrpc: JSONRPC_VERSION, id, result }) + "\n");
}

const read = parseArguments(process.argv.slice(2));
if (read.error !== undefined) {
    console.error(read.error);
    process.exit(EXIT_INVALID_ARGUMENTS);
}

const lines = readline.createInterface({ input: process.stdin });
lines.on("line", (line) => {
    if (line.trim() === "") {
        return;
    }

    const request = JSON.parse(line);
    if (request.id === undefined) {
        return;
    }

    if (request.method === "initialize") {
        reply(request.id, {
            protocolVersion: request.params.protocolVersion,
            capabilities: { tools: {} },
            serverInfo: { name: "pmx-editor-mcp", version: "0.0.0-stub" },
            instructions: INSTRUCTIONS,
        });
        return;
    }

    if (request.method === "tools/call") {
        if (read.broken !== "") {
            process.stdout.write(NOISES[read.broken] + "\n");
        }

        reply(request.id, { content: [{ type: "text", text: PONG }] });
        return;
    }

    if (request.method === "tools/list") {
        reply(request.id, {
            tools: TOOLS.map((name) => ({
                name,
                description: "作ったツールである。",
                inputSchema: { type: "object", properties: {} },
            })),
        });
        return;
    }

    process.stdout.write(JSON.stringify({
        jsonrpc: JSONRPC_VERSION,
        id: request.id,
        error: { code: -32601, message: "知らない要求: " + request.method },
    }) + "\n");
});
lines.on("close", () => process.exit(0));
