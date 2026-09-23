// 自動E2E検査の実行器を確かめるための、MCPサーバーとして応答を作って返すだけの相手。
// 実行器が投げたツールの呼び出しの順から、その検査が求める結末を引き、期待どおりの応答か、その
// 結末の形だけを違えた応答を返す。実機のエディタもホストもブリッジも要らないので、常設の検査から
// 走らせられる。包みからMCPの結果への写し方はブリッジの実装が定めるので、ここはそれに合わせる。

import fs from "node:fs";
import process from "node:process";

/** 要求と応答の jsonrpc に固定で置く値。 */
const JSONRPC_VERSION = "2.0";

/** MCPのバージョン。実行器が名乗るバージョンに合わせる。 */
const MCP_PROTOCOL_VERSION = "2025-06-18";

/** 呼び先が無いことを表すJSON-RPCの綴り。 */
const UNKNOWN_METHOD = -32601;

/** ブリッジが本文の先頭へ置く、接続先の名乗り。実行器はこの書き出しで名乗りを見分ける。 */
const TARGET_NOTICE = "接続先: 代わりの待受";

/** ブリッジが本文の末尾へ足す警告の行の頭。 */
const WARNING_PREFIX = "警告: ";

/**
 * ビューを写した中身の書き出し。これに行が名乗るビューの名を続けたものが、そのビューの写しと
 * 同じ中身になる。操作役の代わりが同じ綴りで写しを書く。
 */
const SAME_VIEW = "うつしたすがた";

/** 写したビューと合わない画像の代わり。 */
const OTHER_VIEW = "ちがうすがた";

/** 書き込んだ置き場を確かめる段を違えるときの名前。結末の名前ではないのでここで名指しする。 */
const BROKEN_FILE = "file";

/** 違える形のうち、呼び先まで届く段を確認の表示で止めるもの。 */
const BROKEN_PROMPT = "prompt";

/** 違える形のうち、公開するツールの名前を1つ欠けさせるもの。 */
const BROKEN_NAMES = "names";

/** 違える形のうち、中継を作れなかった行を残すもの。 */
const BROKEN_UNRESOLVED = "unresolved";

/** 違える形のうち、呼び出しの失敗で無効にした行を残すもの。 */
const BROKEN_DISABLED = "disabled";

/** 違える形のうち、ブリッジ自身の誤りを返すもの。この誤りは接続先を名乗らない。 */
const BROKEN_NOTICE = "notice";

/** ブリッジ自身の誤りの綴り。名乗る接続先が無いので、本文はこの1行だけになる。 */
const BRIDGE_FAILURE = "BRIDGE_TIMEOUT: ホストの応答が時間内に返らない。";

/** 包みへ足す警告。結末を変えないので、どの検査へ足しても判定は動かない。 */
const WARNING = "確かめのための警告。";

/** 確認の表示が出て止まったことを知らせる断りの綴り。ホストの包みが定める。 */
const PROMPT_SHOWN = "TOOL_PROMPT_SHOWN";

/** ホストが受け持ち、ブリッジが固定のツールとして公開する名前。 */
const FIXED_TOOLS = ["ping", "sdk_status"];

/** 画像を返す結末。ブリッジはこの結末の値を本文でなく画像の塊で返す。 */
const IMAGE_EXPECT = "viewImage";

const EXIT_INVALID_ARGUMENTS = 2;

function parseArguments(args) {
    const parsed = { cases: null, at: -1, broken: "" };
    for (let at = 0; at < args.length; at += 1) {
        const name = args[at];
        const value = args[at + 1];
        if (name === "--cases" && value !== undefined) {
            parsed.cases = value;
            at += 1;
        } else if (name === "--at" && value !== undefined) {
            parsed.at = Number.parseInt(value, 10);
            at += 1;
        } else if (name === "--broken" && value !== undefined) {
            parsed.broken = value;
            at += 1;
        } else {
            return { error: "知らない引数です: " + name };
        }
    }

    return parsed.cases === null ? { error: "--cases が要ります。" } : { parsed };
}

/** 覚えさせるハンドル。借りる側が並びの中を指せるよう、並びで返す。 */
function handed(round) {
    return [round * 10, round * 10 + 1];
}

/** 読み比べる相手が返す一覧。呼び出しの前後で違えるために、何回目かを載せる。 */
function listed(round) {
    return { items: [{ count: round }] };
}

/** 道の各段をたどって着いた先の値。たどれなければ undefined。 */
function along(value, road) {
    let held = value;
    for (const step of road) {
        if (held === null || held === undefined) {
            return undefined;
        }

        held = Array.isArray(held) ? held[Number.parseInt(step, 10)] : held[step];
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
 * その検査へ返す包み。違える形を渡すと、その結末を確かめる突き合わせだけが外れる包みになる
 * ——外れ方は結末ごとに違うので、結末ごとに書き分ける。
 */
function answer(one, broken, params, remembered, round) {
    const wrong = broken === one.expect;
    if (one.expect === "called" && broken === BROKEN_PROMPT) {
        return { ok: false, error: { code: PROMPT_SHOWN, message: "確認の表示が出た。" } };
    }

    if (one.expect === "refusal") {
        return wrong
            ? { ok: true, value: null }
            : { ok: false, error: { code: one.code, message: "断った。" } };
    }

    if (one.expect === "denied") {
        return wrong
            ? { ok: false, error: { code: one.code, message: "別の事情。" } }
            : { ok: false, error: { code: one.code, message: "断った: " + one.says } };
    }

    if (one.expect === "reads") {
        const value = wrong ? "別の値" : one.expected.value;

        return { ok: true, value: { items: [{ [one.expected.member]: value }] } };
    }

    if (one.expect === "changed") {
        return { ok: true, value: wrong ? remembered.get(one.differs) : listed(round) };
    }

    if (one.expect === IMAGE_EXPECT) {
        // どの行も、自分のビューの写しと合う画像で通る。写しにはビューの名が混ざるので、行と
        // 写しの取り合わせが入れ替われば合わない。違えるときは合わない画像を返す。
        return { ok: true, value: wrong ? OTHER_VIEW : SAME_VIEW + " " + one.view };
    }

    const missing = borrowed(one, params, remembered);
    if (missing !== null) {
        return { ok: false, error: { code: "TOOL_INVALID_HANDLE", message: missing } };
    }

    // 成功と、呼び先まで届くことを見る結末は、どちらも成功した包みで通る。
    if (wrong) {
        return { ok: false, error: { code: "TOOL_OPERATION_FAILED", message: "断った。" } };
    }

    if (one.writes !== undefined && broken !== BROKEN_FILE) {
        fs.mkdirSync(params[one.writes].replace(/[\\/][^\\/]*$/, ""), { recursive: true });
        fs.writeFileSync(params[one.writes], "書いた。", "utf8");
    }

    return { ok: true, value: null };
}

/** その名前で覚えた値を、あとの検査がどう使うか。読み比べる相手なら一覧、そうでなければハンドル。 */
function producing(cases) {
    return new Set(cases.filter((one) => one.differs !== undefined).map((one) => one.differs));
}

/**
 * 包みをMCPのツールの結果へ写す。成功なら値をJSONの表記で、失敗なら「コード: メッセージ」を本文に
 * し、先頭へ接続先の名乗りを置く。画像を返す結末だけは、値を本文でなく画像の塊で返す。包みが警告を
 * 持つときは本文の末尾へ行として足す。
 */
function toolResult(envelope, drawn, warned) {
    let body = envelope.ok === true
        ? (drawn ? "" : JSON.stringify(envelope.value === undefined ? null : envelope.value))
        : envelope.error.code + ": " + envelope.error.message;
    if (warned) {
        body += (body.length === 0 ? "" : "\n") + WARNING_PREFIX + WARNING;
    }

    const content = [{
        type: "text",
        text: body.length === 0 ? TARGET_NOTICE : TARGET_NOTICE + "\n" + body,
    }];
    if (drawn && envelope.ok === true) {
        content.push({ type: "image", data: envelope.value, mimeType: "image/png" });
    }

    return { isError: envelope.ok !== true, content };
}

/** 公開するツールの名前。検査が呼ぶ名前と、ホストが受け持つ固定のツールからなる。 */
function published(cases, broken) {
    const named = [];
    for (const one of cases) {
        if (!named.includes(one.tool)) {
            named.push(one.tool);
        }
    }

    const kept = broken === BROKEN_NAMES ? named.slice(1) : named;

    return [...kept, ...FIXED_TOOLS].sort();
}

/** 中継の状態。違える形を渡すと、空であるはずの並びに行が残る。 */
function status(cases, broken) {
    return {
        runningSdkVersion: "0.0.0-stub",
        generatedSdkVersion: "0.0.0-stub",
        unresolvedRows: broken === BROKEN_UNRESOLVED ? ["Sdk.Type.Absent()"] : [],
        disabledRows: broken === BROKEN_DISABLED ? ["Sdk.Type.Lost()"] : [],
        toolNames: published(cases, broken).filter((name) => !FIXED_TOOLS.includes(name)),
    };
}

function serve(cases, parsed) {
    const compared = producing(cases);
    const remembered = new Map();
    let round = 0;
    let called = -1;

    const reply = (request) => {
        if (request.method === "initialize") {
            return {
                protocolVersion: MCP_PROTOCOL_VERSION,
                capabilities: { tools: {} },
                serverInfo: { name: "e2e-stub-bridge", version: "1" },
            };
        }

        if (request.method === "tools/list") {
            return {
                tools: published(cases, parsed.broken).map((name) => ({
                    name,
                    description: name + " の代わり。",
                    inputSchema: { type: "object" },
                })),
            };
        }

        if (request.method !== "tools/call") {
            return { error: { code: UNKNOWN_METHOD, message: "その呼び先は無い。" } };
        }

        const name = request.params?.name;
        const given = request.params?.arguments ?? {};
        if (name === "sdk_status") {
            return {
                isError: false,
                content: [{
                    type: "text",
                    text: TARGET_NOTICE + "\n" + JSON.stringify(status(cases, parsed.broken)),
                }],
            };
        }

        if (name === "ping") {
            return { isError: false, content: [{ type: "text", text: TARGET_NOTICE + "\npong" }] };
        }

        called += 1;
        const one = cases[called];
        if (one === undefined) {
            return { error: { code: UNKNOWN_METHOD, message: "その番の検査は無い。" } };
        }

        // ブリッジ自身の誤りは、名乗る接続先を持たないので本文が1行だけになる。実行器が名乗りを
        // 1行目と決め打っていると、この形の誤りは中身ごと消える。
        if (called === parsed.at && parsed.broken === BROKEN_NOTICE) {
            return { isError: true, content: [{ type: "text", text: BRIDGE_FAILURE }] };
        }

        // 読み比べる相手は、呼び出しの前と後で違うものを返さなければならない。投げられるたびに
        // 数えて、その数を載せる。
        round += 1;
        const broken = called === parsed.at ? parsed.broken : "";
        const envelope = answer(one, broken, given, remembered, round);
        if (one.produces !== undefined && envelope.ok === true) {
            envelope.value = compared.has(one.produces) ? listed(round) : handed(round);
            remembered.set(one.produces, envelope.value);
        }

        // 警告は1件目へ足して、警告の行を剥がす段が通しの実行で必ず走るようにする。
        return toolResult(envelope, one.expect === IMAGE_EXPECT, called === 0);
    };

    let buffer = "";
    process.stdin.setEncoding("utf8");
    process.stdin.on("data", (chunk) => {
        buffer += chunk;
        for (;;) {
            const at = buffer.indexOf("\n");
            if (at < 0) {
                return;
            }

            const line = buffer.slice(0, at).trim();
            buffer = buffer.slice(at + 1);
            if (line === "") {
                continue;
            }

            const request = JSON.parse(line);

            // 知らせには応答を返さない。返すと、識別子を持たない応答として読み捨てられるだけか、
            // 別の要求の応答と取り違えられる。
            if (request.id === undefined) {
                continue;
            }

            const said = reply(request);
            const response = said.error === undefined
                ? { jsonrpc: JSONRPC_VERSION, id: request.id, result: said }
                : { jsonrpc: JSONRPC_VERSION, id: request.id, error: said.error };
            process.stdout.write(JSON.stringify(response) + "\n");
        }
    });
}

const read = parseArguments(process.argv.slice(2));
if (read.error !== undefined) {
    console.error(read.error);
    process.exit(EXIT_INVALID_ARGUMENTS);
}

serve(JSON.parse(fs.readFileSync(read.parsed.cases, "utf8")).cases, read.parsed);
