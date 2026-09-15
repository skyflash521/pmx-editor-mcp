// ブリッジの実機動作確認を確かめるための、応答を作って返すだけのMCPサーバー。
// 実行器が投げる ping の順に、その回で求められる本文か、その本文の1か所だけを違えた本文を返す。
// 実機のエディタもブリッジも要らないので、常設の検査から走らせられる。
//
// 何回目の ping かで返すものが決まるのは、実行器の操作列が固定だからである。操作役の代わりが
// 起こすエディタのプロセスIDも、同じ並びから決まる。

import process from "node:process";

/** 要求と応答の jsonrpc に固定で置く値。 */
const JSONRPC_VERSION = "2.0";

/** 名乗るプロトコルの版。実行器が使うクライアントはこの値を確かめない。 */
const PROTOCOL_VERSION = "2025-06-18";

/** 待受のパイプ名の付け方。ホスト側の実装が定める。 */
const PIPE_PREFIX = "pmx-editor-mcp-";

/** 接続先を名乗る行の書き出し。ブリッジの実装が定める。 */
const TARGET_PREFIX = "接続先: ";

/** 接続先が移ったことを知らせる行の書き出し。ブリッジの実装が定める。 */
const TARGET_CHANGED_PREFIX = "接続先が変わった: ";

/** ホストが応答したときに返る本文。ホスト側の実装が定める。 */
const PONG = "pong";

/** エラーの本文が、コードと説明の間に置く区切り。ブリッジの実装が定める。 */
const ERROR_SEPARATOR = ": ";

const EXIT_INVALID_ARGUMENTS = 2;

function parseArguments(args) {
    const parsed = { firstEditor: 0, broken: "" };
    const named = { "--first-editor": "firstEditor", "--broken": "broken" };
    for (let at = 0; at < args.length; at += 2) {
        const name = args[at];
        const value = args[at + 1];
        if (value === undefined) {
            return { error: name + " に値がありません。" };
        }

        if (named[name] === undefined) {
            return { error: "知らない引数: " + name };
        }

        parsed[named[name]] = name === "--first-editor" ? Number(value) : value;
    }

    if (!Number.isInteger(parsed.firstEditor) || parsed.firstEditor <= 0) {
        return { error: "--first-editor には起こす1つ目のプロセスIDが要ります。" };
    }

    return { parsed };
}

function pipeNameOf(editor) {
    return PIPE_PREFIX + editor;
}

/** 繋がっている相手を名乗る本文。 */
function pong(editor, broken) {
    const named = pipeNameOf(broken === "target" ? editor + 100 : editor);

    return TARGET_PREFIX + named + "\n" + (broken === "pong" ? "ぽん" : PONG);
}

/** 相手が移ったことを知らせる本文。 */
function moved(from, to, broken) {
    if (broken === "moved") {
        return pong(to, "");
    }

    return TARGET_CHANGED_PREFIX + pipeNameOf(from) + " から " + pipeNameOf(to) + " へ。\n"
        + (broken === "pong" ? "ぽん" : PONG);
}

/** ブリッジが断るときの本文。 */
function refused(code, broken) {
    return (broken === "code" ? "BRIDGE_違う理由" : code) + ERROR_SEPARATOR + "作った断りである。";
}

/**
 * 待ち受ける相手が2つ並ぶ本文。並びはプロセスIDの昇順で、実行器は両方が挙がることと、その順の
 * 両方を確かめる。
 */
function many(editors, broken) {
    const named = [...editors].sort((a, b) => a - b).map(pipeNameOf);
    const listed = broken === "order" ? [...named].reverse() : named;
    const some = broken === "listed" ? listed.slice(0, 1) : listed;

    return refused("BRIDGE_MULTIPLE_HOSTS", "") + "\n" + some.join("\n");
}

/**
 * 何回目の ping かで返す本文。実行器の操作列と同じ順に並べる——操作列が変われば、この並びも
 * 一緒に変える。違える形は1回の実行で1つだけ効かせ、その形が現れる最初の回へ当てる。
 */
function answers(firstEditor, broken) {
    const one = firstEditor;
    const two = firstEditor + 1;
    const three = firstEditor + 2;

    return [
        refused("BRIDGE_NO_EDITOR", broken),
        pong(one, broken),
        refused("BRIDGE_CONNECTION_LOST", ""),
        refused("BRIDGE_NO_HOST", ""),
        pong(one, ""),
        refused("BRIDGE_CONNECTION_LOST", ""),
        many([one, two], broken),
        moved(one, two, broken),
        refused("BRIDGE_CONNECTION_LOST", ""),
        moved(two, three, ""),
    ];
}

function serve(parsed) {
    const said = answers(parsed.firstEditor, parsed.broken);
    let asked = 0;
    let buffer = "";

    const reply = (request) => {
        if (request.method === "initialize") {
            return {
                result: {
                    protocolVersion: PROTOCOL_VERSION,
                    capabilities: { tools: {} },
                    serverInfo: { name: "live-bridge-stub", version: "0.0.0" },
                },
            };
        }

        if (request.method === "tools/call") {
            const text = said[asked] ?? refused("BRIDGE_NO_EDITOR", "");
            asked += 1;

            return { result: { content: [{ type: "text", text }] } };
        }

        return { result: {} };
    };

    process.stdin.setEncoding("utf8");
    process.stdin.on("data", (chunk) => {
        buffer += chunk;
        for (;;) {
            const at = buffer.indexOf("\n");
            if (at < 0) {
                return;
            }

            const line = buffer.slice(0, at);
            buffer = buffer.slice(at + 1);
            if (line.trim() === "") {
                continue;
            }

            const request = JSON.parse(line);
            if (request.id === undefined) {
                continue;
            }

            process.stdout.write(
                JSON.stringify({ jsonrpc: JSONRPC_VERSION, id: request.id, ...reply(request) })
                    + "\n");
        }
    });
}

const read = parseArguments(process.argv.slice(2));
if (read.error !== undefined) {
    console.error(read.error);
    process.exit(EXIT_INVALID_ARGUMENTS);
}

serve(read.parsed);
