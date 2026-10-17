// 実機動作確認の確認クライアント。
// 常駐ホストが待ち受ける名前付きパイプへ接続し、要求を1件ずつ送って応答を表示する。
// 接続先の名前の付け方と、要求・応答の符号化と区切りはホスト側の実装が定める。
// 接続先のエディタと送る要求の並びは起動引数で決まる。

import fs from "node:fs";
import net from "node:net";
import process from "node:process";

/** 要求と応答の jsonrpc に固定で置く値。 */
const JSONRPC_VERSION = "2.0";

/** ハンドシェイクで一致していなければならないプロトコル番号。 */
const HANDSHAKE_PROTOCOL = 1;

/**
 * 引数を省略したときに期待する応答サイズ予算の文字数。ホストが読む既定値で、実機動作確認の
 * 実行器は予算の環境変数を外してからエディタを起こす。
 */
const EXPECTED_BUDGET_CHARS = 100000;

/** 応答を受け切ったあとも接続を保ち続けるよう指示する語。第1引数の直後に置く。 */
const HOLD_WORD = "--hold";

/** 引数を省略したときに送る要求の並び。 */
const DEFAULT_REQUEST_WORDS = ["handshake", JSON.stringify({ protocol: HANDSHAKE_PROTOCOL }), "ping"];

/**
 * 引数を省略したときに送る要求へ課す期待。既定の要求は成功する前提で、結果の中身まで確かめる。
 * 要求を明示したときは課さない。
 */
const DEFAULT_EXPECTATIONS = {
    handshake: expectHandshakeResult,
    ping: expectPongResult,
};

/** 落ちた項目の番号。 */
const fell = new Set();

/** 接続を保つ待ちへ入ったことを、呼ぶ側が読む形で知らせる。 */
function tellHolding() {
    const toldPath = process.env.PMX_EDITOR_MCP_HOLDING_PATH;
    if (toldPath) fs.writeFileSync(toldPath, String(process.pid), "utf8");
}

/** 落ちた項目の番号を控えて、その言い分をそのまま返す。 */
function noted(at, says) {
    fell.add(at);

    return says;
}

/** ハンドシェイクの成功応答が契約どおりかを見る。合っていれば null を返す。 */
function expectHandshakeResult(taken) {
    if (!taken.hasResult) {
        return noted(0, "成功応答であるべきところがエラー応答です。");
    }
    const result = taken.result;
    if (result === null || typeof result !== "object" || Array.isArray(result)) {
        return noted(1, "result がJSONのオブジェクトではありません。");
    }
    if (result.protocol !== HANDSHAKE_PROTOCOL) {
        return noted(2, "result の protocol が " + HANDSHAKE_PROTOCOL + " ではありません。");
    }
    if (typeof result.hostVersion !== "string" || result.hostVersion.length === 0) {
        return noted(3, "result の hostVersion が空でない文字列ではありません。");
    }
    if (result.budgetChars !== EXPECTED_BUDGET_CHARS) {
        return noted(
            4,
            "result の budgetChars が " + EXPECTED_BUDGET_CHARS + " ではありません" +
            "(予算の環境変数を設定したままエディタを起動していないか確かめてください)。"
        );
    }
    if (typeof result.session !== "string" || !/^[0-9a-f]{32}$/.test(result.session)) {
        return noted(5, "result の session が16進32文字の文字列ではありません。");
    }
    return null;
}

/** ping の成功応答が契約どおりかを見る。合っていれば null を返す。 */
function expectPongResult(taken) {
    if (!taken.hasResult) {
        return noted(6, "成功応答であるべきところがエラー応答です。");
    }
    if (taken.result !== "pong") {
        return noted(7, "result が pong ではありません。");
    }
    return null;
}

/**
 * 1件の応答が収まるバイト数の上限。ホストが本文へ課すのと同じ値を採り、区切りが来ないまま
 * 積み続けるのも、区切りまで揃った1行が上限を超えるのも拒む。
 */
const MAX_RESPONSE_BYTES = 16 * 1024 * 1024;

/** 要求へ採番する次の識別子。1から始めて要求ごとに1ずつ増やす。 */
let nextRequestId = 1;

/** 直前に送った要求へ採番した識別子。応答の突き合わせに用いる。 */
let sentRequestId = null;

/**
 * 本文を厳格なUTF-8でデコードする。壊れたバイト列は例外にする。ホストはBOMを付けない。
 * BOMは取り除かずに残す。
 */
const decoder = new TextDecoder("utf-8", { fatal: true, ignoreBOM: true });

/** 応答の本文を記録に残せる長さへ切り詰める。本文は上限の16MiBまでありうる。 */
function describeLine(line) {
    if (line.length === 0) {
        return "(空)";
    }
    return line.length > 200 ? line.slice(0, 200) + "…" : line;
}

/** バイト列の先頭を16進で表す。 */
function describeBytes(bytes) {
    const head = bytes.subarray(0, 32);
    return head.toString("hex") + (bytes.length > head.length ? "…" : "");
}

/** 接続先の名前付きパイプ名を、対象エディタのプロセスIDから組み立てる。 */
function buildPipeName(editorProcessId) {
    return "pmx-editor-mcp-" + editorProcessId;
}

/**
 * コマンドライン引数の語の並びを要求の並びへ組み立てる。
 * 1件の要求はメソッド名単体か、メソッド名の直後に params のJSON表記を続けた2語で表す。
 * メソッド名の位置にJSONに見える語が来れば拒む。
 */
function parseRequests(words) {
    const requests = [];
    for (let index = 0; index < words.length; index += 1) {
        const method = words[index];
        if (looksLikeOption(method)) {
            throw new Error("知らない指定です: " + method);
        }
        if (looksLikeJson(method)) {
            throw new Error(
                "メソッド名の位置にJSONに見える語があります。" +
                    "1件の要求は「メソッド名」か「メソッド名 params」で指定してください: " +
                    method,
            );
        }

        const next = words[index + 1];
        if (next !== undefined && looksLikeOption(next)) {
            throw new Error("知らない指定です: " + next);
        }
        if (next !== undefined && looksLikeJson(next)) {
            requests.push({ method, params: parseParams(next), label: describeMethod(method) + " " + next });
            index += 1;
            continue;
        }
        requests.push({ method, params: undefined, label: describeMethod(method) });
    }
    return requests;
}

/** メソッド名を表示に載せる形にする。空のメソッド名は、空であると書く。 */
function describeMethod(method) {
    return method.length === 0 ? "(空のメソッド名)" : method;
}

/** params のJSON表記を解く。誤りは接続する前に知らせる。 */
function parseParams(word) {
    try {
        return JSON.parse(word);
    } catch (error) {
        throw new Error("params をJSONとして解釈できません: " + word + " (" + error.message + ")");
    }
}

/** オプションの打ち間違いかどうか。JSONの数値になる語(params の形)は除く。 */
function looksLikeOption(word) {
    return word.startsWith("-") && Number.isNaN(Number(word));
}

/** JSONの値の書き出しかどうか。メソッド名にこの形は現れない。 */
function looksLikeJson(word) {
    // JSON.parse は前後の空白を許す。
    const body = word.trim();
    return /^["[{0-9-]/.test(body) || body === "true" || body === "false" || body === "null";
}

/**
 * 要求を、送信するバイト列へ変換する。戻り値は Buffer。
 * 併せて識別子を1つ採番し、応答の突き合わせに使えるよう控える。
 * 引数の妥当性は組み立ての時点で確かめてあり、ここでは失敗しない。
 */
function encodeRequest(request) {
    const body = {
        jsonrpc: JSONRPC_VERSION,
        id: nextRequestId,
        method: request.method,
    };
    if (request.params !== undefined) {
        body.params = request.params;
    }

    sentRequestId = nextRequestId;
    nextRequestId += 1;
    return Buffer.from(JSON.stringify(body) + "\n", "utf8");
}

/**
 * 受信済みのバイト列の先頭から、応答を1件取り出す。buffer は Buffer。
 * 取り出せたときは、表示する文字列を持つ text・残りのバイト列(Buffer)を持つ rest・
 * エラー応答ならそのコード、成功応答なら null を持つ errorCode・成功応答かどうかを持つ
 * hasResult・成功応答ならその result(エラー応答なら undefined)からなるオブジェクトを返す。
 * 1件に満たないときは null を返す。
 * 1件ぶん揃っているが契約に反する場合は、応答の本文(長ければ切り詰めたもの)を添えた例外を投げる。
 */
function takeResponse(buffer) {
    const newlineIndex = buffer.indexOf(0x0a);
    if (newlineIndex < 0) {
        return null;
    }

    if (newlineIndex > MAX_RESPONSE_BYTES) {
        throw new Error(noted(
            8,
            "応答の本文が " + newlineIndex + " バイトで、上限の " + MAX_RESPONSE_BYTES + " バイトを超えています。",
        ));
    }

    const body = buffer.subarray(0, newlineIndex);
    const rest = buffer.subarray(newlineIndex + 1);

    let line;
    try {
        line = decoder.decode(body);
    } catch (error) {
        throw new Error(noted(9, "応答をUTF-8として解釈できません: " + describeBytes(body) + " (" + error.message + ")"));
    }

    if (line.charCodeAt(0) === 0xfeff) {
        throw new Error(noted(10, "応答の先頭にBOMが付いています: " + describeLine(line.slice(1))));
    }

    // 出力の区切りはLFだけで、CRLFを受理するのは入力側だけである。
    if (line.charCodeAt(line.length - 1) === 13) {
        throw new Error(noted(11, "応答の行末にCRが付いています: " + describeLine(line)));
    }

    let response;
    try {
        response = JSON.parse(line);
    } catch (error) {
        throw new Error(noted(12, "応答をJSONとして解釈できません: " + describeLine(line) + " (" + error.message + ")"));
    }

    if (response === null || typeof response !== "object" || Array.isArray(response)) {
        throw new Error(noted(13, "応答がJSONのオブジェクトではありません: " + describeLine(line)));
    }
    if (response.jsonrpc !== JSONRPC_VERSION) {
        throw new Error(noted(14, "応答の jsonrpc が " + JSONRPC_VERSION + " ではありません: " + describeLine(line)));
    }
    if (!Object.prototype.hasOwnProperty.call(response, "id")) {
        throw new Error(noted(15, "応答が id を持ちません: " + describeLine(line)));
    }

    // ホストが id を落とすのは、要求の識別子を判別できなかったときと、識別子まで載せると
    // エラー応答が上限のバイト数に収まらないときで、どちらもエラー応答に限られる。
    const unidentified = response.id === null;
    if (!unidentified && response.id !== sentRequestId) {
        throw new Error(noted(16, "応答の識別子が要求の識別子 " + sentRequestId + " と一致しません: " + describeLine(line)));
    }

    const hasResult = Object.prototype.hasOwnProperty.call(response, "result");
    const hasError = Object.prototype.hasOwnProperty.call(response, "error");
    if (hasResult === hasError) {
        throw new Error(noted(17, "応答は result と error のどちらか一方だけを持たなければなりません: " + describeLine(line)));
    }
    if (unidentified && hasResult) {
        throw new Error(noted(18, "識別子を持たない応答が成功応答になっています: " + describeLine(line)));
    }

    let errorCode = null;
    if (hasError) {
        const error = response.error;
        if (error === null || typeof error !== "object" || Array.isArray(error)) {
            throw new Error(noted(19, "エラー応答の error がJSONのオブジェクトではありません: " + describeLine(line)));
        }
        if (typeof error.code !== "number") {
            throw new Error(noted(20, "エラー応答が数値の code を持ちません: " + describeLine(line)));
        }
        if (typeof error.message !== "string") {
            throw new Error(noted(21, "エラー応答が文字列の message を持ちません: " + describeLine(line)));
        }
        errorCode = error.code;
    }

    const text = JSON.stringify(hasResult ? response.result : response.error);
    return { text, rest, errorCode, hasResult, result: hasResult ? response.result : undefined };
}

/**
 * 接続を待つ無通信の上限。待受が無ければ即座に、使用中(パイプインスタンスは1つ)なら
 * 数秒で失敗が返る。
 */
const CONNECT_TIMEOUT_MS = 15000;

/**
 * 応答を待つ無通信の上限。受信のたびに数え直す。ホストは1件の処理に120秒まで許し、超過した
 * ときだけエラー応答を返す。超過した処理が終わるまで、ホストは次の要求を読み取らない。
 * 応答を受け切ったあとや切断が要るエラーのあとに、ホストが接続を閉じるのを待つ上限も同じ値で兼ねる。
 */
const RESPONSE_TIMEOUT_MS = 130000;

/**
 * 終了コード。契約に反することが起きなかった終わり方を3つに分ける。0 は要求への応答がすべて
 * 返って自分から終えたとき、3 は切断が要るエラー応答のあとホストが契約どおり接続を切ったとき、
 * 4 は接続を保ったままホスト側から切られたときである。エラー応答が含まれるかどうかは問わない。
 * 切断が要るエラーを誘う要求は、並びの末尾に置く。後続の要求は応答を得られない。
 * 引数の誤り・接続や送受信の失敗・応答が揃う前の切断・受け取った応答が契約に反することは失敗とする。
 * 待って初めて分かること(待受が応じない、応答が返らない、切るはずの接続を切らない、
 * 閉じるはずの接続を閉じない)は時間切れとする。保持しているときも、この見分けは変わらない。
 */
const EXIT_OK = 0;
const EXIT_ERROR = 1;
const EXIT_TIMEOUT = 2;
const EXIT_CLOSED_AFTER_DISCONNECTING_ERROR = 3;
const EXIT_CLOSED_WHILE_HOLDING = 4;

/** ホストが接続を切ったときに、こちら側の読み書きに現れるエラーコード。 */
const DISCONNECTED_CODES = ["EPIPE", "ECONNRESET"];

/**
 * ホストが応答を書いたあと接続を切るエラーコード。
 * これ以外のエラー応答では接続が続くので、そのあとの切断は別の理由による。
 */
const DISCONNECTING_ERROR_CODES = [-32700, -32001, -32003, -32004, -32006];

function toPipePath(name) {
    return "\\\\.\\pipe\\" + name;
}

function run(pipeName, requests, hold) {
    const socket = net.connect(toPipePath(pipeName));
    let connected = false;
    let complete = false;
    let settled = false;
    let buffer = Buffer.alloc(0);
    let index = 0;
    let lastErrorCode = null;
    let awaitingClose = false;

    function settle(code, message) {
        if (settled) {
            return;
        }
        settled = true;
        if (message !== null) {
            console.error(message);
        }

        const toldPath = process.env.PMX_EDITOR_MCP_FELL_PATH;
        if (toldPath) {
            fs.writeFileSync(toldPath, [...fell].join(","), "utf8");
        }

        process.exitCode = code;
    }

    function finish() {
        if (settled) {
            return;
        }
        if (hold) {
            console.log("ホストが接続を切りました。");
            settle(EXIT_CLOSED_WHILE_HOLDING, null);

            return;
        }
        settle(EXIT_OK, null);
    }

    function settleOnClose() {
        if (settled) {
            return;
        }
        if (awaitingClose && index >= requests.length) {
            console.log(
                "切断が要るエラー応答(" + lastErrorCode + ")のあと、ホストが契約どおり接続を切りました。",
            );
            settle(EXIT_CLOSED_AFTER_DISCONNECTING_ERROR, null);
            return;
        }
        settle(EXIT_ERROR, describeDisconnection());
    }

    function abort(message) {
        settle(EXIT_ERROR, message);
        socket.destroy();
    }

    function send(request) {
        try {
            socket.write(encodeRequest(request));
        } catch (error) {
            abort("要求を送信できませんでした: " + error.message);
        }
    }

    socket.setTimeout(CONNECT_TIMEOUT_MS, () => {
        if (settled) {
            return;
        }
        if (complete) {
            settle(EXIT_TIMEOUT, "応答を受け切ったあと、ホストが接続を閉じないまま上限の時間に達しました。");
        } else if (awaitingClose) {
            settle(
                EXIT_TIMEOUT,
                "切断が要るエラー応答(" + lastErrorCode + ")のあとも接続が続いたまま上限の時間に達しました。",
            );
        } else if (connected) {
            settle(EXIT_TIMEOUT, "応答が時間内に返りませんでした: " + requests[index].label);
        } else {
            settle(EXIT_TIMEOUT, "パイプへ時間内に接続できませんでした: " + pipeName);
        }
        socket.destroy();
    });

    socket.on("connect", () => {
        connected = true;
        socket.setTimeout(RESPONSE_TIMEOUT_MS);
        console.log("接続しました: " + pipeName);
        send(requests[index]);
    });

    socket.on("data", (chunk) => {
        if (settled) {
            return;
        }
        if (complete) {
            abort("最後の応答のあとに予期しないデータが届きました。");
            return;
        }
        if (awaitingClose) {
            abort("切断が要るエラー応答(" + lastErrorCode + ")のあとにデータが届きました。");
            return;
        }
        buffer = Buffer.concat([buffer, chunk]);

        while (index < requests.length) {
            let taken;
            try {
                taken = takeResponse(buffer);
            } catch (error) {
                abort("応答を取り出せませんでした(" + requests[index].label + "): " + error.message);
                return;
            }
            if (taken === null) {
                break;
            }
            console.log(requests[index].label + " -> " + taken.text);

            const mismatch = requests[index].expect === undefined ? null : requests[index].expect(taken);
            if (mismatch !== null) {
                abort("応答が期待と違います(" + requests[index].label + "): " + mismatch);
                return;
            }

            lastErrorCode = taken.errorCode;
            buffer = taken.rest;
            index += 1;

            // ホストはこのエラーのあと接続を切る。
            if (DISCONNECTING_ERROR_CODES.includes(lastErrorCode)) {
                if (buffer.length > 0) {
                    abort("切断が要るエラー応答(" + lastErrorCode + ")のあとにデータが届きました。");
                    return;
                }
                awaitingClose = true;
                return;
            }
            if (index >= requests.length) {
                break;
            }

            if (buffer.length > 0) {
                abort("次の要求を送る前に予期しない応答が届きました。");
                return;
            }
            send(requests[index]);
            if (settled) {
                return;
            }
        }

        if (index < requests.length) {
            if (buffer.length > MAX_RESPONSE_BYTES) {
                abort(
                    "応答の区切りが現れないまま受信済みが " +
                        buffer.length +
                        " バイトになり、上限の " +
                        MAX_RESPONSE_BYTES +
                        " バイトを超えました。",
                );
            }
            return;
        }
        if (buffer.length > 0) {
            abort("最後の応答のあとに予期しないデータが届きました。");
            return;
        }
        complete = true;
        if (hold) {
            socket.setTimeout(0);
            console.log("接続を保持しています。ホストが切るか、Ctrl+C を押すまで待ちます。");
            tellHolding();
            return;
        }
        socket.end();
    });

    socket.on("error", (error) => {
        if (settled) {
            return;
        }
        if (complete) {
            if (!DISCONNECTED_CODES.includes(error.code)) {
                const where = hold ? "接続を保持している間に" : "応答を受け切ったあとに";
                abort(where + "失敗しました: " + error.message);
                return;
            }
            finish();
            socket.destroy();
            return;
        }
        if (connected && DISCONNECTED_CODES.includes(error.code)) {
            settleOnClose();
            socket.destroy();
            return;
        }
        abort("接続または送受信に失敗しました: " + error.message);
    });

    socket.on("close", () => {
        if (settled) {
            return;
        }
        if (complete) {
            finish();
            return;
        }
        settleOnClose();
    });

    // ホストは切断が要るエラーでは応答を書いてから切る。
    function describeDisconnection() {
        const pending = requests.slice(index).map((request) => request.label);
        let cause;
        if (index === 0) {
            cause = "応答を1件も受け取らないままホストが接続を切りました。";
        } else if (DISCONNECTING_ERROR_CODES.includes(lastErrorCode)) {
            cause = "切断が要るエラー応答(" + lastErrorCode + ")のあとにホストが接続を切りました。";
        } else {
            cause = "ホストが接続を切りました(直前の応答は切断が要るものではありません)。";
        }
        const partial =
            buffer.length > 0 ? " 区切りに達していない受信済みのデータが " + buffer.length + " バイトあります。" : "";
        return (
            cause + partial + " 応答を受け取っていない要求: " + pending.join(", ")
        );
    }
}

/** 起動引数の誤りを、正しい書き方を添えて知らせる。 */
function reject(message) {
    console.error(message);
    console.error(
        "使い方: node scripts/e2e-check.mjs <エディタのプロセスID> [" + HOLD_WORD + "] [メソッド名 [params]] ...",
    );
    console.error("要求を省略すると " + DEFAULT_REQUEST_WORDS.join(" ") + " を送ります。");
    process.exitCode = EXIT_ERROR;
}

function main() {
    const args = process.argv.slice(2);
    const editorProcessId = args[0];
    if (!/^[1-9][0-9]*$/.test(editorProcessId)) {
        reject("第1引数に対象エディタのプロセスIDを指定してください。");
        return;
    }

    let words = args.slice(1);
    const hold = words[0] === HOLD_WORD;
    if (hold) {
        words = words.slice(1);
    }

    if (words.includes(HOLD_WORD)) {
        reject(HOLD_WORD + " は第1引数の直後に1つだけ置けます。");
        return;
    }

    const useDefaults = words.length === 0;
    let requests;
    try {
        requests = parseRequests(useDefaults ? DEFAULT_REQUEST_WORDS : words);
    } catch (error) {
        reject(error.message);
        return;
    }

    if (useDefaults) {
        for (const request of requests) {
            request.expect = DEFAULT_EXPECTATIONS[request.method];
        }
    }

    run(buildPipeName(editorProcessId), requests, hold);
}

main();
