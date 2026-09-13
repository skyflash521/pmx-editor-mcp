// 受入の実行器を確かめるための、応答を作って返すだけのMCPサーバー。
// 定義が書いた段の順に呼ばれているかを見て、期待どおりの応答と、期待と違う応答を選んで返す。
// 実機のエディタもブリッジも要らないので、常設の検査から走らせられる。
// 起動したことにするエディタの番号と、写したことにするビューの大きさは前置から受け取る
// ——同じ値を操作役の相手も使うので、決めるのは1か所にする。

import fs from "node:fs";
import path from "node:path";
import process from "node:process";
import zlib from "node:zlib";

const JSONRPC_VERSION = "2.0";

const MCP_PROTOCOL_VERSION = "2025-06-18";

/** 接続先を名乗る行の書き出し。ブリッジの実装が定める。 */
const TARGET_PREFIX = "接続先: ";

/** 接続先が移ったことを知らせる行の書き出しと結び。ブリッジの実装が定める。 */
const TARGET_CHANGED_PREFIX = "接続先が変わった: ";
const TARGET_CHANGED_MIDDLE = " から ";
const TARGET_CHANGED_SUFFIX = " へ。以前の応答は別のエディタのものである。";

/** 待受のパイプ名の付け方。ホスト側の実装が定める。 */
const PIPE_PREFIX = "pmx-editor-mcp-";

const EXIT_INVALID_ARGUMENTS = 2;

function parseArguments(args) {
    const parsed = {
        cases: null, broken: null, at: 0, firstEditor: null, view: null, progress: null,
    };
    const named = {
        "--cases": "cases", "--broken": "broken", "--progress": "progress", "--view": "view",
    };
    const counted = { "--at": "at", "--first-editor": "firstEditor" };
    for (let at = 0; at < args.length; at += 2) {
        const name = args[at];
        const value = args[at + 1];
        if (value === undefined) {
            return { error: name + " に値がありません。" };
        }
        if (named[name] !== undefined) {
            parsed[named[name]] = value;
            continue;
        }
        if (counted[name] !== undefined) {
            parsed[counted[name]] = Number.parseInt(value, 10);
            continue;
        }

        return { error: "知らない引数: " + name };
    }

    if (parsed.cases === null || parsed.broken === null
        || !Number.isInteger(parsed.firstEditor) || parsed.view === null
        || parsed.progress === null) {
        return { error: "--cases・--broken・--first-editor・--view・--progress は省けません。" };
    }

    if (parsed.broken === "") {
        parsed.broken = null;
    }

    const measured = /^(\d+)x(\d+)$/.exec(parsed.view);
    if (measured === null) {
        return { error: "--view は幅x高さでなければなりません: " + parsed.view };
    }

    parsed.view = {
        width: Number.parseInt(measured[1], 10),
        height: Number.parseInt(measured[2], 10),
    };

    return { parsed };
}

/** 道をたどった先へ値を置く。途中の組は作りながら進む。 */
function put(into, dotted, value) {
    const steps = dotted.split(".");
    let held = into;
    for (let at = 0; at < steps.length - 1; at++) {
        const step = steps[at];
        const next = steps[at + 1];
        if (held[step] === undefined) {
            held[step] = /^\d+$/.test(next) ? [] : {};
        }
        held = held[step];
    }

    held[steps[steps.length - 1]] = value;
}

/** 期待された値と違う値。数は足し、文字は継ぎ足し、並びと真偽は別のものにする。 */
function differ(value) {
    if (typeof value === "number") {
        return value + 1;
    }
    if (typeof value === "string") {
        return value + "-違う";
    }
    if (typeof value === "boolean") {
        return !value;
    }
    if (Array.isArray(value)) {
        return value.length === 0 ? [0] : [];
    }

    return null;
}

/** 覚えた値を差し込む。実行器と同じ差し込み方をしないと、引数を突き合わせられない。 */
function fill(node, remembered) {
    if (Array.isArray(node)) {
        return node.map((item) => fill(item, remembered));
    }

    if (typeof node === "string") {
        return node.replace(/%([^%]+)%/g, (whole, name) => process.env[name] ?? whole);
    }

    if (node === null || typeof node !== "object") {
        return node;
    }

    if (typeof node.$from === "string") {
        return remembered.get(node.$from);
    }

    const filled = {};
    for (const [name, value] of Object.entries(node)) {
        filled[name] = fill(value, remembered);
    }

    return filled;
}

/** 指定した大きさのPNGを詰めた文字列。中身は一色で、見るのは大きさだけである。 */
function png(width, height) {
    const raw = Buffer.alloc((width * 3 + 1) * height);
    const chunk = (kind, body) => {
        const named = Buffer.concat([Buffer.from(kind, "ascii"), body]);
        const length = Buffer.alloc(4);
        length.writeUInt32BE(body.length);
        const sum = Buffer.alloc(4);
        sum.writeUInt32BE(zlib.crc32(named));

        return Buffer.concat([length, named, sum]);
    };

    const header = Buffer.alloc(13);
    header.writeUInt32BE(width, 0);
    header.writeUInt32BE(height, 4);
    header[8] = 8;
    header[9] = 2;

    return Buffer.concat([
        Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
        chunk("IHDR", header),
        chunk("IDAT", zlib.deflateSync(raw)),
        chunk("IEND", Buffer.alloc(0)),
    ]).toString("base64");
}

/**
 * 接続先の知らせの行。期待が相手を述べていれば、その相手を名乗る。違えるときは別の相手を
 * 名乗る——知らせだけが期待から外れるので、知らせを突き合わせている実行器だけが落ちる。
 */
function notice(expect, remembered, firstEditor, broken) {
    const shift = broken ? 1 : 0;
    const wanted = expect.notice === undefined
        ? undefined
        : fill(expect.notice, remembered);
    if (wanted === undefined || wanted.editor !== undefined) {
        return TARGET_PREFIX + PIPE_PREFIX + ((wanted?.editor ?? firstEditor) + shift);
    }

    return TARGET_CHANGED_PREFIX + PIPE_PREFIX + (wanted.changedFrom + shift)
        + TARGET_CHANGED_MIDDLE + PIPE_PREFIX + (wanted.changedTo + shift)
        + TARGET_CHANGED_SUFFIX;
}

/**
 * 段が書いた期待から、それを満たす本文を作る。覚えさせる段には controlled の値を載せ、絵と
 * イベントは形だけを作る。作れない期待なら投げる。
 */
function compose(step, broken, recorded, options, remembered) {
    const expect = step.expect;
    if (expect.body !== undefined) {
        return broken === "body" ? differ(expect.body.equals) : expect.body.equals;
    }

    if (expect.code !== undefined) {
        return (broken === "code" ? "TOOL_違う理由" : expect.code) + ": 作った応答である。";
    }

    if (expect.image !== undefined) {
        // 違えるときは大きさを変える——写した実寸と合わないので、突き合わせている実行器だけが落ちる。
        return JSON.stringify(png(
            options.view.width + (broken === "image" ? 1 : 0), options.view.height));
    }

    let value = null;
    if (expect.values !== undefined) {
        value = {};
        for (const wanted of expect.values) {
            put(value, wanted.path, broken === "values" ? differ(wanted.equals) : wanted.equals);
        }
    }

    if (expect.events !== undefined) {
        const wanted = fill(expect.events, remembered);
        value = value ?? {};
        // 違えるときは並びを逆にする——同じものが揃っていても順が違えば期待を満たさない。
        const told = broken === "events" ? [...wanted.types].reverse() : wanted.types;
        value.events = told.map((type, at) => ({
            seq: at + 1, type, sourceHandle: wanted.sourceHandle, payload: {},
        }));
        value.dropped = 0;
        value.remaining = 0;
    }

    if (step.record === undefined) {
        if (value === null && expect.ok !== true) {
            throw new Error("この作りでは応答を作れない期待である: " + JSON.stringify(expect));
        }

        return JSON.stringify(value);
    }

    if (step.record.path === "") {
        return JSON.stringify(recorded);
    }

    value = value ?? {};
    put(value, step.record.path, recorded);

    return JSON.stringify(value);
}

/** 段が呼ばれる順と引数が定義どおりか。合っていれば null。 */
function mismatch(step, name, args, remembered) {
    if (step === undefined) {
        return "定義に無い呼び出しである: " + name;
    }
    if (step.kind !== "tool") {
        return "ツールを呼ぶ段ではない。定義は " + step.kind + " の段で、呼ばれたのは " + name;
    }
    if (step.tool !== name) {
        return "呼ぶ順が定義と違う。定義は " + step.tool + " で、呼ばれたのは " + name;
    }

    const expected = fill(step.arguments, remembered);
    if (JSON.stringify(expected) !== JSON.stringify(args)) {
        return "引数が定義と違う。定義は " + JSON.stringify(expected)
            + " で、渡されたのは " + JSON.stringify(args);
    }

    return null;
}

const { parsed, error } = parseArguments(process.argv.slice(2));
if (error !== undefined) {
    process.stderr.write(error + "\n");
    process.exit(EXIT_INVALID_ARGUMENTS);
}

const read = JSON.parse(fs.readFileSync(parsed.cases, "utf8"));

// 実行器はシナリオを順に通し、覚えた値をまたいで持ち越すので、こちらも1つの並びとして扱う。
const steps = read.scenarios.flatMap((one) => one.steps);

// 進み具合は持ち越す。サーバーを起こし直す段があるので、この手続きの中だけでは続かない。
// 起こされた回数も数える——起こし直す段をこなしたかどうかは、これでしか外から分からない。
const held = fs.existsSync(parsed.progress)
    ? JSON.parse(fs.readFileSync(parsed.progress, "utf8"))
    : { pos: 0, calls: 0, launched: 0, starts: 0, remembered: {} };
let pos = held.pos;
let calls = held.calls;
let launched = held.launched;
const starts = held.starts + 1;
const remembered = new Map(Object.entries(held.remembered));
let buffer = "";

function keep() {
    fs.writeFileSync(
        parsed.progress,
        JSON.stringify({
            pos, calls, launched, starts, remembered: Object.fromEntries(remembered),
        }),
        "utf8");
}

keep();

/**
 * ツールを呼ぶ段まで進める。間に挟まる段は実行器の側がこなすので、ここでは覚える値だけを
 * 同じ順に作って辻褄を合わせる——起動したエディタの番号は操作役の相手と同じ並びになる。
 */
function advance() {
    while (pos < steps.length && steps[pos].kind !== "tool") {
        const step = steps[pos];
        if (step.kind === "control" && step.record !== undefined) {
            if (step.action === "launch") {
                remembered.set(step.record.name, parsed.firstEditor + launched);
                launched += 1;
            } else {
                remembered.set(step.record.name, parsed.view);
            }
        }

        pos += 1;
    }

    return steps[pos];
}

function reply(id, result) {
    process.stdout.write(JSON.stringify({ jsonrpc: JSONRPC_VERSION, id, result }) + "\n");
}

function call(id, params) {
    const step = advance();
    pos += 1;
    calls += 1;

    const broken = mismatch(step, params.name, params.arguments, remembered);
    if (broken !== null) {
        // 呼ばれ方が定義と違うことは、実行器の側の誤りである。期待を満たしえない本文で知らせる。
        keep();
        reply(id, { isError: true, content: [{ type: "text", text: broken }] });

        return;
    }

    // 覚えさせる値は段ごとに違えておく——同じ値を返すと、借りる名前を取り違えた実行器の引数も
    // 定義どおりに見えてしまう。
    if (step.record !== undefined) {
        remembered.set(step.record.name, calls);
    }

    // 違えるのは、指定した番の呼び出しの、指定した形だけとする。まとめて違えると、どの形を
    // 突き合わせているのかを見分けられない——本文が違うだけで落ちるので、知らせも成否も見て
    // いない実行器が通ってしまう。
    const spoiled = parsed.at === calls ? parsed.broken : null;
    const ok = step.expect.ok !== false;

    // 書き込みに成功したことにした段では、渡された置き場を実際に作る。後の段がその実在を
    // 確かめるので、作らないと実行器の側の誤りに見える。作らないこと自体も違え方の1つで、
    // 実在を確かめる段をこなしていない実行器がこれで落ちる。
    if (ok && parsed.broken !== "file" && typeof params.arguments.path === "string") {
        fs.mkdirSync(path.dirname(params.arguments.path), { recursive: true });
        fs.writeFileSync(params.arguments.path, "", "utf8");
    }

    keep();
    reply(id, {
        isError: spoiled === "ok" ? ok : !ok,
        content: [{
            type: "text",
            text: notice(
                step.expect,
                remembered,
                parsed.firstEditor,
                spoiled === "notice" || spoiled === "notice.changed") + "\n"
                + compose(step, spoiled, calls, parsed, remembered),
        }],
    });
}

process.stdin.setEncoding("utf8");
process.stdin.on("data", (chunk) => {
    buffer += chunk;
    for (;;) {
        const end = buffer.indexOf("\n");
        if (end < 0) {
            return;
        }

        const line = buffer.slice(0, end).trim();
        buffer = buffer.slice(end + 1);
        if (line === "") {
            continue;
        }

        const message = JSON.parse(line);
        if (message.method === "initialize") {
            reply(message.id, {
                protocolVersion: MCP_PROTOCOL_VERSION,
                capabilities: { tools: {} },
                serverInfo: { name: "acceptance-stub", version: "1" },
            });
            continue;
        }

        if (message.method === "tools/call") {
            call(message.id, message.params);
        }
    }
});
