// 自動E2E検査の実行器を確かめるための、応答を作って返すだけの待受。
// 実行器が投げた検査の番から、その検査が求める結末を引き、期待どおりの応答か、その結末の形だけを
// 違えた応答を返す。実機のエディタもホストも要らないので、常設の検査から走らせられる。

import fs from "node:fs";
import net from "node:net";
import path from "node:path";
import process from "node:process";

/** 要求と応答の jsonrpc に固定で置く値。 */
const JSONRPC_VERSION = "2.0";

/** 待受のパイプ名の付け方。ホスト側の実装が定める。 */
const PIPE_PREFIX = "pmx-editor-mcp-";

/** ハンドシェイクで返すプロトコル番号。実行器がこの値を求める。 */
const HANDSHAKE_PROTOCOL = 1;

/** 呼び先が無いことを表すJSON-RPCの綴り。実行器はこれだけを呼び先の不在と見る。 */
const UNKNOWN_METHOD = -32601;

/** 検査に並ぶ番から要求の識別子への差。実行器がこの並べ方で投げる。 */
const FIRST_CASE_ID = 2;

/** ハンドシェイクが名乗るセッション。実行器は形だけを見るので、中身は何でもよい。 */
const SESSION = "0123456789abcdef0123456789abcdef";

/** ビューを写した中身の代わり。見比べる相手の代わりが、この値だけを写しと同じと答える。 */
const SAME_VIEW = "うつしたすがた";

/** 写したビューと合わない画像の代わり。 */
const OTHER_VIEW = "ちがうすがた";

/** 書き込んだ置き場を確かめる段を違えるときの名前。結末の名前ではないのでここで名指しする。 */
const BROKEN_FILE = "file";

/** 写しを取れるビューの名前。実行器はこのビューの画像だけを、写しと合うことを求める。 */
const CAPTURED_VIEW = "pmx";

const EXIT_INVALID_ARGUMENTS = 2;

function parseArguments(args) {
    const parsed = { cases: null, pipe: null, broken: "", at: -1 };
    const named = { "--cases": "cases", "--pipe": "pipe", "--broken": "broken" };
    for (let at = 0; at < args.length; at += 2) {
        const name = args[at];
        const value = args[at + 1];
        if (value === undefined) {
            return { error: name + " に値がありません。" };
        }
        if (name === "--at") {
            parsed.at = Number.parseInt(value, 10);
            continue;
        }
        if (named[name] === undefined) {
            return { error: "知らない引数: " + name };
        }

        parsed[named[name]] = value;
    }

    if (parsed.cases === null || parsed.pipe === null || !Number.isInteger(parsed.at)) {
        return { error: "--cases と --pipe は省けません。--at は整数で渡す。" };
    }

    return { parsed };
}

/** 覚えさせるハンドル。借りる側が並びの中を指せるよう、並びで返す。 */
function handed(round) {
    return [round];
}

/** 読み比べる相手が返す一覧。呼び出しの前後で違えるために、何回目かを載せる。 */
function listed(round) {
    return { total: round, items: [{ name: "もの" + round }] };
}

/** 道の各段をたどって着いた先の値。たどれなければ undefined。 */
function along(value, road) {
    let held = value;
    for (const step of road) {
        if (held === null || held === undefined) {
            return undefined;
        }

        held = held[step];
    }

    return held;
}

/**
 * 借りると宣言した値が、手渡したものとして届いているか。届いていなければその旨を返す。差し込む
 * 側を落としても応答が変わらなければ、借りは確かめられていないことになる。
 */
function borrowed(one, params, remembered) {
    for (const [into, from] of Object.entries(one.borrowed ?? {})) {
        const road = from.split("/");
        const given = along(remembered.get(road[0]), road.slice(1));
        if (along(params, into.split("/")) !== given) {
            return "借りた値が渡っていない: " + into;
        }
    }

    return null;
}

/**
 * その検査へ返す応答。違える形を渡すと、その結末を確かめる突き合わせだけが外れる応答になる
 * ——外れ方は結末ごとに違うので、結末ごとに書き分ける。
 */
function answer(one, broken, params, remembered, round) {
    const wrong = broken === one.expect;
    if (one.expect === "refusal") {
        return wrong
            ? { result: { ok: true, value: null } }
            : { result: { ok: false, error: { code: one.code, message: "断った。" } } };
    }

    if (one.expect === "denied") {
        return wrong
            ? { result: { ok: false, error: { code: one.code, message: "別の事情。" } } }
            : { result: { ok: false, error: { code: one.code, message: "断った: " + one.says } } };
    }

    if (one.expect === "reads") {
        const value = wrong ? "別の値" : one.expected.value;

        return { result: { ok: true, value: { items: [{ [one.expected.member]: value }] } } };
    }

    if (one.expect === "changed") {
        return {
            result: {
                ok: true,
                value: wrong ? remembered.get(one.differs) : listed(round),
            },
        };
    }

    if (one.expect === "viewImage") {
        // 写したビューの行は合う画像で、ほかのビューの行は合わない画像で通る。違えるときは
        // その向きを入れ替える。
        const same = (one.view === CAPTURED_VIEW) !== wrong;

        return { result: { ok: true, value: same ? SAME_VIEW : OTHER_VIEW } };
    }

    const missing = borrowed(one, params, remembered);
    if (missing !== null) {
        return { result: { ok: false, error: { code: "TOOL_INVALID_HANDLE", message: missing } } };
    }

    // 成功と、呼び先まで届くことを見る結末は、どちらも成功した応答で通る。
    if (wrong) {
        return {
            result: { ok: false, error: { code: "TOOL_OPERATION_FAILED", message: "断った。" } },
        };
    }

    if (one.writes !== undefined && broken !== BROKEN_FILE) {
        fs.mkdirSync(path.dirname(params[one.writes]), { recursive: true });
        fs.writeFileSync(params[one.writes], "書いた。", "utf8");
    }

    return { result: { ok: true, value: null } };
}

/** その名前で覚えた値を、あとの検査がどう使うか。読み比べる相手なら一覧、そうでなければハンドル。 */
function producing(cases) {
    return new Set(
        cases.filter((one) => one.differs !== undefined).map((one) => one.differs));
}

function serve(cases, parsed) {
    const compared = producing(cases);
    const remembered = new Map();
    let round = 0;
    const reply = (request) => {
        if (request.method === "handshake") {
            return {
                jsonrpc: JSONRPC_VERSION,
                id: request.id,
                result: {
                    protocol: HANDSHAKE_PROTOCOL,
                    hostVersion: "0.0.0-stub",
                    budgetChars: 100000,
                    session: SESSION,
                },
            };
        }

        const one = cases[request.id - FIRST_CASE_ID];
        if (one === undefined) {
            return {
                jsonrpc: JSONRPC_VERSION,
                id: request.id,
                error: { code: UNKNOWN_METHOD, message: "その番の検査は無い。" },
            };
        }

        // 読み比べる相手は、呼び出しの前と後で違うものを返さなければならない。投げられるたびに
        // 数えて、その数を載せる。
        round += 1;
        const broken = request.id - FIRST_CASE_ID === parsed.at ? parsed.broken : "";
        const said = answer(one, broken, request.params ?? {}, remembered, round);
        if (one.produces !== undefined && said.result !== undefined && said.result.ok === true) {
            said.result.value = compared.has(one.produces) ? listed(round) : handed(round);
            remembered.set(one.produces, said.result.value);
        }

        return { jsonrpc: JSONRPC_VERSION, id: request.id, ...said };
    };

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
                    socket.write(JSON.stringify(reply(JSON.parse(line))) + "\n");
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

serve(JSON.parse(fs.readFileSync(read.parsed.cases, "utf8")).cases, read.parsed);
