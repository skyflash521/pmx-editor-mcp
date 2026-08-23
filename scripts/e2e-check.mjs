// 実機動作確認の確認クライアント。
// 常駐ホストが待ち受ける名前付きパイプへ接続し、要求を1件ずつ送って応答を表示する。
// 接続先・要求の並び・要求と応答の符号化と区切りは、いずれもホスト側の実装が定める。

import net from "node:net";
import process from "node:process";

/** 接続先の名前付きパイプ名。ホスト側の実装が定める。 */
const PIPE_NAME = "";

/** 引数を省略したときに送る要求の並び。ホスト側の実装が定める。 */
const DEFAULT_REQUESTS = [];

/**
 * 要求の文字列を、送信するバイト列へ変換する。ホスト側の実装が定める。
 * 戻り値は Buffer。変換できない要求を渡されたときは例外を投げる。
 */
const encodeRequest = null;

/**
 * 受信済みのバイト列の先頭から、request に対する応答を1件取り出す。
 * ホスト側の実装が定める。buffer は Buffer、request は応答を待っている要求の文字列。
 * 取り出せたときは、表示する文字列を持つ text と、残りのバイト列(Buffer)を持つ rest の
 * 2つのプロパティからなるオブジェクトを返す。1件に満たないときは null を返す。
 * 1件ぶん揃っているが壊れている場合、および request に対する応答でない場合は例外を投げる。
 */
const takeResponse = null;

/** 接続待ち・応答待ちのそれぞれに与える無通信の上限。 */
const TIMEOUT_MS = 15000;

const EXIT_OK = 0;
const EXIT_ERROR = 1;
const EXIT_TIMEOUT = 2;

function toPipePath(name) {
    return "\\\\.\\pipe\\" + name;
}

function run(pipeName, requests) {
    const socket = net.connect(toPipePath(pipeName));
    let connected = false;
    let complete = false;
    let settled = false;
    let buffer = Buffer.alloc(0);
    let index = 0;

    // 応答の表示が欠けないよう、強制終了ではなく終了コードの設定で結果を確定させる。
    // ソケットの後始末は確定と切り離し、時間切れがいつでも破棄できるようにする。
    function settle(code, message) {
        if (settled) {
            return;
        }
        settled = true;
        if (message !== null) {
            console.error(message);
        }
        process.exitCode = code;
    }

    function abort(message) {
        settle(EXIT_ERROR, message);
        socket.destroy();
    }

    // ホスト側の実装が投げた例外で終了処理を飛ばさないよう、呼び出しをここで包む。
    function send(request) {
        try {
            socket.write(encodeRequest(request));
        } catch (error) {
            abort("要求を送信用の形式へ変換できませんでした: " + error.message);
        }
    }

    socket.setTimeout(TIMEOUT_MS, () => {
        if (complete) {
            // 応答は揃っており、相手が接続を閉じないだけ。
            settle(EXIT_OK, null);
        } else if (connected) {
            settle(EXIT_TIMEOUT, "応答が時間内に返りませんでした。");
        } else {
            settle(EXIT_TIMEOUT, "パイプへ時間内に接続できませんでした。");
        }
        socket.destroy();
    });

    socket.on("connect", () => {
        connected = true;
        console.log("接続しました: " + pipeName);
        send(requests[index]);
    });

    socket.on("data", (chunk) => {
        if (settled) {
            return;
        }
        if (complete) {
            abort("最後の応答の後に予期しないデータが届きました。");
            return;
        }
        buffer = Buffer.concat([buffer, chunk]);

        while (index < requests.length) {
            let taken;
            try {
                taken = takeResponse(buffer, requests[index]);
            } catch (error) {
                abort("応答を取り出せませんでした: " + error.message);
                return;
            }
            if (taken === null) {
                break;
            }
            console.log(requests[index] + " -> " + taken.text);
            buffer = taken.rest;
            index += 1;
            if (index >= requests.length) {
                break;
            }

            // 次の要求はまだ送っていないので、ここに残るデータは対応する要求を持たない。
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
            return;
        }
        if (buffer.length > 0) {
            abort("最後の応答の後に予期しないデータが届きました。");
            return;
        }
        complete = true;
        socket.end();
    });

    socket.on("error", (error) => {
        abort("接続または送受信に失敗しました: " + error.message);
    });

    socket.on("close", () => {
        if (complete) {
            settle(EXIT_OK, null);
            return;
        }
        settle(EXIT_ERROR, "応答を受け取る前に切断されました。");
    });
}

function main() {
    if (PIPE_NAME === "") {
        console.error("接続先のパイプ名が設定されていません。");
        process.exitCode = EXIT_ERROR;
        return;
    }
    if (encodeRequest === null || takeResponse === null) {
        console.error("要求と応答の符号化・区切りの扱いが設定されていません。");
        process.exitCode = EXIT_ERROR;
        return;
    }

    const requests = process.argv.slice(2);
    if (requests.length === 0) {
        requests.push(...DEFAULT_REQUESTS);
    }
    if (requests.length === 0) {
        console.error("送る要求がありません。要求をコマンドライン引数で指定してください。");
        process.exitCode = EXIT_ERROR;
        return;
    }

    run(PIPE_NAME, requests);
}

main();
