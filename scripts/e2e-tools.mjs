// 自動E2E検査の実行器。
// 生成器が書き出した検査を、起動中の実機エディタの待受へ1件ずつ投げ、結果を
// 行キー・編集の流れ・接続の経路ごとに数えて出す。
// 検査の中身はこの実行器が決めず、生成器が書いたものだけを読む。

import fs from "node:fs";
import net from "node:net";
import process from "node:process";

/** 要求と応答の jsonrpc に固定で置く値。 */
const JSONRPC_VERSION = "2.0";

/** ハンドシェイクで一致していなければならないプロトコル番号。 */
const HANDSHAKE_PROTOCOL = 1;

/** 1件の応答を待つ上限。ホスト側の処理上限へ往復の余裕を足した値。 */
const RESPONSE_TIMEOUT_MS = 130000;

/** 待受のパイプ名の付け方。ホスト側の実装が定める。 */
const PIPE_PREFIX = "pmx-editor-mcp-";

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
function judge(one, response) {
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

    if (envelope.ok !== false) {
        return "断るはずが成功しました。";
    }

    if (envelope.error === undefined || envelope.error.code !== one.code) {
        return "断る理由が " + one.code + " ではありません: " + describe(envelope);
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

/** その検査へ与える要求の識別子。ハンドシェイクが1で、検査は2から順に並ぶ。 */
function requestId(index) {
    return index + 2;
}

function run(pipeName, cases) {
    const socket = net.connect(toPipePath(pipeName));
    let buffer = "";
    let index = -1;
    let settled = false;
    const results = [];

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
            send(requestId(index), one.tool, one.arguments);
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

                results.push({ case: cases[index], reason: judge(cases[index], response) });
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

process.exit(await run(PIPE_PREFIX + processId, cases));
