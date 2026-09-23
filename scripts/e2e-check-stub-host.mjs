// 実機動作確認の確認クライアントを確かめるための、応答を作って返すだけの待受。
// 確認クライアントが要求を省いたときに送る2件——ハンドシェイクと ping——へ、契約どおりの応答か、
// その中の1か所だけを違えた応答を返す。実機のエディタもホストも要らないので、常設の検査から
// 走らせられる。

import net from "node:net";
import path from "node:path";
import process from "node:process";

/** 要求と応答の jsonrpc に固定で置く値。 */
const JSONRPC_VERSION = "2.0";

/** 待受のパイプ名の付け方。ホスト側の実装が定める。 */
const PIPE_PREFIX = "pmx-editor-mcp-";

/** ハンドシェイクで名乗るプロトコル番号。確認クライアントがこの値を求める。 */
const HANDSHAKE_PROTOCOL = 1;

/** ハンドシェイクで名乗る予算。確認クライアントが既定の値として求める。 */
const BUDGET_CHARS = 100000;

/** ハンドシェイクが名乗るセッション。確認クライアントは16進32文字であることを求める。 */
const SESSION = "0123456789abcdef0123456789abcdef";

/** ハンドシェイクが名乗るホストのバージョン。確認クライアントは空でないことだけを求める。 */
const HOST_VERSION = "0.0.0-stub";

/** ping が返す値。確認クライアントがこの値を求める。 */
const PONG = "pong";

/** 作った断り。切断を伴わないコードを選ぶ——切断は別の結末として見られる。 */
const REFUSED = { code: -32000, message: "作った断りである。" };

/**
 * 1件の応答が収まるバイト数の上限。確認クライアントがこの値で拒む。超える行を作るのに使う。
 */
const MAX_RESPONSE_BYTES = 16 * 1024 * 1024;

const EXIT_INVALID_ARGUMENTS = 2;

function parseArguments(args) {
    const parsed = { pipe: null, broken: "" };
    const named = { "--pipe": "pipe", "--broken": "broken" };
    for (let at = 0; at < args.length; at += 2) {
        const name = args[at];
        const value = args[at + 1];
        if (value === undefined) {
            return { error: name + " に値がありません。" };
        }

        if (named[name] === undefined) {
            return { error: "知らない引数: " + name };
        }

        parsed[named[name]] = value;
    }

    if (parsed.pipe === null) {
        return { error: "--pipe は省けません。" };
    }

    return { parsed };
}

/**
 * ハンドシェイクの応答。違える形は、確認クライアントが見る項目ごとに1つずつ置く——項目を1つ
 * 見落とした確認クライアントは、その形を違えた実行で通ってしまう。
 */
function handshake(broken) {
    if (broken === "handshake.error") {
        return { error: REFUSED };
    }

    if (broken === "handshake.result") {
        return { result: [] };
    }

    return {
        result: {
            protocol: broken === "handshake.protocol" ? HANDSHAKE_PROTOCOL + 1 : HANDSHAKE_PROTOCOL,
            hostVersion: broken === "handshake.hostVersion" ? "" : HOST_VERSION,
            budgetChars: broken === "handshake.budgetChars" ? BUDGET_CHARS + 1 : BUDGET_CHARS,
            session: broken === "handshake.session" ? "みじかいなまえ" : SESSION,
        },
    };
}

/** ping の応答。 */
function ping(broken) {
    if (broken === "ping.error") {
        return { error: REFUSED };
    }

    return { result: broken === "ping.value" ? "ぽん" : PONG };
}

/**
 * 返す1行。応答の中身より手前——行の符号化・JSONの形・jsonrpc・識別子・result と error の
 * 排他——を違える形は、組み立てた行そのものへ手を入れる。確認クライアントはこの層も見ており、
 * 見落とせば契約に合わない応答を通してしまう。
 */
function written(request, broken) {
    const said = {
        jsonrpc: broken === "wire.jsonrpc" ? "1.0" : JSONRPC_VERSION,
        id: request.id,
        ...(request.method === "handshake" ? handshake(broken) : ping(broken)),
    };

    if (broken === "wire.id.missing") {
        delete said.id;
    }

    if (broken === "wire.id.other") {
        said.id = request.id + 1;
    }

    if (broken === "wire.unidentified") {
        said.id = null;
    }

    if (broken === "wire.both") {
        said.error = REFUSED;
    }

    if (broken === "wire.error.shape" || broken === "wire.error.code"
        || broken === "wire.error.message") {
        delete said.result;
        said.error = broken === "wire.error.shape"
            ? "だめだった"
            : (broken === "wire.error.code"
                ? { message: REFUSED.message }
                : { code: REFUSED.code });
    }

    if (broken === "wire.utf8") {
        // UTF-8として解けないバイトの並び。文字列では作れないので塊で返す。
        return Buffer.from([0x7b, 0xff, 0x7d]);
    }

    if (broken === "wire.json") {
        return "これはJSONではない。";
    }

    if (broken === "wire.object") {
        return "123";
    }

    if (broken === "wire.oversize") {
        return "\"" + "x".repeat(MAX_RESPONSE_BYTES + 1) + "\"";
    }

    const text = JSON.stringify(said);
    if (broken === "wire.bom") {
        return "\ufeff" + text;
    }

    if (broken === "wire.cr") {
        return text + "\r";
    }

    return text;
}

function serve(parsed) {
    net.createServer((socket) => {
        let buffer = "";
        socket.on("data", (chunk) => {
            buffer += chunk.toString("utf8");
            for (;;) {
                const at = buffer.indexOf("\n");
                if (at < 0) {
                    return;
                }

                const line = buffer.slice(0, at);
                buffer = buffer.slice(at + 1);
                if (line.trim() !== "") {
                    const said = written(JSON.parse(line), parsed.broken);
                    socket.write(Buffer.isBuffer(said)
                        ? Buffer.concat([said, Buffer.from("\n")])
                        : said + "\n");
                }
            }
        });
    }).on("error", (error) => {
        console.error("待受を開けません: " + error.message);
        process.exit(EXIT_INVALID_ARGUMENTS);
    }).listen(path.join("\\\\.\\pipe\\", PIPE_PREFIX + parsed.pipe));
}

const read = parseArguments(process.argv.slice(2));
if (read.error !== undefined) {
    console.error(read.error);
    process.exit(EXIT_INVALID_ARGUMENTS);
}

serve(read.parsed);
