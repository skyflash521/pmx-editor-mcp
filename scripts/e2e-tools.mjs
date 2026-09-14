// 自動E2E検査の実行器。
// 生成器が書き出した検査を、起動中の実機エディタの待受へ1件ずつ投げ、結果を
// 行キー・編集の流れ・接続の経路ごとに数えて出す。
// 検査の中身はこの実行器が決めず、生成器が書いたものだけを読む。

import { spawnSync } from "node:child_process";
import fs from "node:fs";
import net from "node:net";
import os from "node:os";
import path from "node:path";
import process from "node:process";
import url from "node:url";

/** 要求と応答の jsonrpc に固定で置く値。 */
const JSONRPC_VERSION = "2.0";

/** ハンドシェイクで一致していなければならないプロトコル番号。 */
const HANDSHAKE_PROTOCOL = 1;

/** 1件の応答を待つ上限。ホスト側の処理上限へ往復の余裕を足した値。 */
const RESPONSE_TIMEOUT_MS = 130000;

/** 待受のパイプ名の付け方。ホスト側の実装が定める。 */
const PIPE_PREFIX = "pmx-editor-mcp-";

/** 応答待ちの表示へ応答する操作役。画面を触るのはこの1本に寄せる。 */
const CONTROL_SCRIPT = path.join(
    path.dirname(url.fileURLToPath(import.meta.url)), "host-control.ps1");

/** 呼び出しを始めていないことを表す断りの綴り。共通契約が定める。 */
const NOT_STARTED = "TOOL_NOT_STARTED";

/** 人の応答を待つ表示が出ていて進められないことを表す断りの綴り。共通契約が定める。 */
const PROMPT_SHOWN = "TOOL_PROMPT_SHOWN";

/** 写しを取れるビューの名前。ほかのビューは自分の窓を持たない。 */
const CAPTURED_VIEW = "pmx";

/** 同じビューを写した2枚と見なす明るさの差の上限。 */
const MATCHING_IMAGE_LIMIT = 0.1;

/** ビューの写しと画像を見比べるスクリプト。 */
const COMPARE_SCRIPT = path.join(
    path.dirname(url.fileURLToPath(import.meta.url)), "compare-view-image.ps1");

/** ホストが発行するセッションの識別子の形。128ビットを16進で表した文字列である。 */
const SESSION_PATTERN = /^[0-9a-f]{32}$/;

const EXIT_SUCCESS = 0;
const EXIT_FAILED = 1;
const EXIT_INVALID_ARGUMENTS = 2;
const EXIT_INPUT_UNAVAILABLE = 3;

function toPipePath(name) {
    return "\\\\.\\pipe\\" + name;
}

/** 行の区切りで1件ずつ取り出す。区切りはホスト側の実装が定める。 */
function takeLine(buffer) {
    const at = buffer.indexOf("\n");
    if (at < 0) {
        return null;
    }

    return { text: buffer.slice(0, at).trim(), rest: buffer.slice(at + 1) };
}

/**
 * 応答が契約の形をしているか。合っていれば null。要求の識別子と合っているところまで見る
 * ——到着の順だけで割り当てると、別の要求への応答を取り違える。
 */
function contract(response, requestId) {
    if (response === null || typeof response !== "object" || Array.isArray(response)) {
        return "応答がJSONのオブジェクトではありません。";
    }
    if (response.jsonrpc !== JSONRPC_VERSION) {
        return "応答の jsonrpc が " + JSONRPC_VERSION + " ではありません。";
    }
    if (response.id !== requestId) {
        return "応答の id が要求の " + requestId + " ではありません: " + JSON.stringify(response.id);
    }

    const hasResult = Object.prototype.hasOwnProperty.call(response, "result");
    const hasError = Object.prototype.hasOwnProperty.call(response, "error");
    if (hasResult === hasError) {
        return "応答が result と error のどちらか一方だけを持っていません。";
    }
    if (hasError) {
        const error = response.error;
        if (error === null || typeof error !== "object" || Array.isArray(error) ||
            typeof error.code !== "number" || typeof error.message !== "string") {
            return "error が code と message の組ではありません。";
        }
    }

    return null;
}

/** ハンドシェイクの成功応答が契約どおりか。合っていれば null。 */
function handshake(result) {
    if (result === null || typeof result !== "object" || Array.isArray(result)) {
        return "handshake の result がJSONのオブジェクトではありません。";
    }
    if (result.protocol !== HANDSHAKE_PROTOCOL) {
        return "ホストのプロトコル番号が " + HANDSHAKE_PROTOCOL + " ではありません。";
    }
    if (typeof result.hostVersion !== "string" || result.hostVersion.length === 0) {
        return "handshake の hostVersion が空でない文字列ではありません。";
    }
    if (!Number.isInteger(result.budgetChars)) {
        return "handshake の budgetChars が整数ではありません。";
    }
    if (typeof result.session !== "string" || !SESSION_PATTERN.test(result.session)) {
        return "handshake の session が16進32文字の文字列ではありません。";
    }

    return null;
}

/**
 * 1件の検査の結末。合っていれば null、違っていればその理由を返す。
 * 包みの形は共通契約が定めるので、ここでは成功・失敗と理由の綴りだけを見る。
 */
function judge(one, response, capture) {
    if (one.expect === "dispatched") {
        return dispatched(response);
    }

    if (one.expect === "viewImage") {
        return viewImage(one, response, capture);
    }

    if (response.error !== undefined) {
        return "ホストが要求を断りました(" + response.error.code + "): " + response.error.message;
    }

    const envelope = response.result;
    if (envelope === null || typeof envelope !== "object" || Array.isArray(envelope)) {
        return "result が包みのオブジェクトではありません。";
    }

    if (one.expect === "success") {
        return envelope.ok === true ? null : "成功するはずが断られました: " + describe(envelope);
    }

    if (one.expect === "called") {
        if (envelope.ok === true) {
            return null;
        }

        return envelope.error !== undefined && envelope.error.code === PROMPT_SHOWN
            ? null
            : "呼び先まで届くはずが断られました: " + describe(envelope);
    }

    if (one.expect === "denied") {
        if (envelope.ok !== false) {
            return "断るはずが成功しました。";
        }
        if (envelope.error === undefined || envelope.error.code !== one.code) {
            return "断る理由が " + one.code + " ではありません: " + describe(envelope);
        }

        return envelope.error.message.indexOf(one.says) >= 0
            ? null
            : "断る理由が " + JSON.stringify(one.says) + " を述べていません: "
                + envelope.error.message;
    }

    if (one.expect === "reads") {
        return envelope.ok === true
            ? reads(one.expected, envelope.value)
            : "読み返せるはずが断られました: " + describe(envelope);
    }

    if (envelope.ok !== false) {
        return "断るはずが成功しました。";
    }

    if (envelope.error === undefined || envelope.error.code !== one.code) {
        return "断る理由が " + one.code + " ではありません: " + describe(envelope);
    }

    return null;
}

/**
 * 呼び先が在るか。未知のメソッドとホストの内部の失敗だけを落とし、引数の不足で断られた応答は
 * 呼び先が在る証拠として通す。
 */
function dispatched(response) {
    const unknown = -32601;
    const internal = -32603;
    if (response.error !== undefined
        && (response.error.code === unknown || response.error.code === internal)) {
        return "呼び先が無いか内部で失敗しました(" + response.error.code + "): "
            + response.error.message;
    }

    return null;
}

/** ビューの写しを1枚だけ取る。取れなければその事情を返す。 */
function captureView(processId) {
    const destination = path.join(os.tmpdir(), "pmx-editor-mcp-view.png");
    const done = invokeControl([
        "-File", CONTROL_SCRIPT, "-Action", "capture",
        "-ProcessId", String(processId), "-View", CAPTURED_VIEW, "-Path", destination,
    ]);

    return done.written === null
        ? { path: null, unavailable: done.unavailable }
        : { path: destination, unavailable: null };
}

/** 写しと画像の明るさの差。比べられなければその事情を返す。 */
function difference(reference, image) {
    const candidate = path.join(os.tmpdir(), "pmx-editor-mcp-view.b64");
    fs.writeFileSync(candidate, image, "utf8");
    const done = invokeControl([
        "-File", COMPARE_SCRIPT, "-Reference", reference, "-Candidate", candidate,
    ]);
    if (done.written === null) {
        return { measured: null, unavailable: done.unavailable };
    }

    const measured = Number.parseFloat(done.written);

    return Number.isFinite(measured)
        ? { measured, unavailable: null }
        : { measured: null, unavailable: "明るさの差を数として読めません: " + done.written };
}

/**
 * 返した画像が、写し取ったビューの姿と合うか。写せるビューを返す行は合い、ほかのビューを返す行は
 * 合わないことを確かめる。
 */
function viewImage(one, response, capture) {
    if (capture.path === null) {
        return "ビューを写し取れませんでした: " + capture.unavailable;
    }

    const envelope = response.result;
    if (envelope === null || typeof envelope !== "object" || envelope.ok !== true) {
        return "画像が返りませんでした: " + JSON.stringify(response).slice(0, 200);
    }
    if (typeof envelope.value !== "string" || envelope.value.length === 0) {
        return "画像が文字列で返りませんでした。";
    }

    const compared = difference(capture.path, envelope.value);
    if (compared.measured === null) {
        return "画像を写しと見比べられませんでした: " + compared.unavailable;
    }

    const matches = compared.measured <= MATCHING_IMAGE_LIMIT;
    if (one.view === CAPTURED_VIEW) {
        return matches
            ? null
            : "写したビューの姿と合いません(明るさの差 " + compared.measured + ")。";
    }

    return matches
        ? "別のビューの画像が写したビューの姿と合いました(明るさの差 " + compared.measured + ")。"
        : null;
}

/**
 * 読み返した項目が、書いた値のまま読めているか。並べて返す形は全件を、1つを返す形はそれ自身を
 * 見る。合っていれば null。1件も返らない並びは落とす——1件も見ないまま通ると、書き込みを
 * 確かめない検査になる。
 */
function reads(expected, value) {
    const items = value !== null && typeof value === "object" && Array.isArray(value.items)
        ? value.items
        : [value];
    if (items.length === 0) {
        return "読み返すものが1件もありません。書いた値をどれも確かめられません。";
    }
    for (let at = 0; at < items.length; at++) {
        const item = items[at];
        if (item === null || typeof item !== "object") {
            return "読み返した" + at + "件目が項目の組ではありません。";
        }
        if (!Object.prototype.hasOwnProperty.call(item, expected.member)) {
            return "読み返した" + at + "件目に " + expected.member + " がありません。";
        }
        if (item[expected.member] !== expected.value) {
            return "読み返した" + at + "件目の " + expected.member + " が "
                + JSON.stringify(expected.value) + " ではありません: "
                + JSON.stringify(item[expected.member]);
        }
    }

    return null;
}

function describe(envelope) {
    return envelope.error === undefined
        ? JSON.stringify(envelope)
        : envelope.error.code + " " + envelope.error.message;
}

/** 鍵ごとに合否を数える。鍵が空の検査はその区切りに現れない。 */
function tally(results, key) {
    const counts = new Map();
    for (const result of results) {
        const name = result.case[key];
        if (name === "") {
            continue;
        }

        const count = counts.get(name) ?? { passed: 0, failed: 0 };
        if (result.reason === null) {
            count.passed += 1;
        } else {
            count.failed += 1;
        }
        counts.set(name, count);
    }

    return counts;
}

function report(results, key, title) {
    const counts = tally(results, key);
    console.log("");
    console.log(title + ": " + counts.size + " 種");
    for (const name of [...counts.keys()].sort()) {
        const count = counts.get(name);
        console.log("  " + name + ": 合格 " + count.passed + "・不合格 " + count.failed);
    }
}

/**
 * 操作役のスクリプトを起こし、書き出したものと、落ちたときの事情を返す。
 * PowerShellの出力の文字コードは端末の設定で変わるので、読めない並びは読めないまま置いて、
 * 数と綴りだけを確かに読めるようにする。
 */
function invokeControl(args) {
    const done = spawnSync("pwsh", ["-NoProfile", ...args], { encoding: "utf8" });
    if (done.error !== undefined) {
        return { written: null, unavailable: "pwsh を起こせません: " + done.error.message };
    }
    if (done.status !== 0) {
        return {
            written: null,
            unavailable: "pwsh が " + done.status + " で終わりました: "
                + ((done.stderr ?? "").trim() || "(何も言いませんでした)"),
        };
    }

    return { written: (done.stdout ?? "").trim(), unavailable: null };
}

/** エディタが出している応答待ちの表示へ応答して閉じ、閉じた数を返す。 */
function answerDialogs(processId) {
    const done = invokeControl([
        "-File", CONTROL_SCRIPT, "-Action", "answer", "-ProcessId", String(processId),
    ]);
    if (done.written === null) {
        return 0;
    }

    const answered = Number.parseInt(done.written, 10);

    return Number.isInteger(answered) ? answered : 0;
}

/** 表示へ何度まで続けて答えるか。1つ答えると次が出る作りがあるので、1度では足りない。 */
const ANSWERING_ROUNDS = 8;

/**
 * 出ている表示へ、出なくなるまで答える。答え切れたときだけ答えた総数を返す。1つも答えられ
 * なかったときと、上限まで答えても出続けたときは0で、そのときは投げ直しても同じところで止まる。
 */
function clearPrompts(processId) {
    let answered = 0;
    for (let round = 0; round < ANSWERING_ROUNDS; round++) {
        const cleared = answerDialogs(processId);
        if (cleared === 0) {
            return answered;
        }

        answered += cleared;
    }

    // 上限まで答えても出続けるなら、まだ出ている。答えられたことにすると、残った表示に
    // 続きの検査が巻き添えで落ちる。
    return 0;
}

/** ホストが呼び出しを始めていないと言っているか。始めていなければ投げ直せる。 */
function notStarted(response) {
    const envelope = response.result;

    return envelope !== null
        && typeof envelope === "object"
        && !Array.isArray(envelope)
        && envelope.ok === false
        && envelope.error !== undefined
        && envelope.error.code === NOT_STARTED;
}

/**
 * 表示が出たことを知らせる断りか。次の検査へ進む前に表示へ答えるので、出たままにならない。
 */
function prompted(response) {
    const envelope = response.result;

    return envelope !== null
        && envelope !== undefined
        && typeof envelope === "object"
        && !Array.isArray(envelope)
        && envelope.ok === false
        && envelope.error !== undefined
        && envelope.error.code === PROMPT_SHOWN;
}

/**
 * 借りる値を差し込んだ引数。差し込む先は引数の中の道で、斜線で区切った各段をたどる。借りる
 * 名前をまだ覚えていなければ null。
 */
function borrowing(one, remembered) {
    if (one.borrowed === undefined) {
        return one.arguments;
    }

    const given = JSON.parse(JSON.stringify(one.arguments));
    for (const [path, from] of Object.entries(one.borrowed)) {
        if (!remembered.has(from)) {
            return null;
        }

        const steps = path.split("/");
        let held = given;
        for (let at = 0; at < steps.length - 1; at++) {
            held = held[steps[at]];
        }

        held[steps[steps.length - 1]] = [remembered.get(from)];
    }

    return given;
}

/** その検査へ与える要求の識別子。ハンドシェイクが1で、検査は2から順に並ぶ。 */
function requestId(index) {
    return index + 2;
}

function run(pipeName, cases, processId) {
    const socket = net.connect(toPipePath(pipeName));
    let buffer = "";
    let index = -1;
    let retried = -1;
    let settled = false;
    const results = [];
    const remembered = new Map();
    let capture = null;

    return new Promise((resolve) => {
        const settle = (code, message) => {
            if (settled) {
                return;
            }
            settled = true;
            if (message !== null) {
                console.error(message);
            }
            socket.destroy();
            resolve(code);
        };

        const send = (id, method, params) => {
            const request = { jsonrpc: JSONRPC_VERSION, id, method };
            if (params !== undefined) {
                request.params = params;
            }
            socket.write(JSON.stringify(request) + "\n");
        };

        const next = () => {
            index += 1;
            if (index >= cases.length) {
                finish();
                return;
            }

            const one = cases[index];
            if (one.expect === "viewImage" && capture === null) {
                capture = captureView(processId);
            }

            const given = borrowing(one, remembered);
            if (given === null) {
                results.push({
                    case: one,
                    reason: "借りる値をまだ覚えていません: " + JSON.stringify(one.borrowed),
                });
                next();

                return;
            }

            send(requestId(index), one.tool, given);
        };

        const finish = () => {
            const failed = results.filter((r) => r.reason !== null);
            for (const result of failed) {
                console.log(
                    "不合格: " + result.case.tool + " — " + result.case.purpose + " — " + result.reason);
            }

            console.log("");
            console.log(
                "検査: " + results.length + " 件・合格 " + (results.length - failed.length) +
                "・不合格 " + failed.length);
            report(results, "rowKey", "行キー");
            report(results, "editKind", "編集の流れ");
            report(results, "connectionPath", "接続の経路");

            settle(failed.length === 0 ? EXIT_SUCCESS : EXIT_FAILED, null);
        };

        socket.on("error", (error) => settle(EXIT_INPUT_UNAVAILABLE, "接続に失敗しました: " + error.message));
        socket.on("timeout", () => settle(EXIT_INPUT_UNAVAILABLE, "応答が時間内に返りませんでした。"));
        socket.on("close", () => settle(EXIT_INPUT_UNAVAILABLE, "ホストが接続を切りました。"));

        socket.on("connect", () => {
            socket.setTimeout(RESPONSE_TIMEOUT_MS);
            console.log("接続しました: " + pipeName);
            send(requestId(-1), "handshake", { protocol: HANDSHAKE_PROTOCOL });
        });

        socket.on("data", (chunk) => {
            if (settled) {
                return;
            }

            buffer += chunk.toString("utf8");
            for (;;) {
                const taken = takeLine(buffer);
                if (taken === null) {
                    return;
                }
                buffer = taken.rest;
                if (taken.text.length === 0) {
                    continue;
                }

                let response;
                try {
                    response = JSON.parse(taken.text);
                } catch (error) {
                    settle(EXIT_INPUT_UNAVAILABLE, "応答を読み解けませんでした: " + error.message);
                    return;
                }

                const broken = contract(response, requestId(index));
                if (broken !== null) {
                    settle(EXIT_INPUT_UNAVAILABLE, broken);
                    return;
                }

                if (index < 0) {
                    if (response.error !== undefined) {
                        settle(
                            EXIT_INPUT_UNAVAILABLE,
                            "ホストが handshake を断りました: " + response.error.message);
                        return;
                    }

                    const mismatch = handshake(response.result);
                    if (mismatch !== null) {
                        settle(EXIT_INPUT_UNAVAILABLE, mismatch);
                        return;
                    }

                    next();
                    continue;
                }

                const one = cases[index];
                if (notStarted(response) && retried !== index
                    && clearPrompts(processId) > 0) {
                    retried = index;
                    send(requestId(index), one.tool, borrowing(one, remembered));
                    continue;
                }

                // 片付かない表示を残したまま先へ進むと、後の検査が巻き添えで落ちる。
                if (prompted(response) && clearPrompts(processId) === 0) {
                    results.push({
                        case: one,
                        reason: "出ている表示を片付けられませんでした。",
                    });
                    next();

                    continue;
                }

                const reason = judge(one, response, capture);
                if (reason === null && one.produces !== undefined) {
                    remembered.set(one.produces, response.result.value);
                }

                results.push({ case: one, reason });
                next();
            }
        });
    });
}

function readCases(path) {
    const text = fs.readFileSync(path, "utf8");
    const read = JSON.parse(text);
    if (read === null || typeof read !== "object" || !Array.isArray(read.cases)) {
        throw new Error("cases の並びを持たない。");
    }

    return read.cases;
}

const [processId, casesPath] = process.argv.slice(2);
if (processId === undefined || casesPath === undefined) {
    console.error("使い方: node e2e-tools.mjs <エディタのプロセスID> <検査のパス>");
    process.exit(EXIT_INVALID_ARGUMENTS);
}

let cases;
try {
    cases = readCases(casesPath);
} catch (error) {
    console.error("検査を読めません(" + casesPath + "): " + error.message);
    process.exit(EXIT_INPUT_UNAVAILABLE);
}

if (cases.length === 0) {
    console.log("検査が1件も無い。");
    process.exit(EXIT_SUCCESS);
}

process.exit(await run(PIPE_PREFIX + processId, cases, processId));
