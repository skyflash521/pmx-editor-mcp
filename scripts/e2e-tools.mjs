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

/**
 * 答えの返らない検査が何件出たら、塞がりが解けていないと見なして実行を打ち切るか。表示を
 * 片付けても返らなかった1件は、応答待ちとその待ち直しで実行の時間の上限をほぼ使い切る。
 * 2件目を待っても、その実行が上限に収まる見込みはもう無い——待つ時間が伸びるだけである。
 * 表示を片付けて進めた検査はここに数えないので、片付く塞がりで実行が終わることはない。
 */
const STALLED_LIMIT = 1;

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
function judge(one, response, capture, remembered) {
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

    if (one.expect === "changed") {
        return envelope.ok === true
            ? changed(one.differs, envelope.value, remembered)
            : "読み比べるはずが断られました: " + describe(envelope);
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
 * 呼ぶ前に読んだものと違うか。覚えていない名前を指す検査は落とす——比べる相手が無いまま通ると、
 * 呼び出しが何も動かさなかった回も合格になる。
 */
function changed(name, value, remembered) {
    if (!remembered.has(name)) {
        return "呼ぶ前に読んだものを覚えていません: " + name;
    }

    const before = JSON.stringify(remembered.get(name));

    return before === JSON.stringify(value)
        ? "呼び出しの後に読めるものが、呼ぶ前と同じです: " + before.slice(0, 200)
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

/**
 * エディタが出している応答待ちの表示へ応答して閉じ、閉じたものの素性を返す。応答できない表示が
 * 残ったときは操作役が理由を述べて落ちるので、その文言を返す——答えるものが無かったときと同じ
 * 空の並びにすると、最も素性が要る場面で何も言えなくなる。
 */
function answerDialogs(processId) {
    const done = invokeControl([
        "-File", CONTROL_SCRIPT, "-Action", "answer", "-ProcessId", String(processId),
    ]);
    if (done.written === null) {
        return { answered: null, unavailable: done.unavailable };
    }

    return {
        answered: done.written.split("\n")
            .map((line) => line.trim())
            .filter((line) => line !== ""),
        unavailable: null,
    };
}

/** 表示へ何度まで続けて答えるか。1つ答えると次が出る作りがあるので、1度では足りない。 */
const ANSWERING_ROUNDS = 8;

/**
 * 出ている表示へ、出なくなるまで答える。答え切れたときだけ、答えた表示の素性を並びで返す。
 * 答え切れなかったときは null で、そのときは投げ直しても同じところで止まる。理由はそのまま
 * 呼んだ側へ渡す。
 */
function clearPrompts(processId) {
    const answered = [];
    for (let round = 0; round < ANSWERING_ROUNDS; round++) {
        const cleared = answerDialogs(processId);
        if (cleared.answered === null) {
            return { answered: null, unavailable: cleared.unavailable };
        }

        if (cleared.answered.length === 0) {
            return { answered, unavailable: null };
        }

        answered.push(...cleared.answered);
    }

    // 上限まで答えても出続けるなら、まだ出ている。答えられたことにすると、残った表示に
    // 続きの検査が巻き添えで落ちる。
    return {
        answered: null,
        unavailable: "上限まで答えても表示が出続けました: " + answered.join(" / "),
    };
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
 * 環境変数の名前を値へ広げる。広げられない名前があればその名前を投げる。検査が書き先に使う位置は
 * 走らせる機械ごとに変わるので、正本は名前で書き、ここで値にする。
 */
function expand(text) {
    return text.replace(/%([^%]+)%/g, (whole, name) => {
        const value = process.env[name];
        if (value === undefined) {
            throw new Error("環境変数が定義されていません: " + name);
        }

        return value;
    });
}

/** 文字列の中の環境変数を、組も並びもたどって広げた値。 */
function expanded(value) {
    if (typeof value === "string") {
        return expand(value);
    }

    if (Array.isArray(value)) {
        return value.map(expanded);
    }

    if (value !== null && typeof value === "object") {
        const filled = {};
        for (const [name, held] of Object.entries(value)) {
            filled[name] = expanded(held);
        }

        return filled;
    }

    return value;
}

/**
 * その検査が書くファイルの位置。書かない検査では null。呼ぶ前に置き場を用意して、そこに在る古い
 * ものを消す——置き場が無いことで断られると書けたかどうかを確かめられず、前の実行が残したものを
 * 残すと、何も書かない呼び出しでも在ることになる。
 */
function written(one, given) {
    if (one.writes === undefined) {
        return null;
    }

    const target = given[one.writes];
    fs.mkdirSync(path.dirname(target), { recursive: true });
    fs.rmSync(target, { force: true });

    return target;
}

/**
 * 返った値を覚えられるか。成功して値を載せた応答だけが覚える相手で、表示が出たことを知らせる
 * 断りのように値を持たない応答は覚えない——覚えると、借りる側が値の無いものを渡してしまう。
 */
function produced(response) {
    const envelope = response.result;

    return envelope !== null
        && envelope !== undefined
        && typeof envelope === "object"
        && !Array.isArray(envelope)
        && envelope.ok === true
        && envelope.value !== undefined;
}

/**
 * 借りる値を差し込んだ引数。差し込む先は引数の中の道で、斜線で区切った各段をたどり、たどり着いた
 * 位置へ覚えた値を置く。並びで受け取る引数は生成器が空きを1つ置き、道がその中を指す。借りる元も
 * 斜線で位置を指せる——応答を並びで返すツールは、出たハンドルもその並びの中へ入れるので、その中の
 * どれを借りるかを言う必要がある。借りる名前をまだ覚えていなければ null。
 */
function borrowing(one, remembered) {
    if (one.borrowed === undefined) {
        return one.arguments;
    }

    const given = JSON.parse(JSON.stringify(one.arguments));
    for (const [path, from] of Object.entries(one.borrowed)) {
        if (!remembered.has(from.split("/")[0])) {
            return null;
        }

        const taken = from.split("/");
        let value = remembered.get(taken[0]);
        for (let at = 1; at < taken.length; at++) {
            if (value === null || typeof value !== "object") {
                return null;
            }

            value = value[taken[at]];
        }

        const steps = path.split("/");
        let held = given;
        for (let at = 0; at < steps.length - 1; at++) {
            held = held[steps[at]];
        }

        held[steps[steps.length - 1]] = value;
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
    let waited = -1;
    let stalled = 0;
    let settled = false;
    const results = [];
    const remembered = new Map();
    let capture = null;
    let wrote = null;

    // いま投げている検査のために片付けた表示の素性。片付けた場所と結末を記録する場所が離れて
    // いるので、検査ごとにここへ溜めて結末へ渡す。
    let noted = [];

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
            noted = [];
            if (index >= cases.length) {
                finish();
                return;
            }

            const one = cases[index];
            if (one.expect === "viewImage" && capture === null) {
                capture = captureView(processId);
            }

            const given = expanded(borrowing(one, remembered));
            if (given === null) {
                results.push({
                    case: one,
                    reason: "借りる値をまだ覚えていません: " + JSON.stringify(one.borrowed),
                    stopped: noted,
                });
                next();

                return;
            }

            wrote = written(one, given);
            send(requestId(index), one.tool, given);
        };

        const finish = () => {
            const failed = results.filter((r) => r.reason !== null);
            for (const result of failed) {
                console.log(
                    "不合格: " + result.case.tool + " — " + result.case.purpose + " — " + result.reason);
            }

            // 表示で止まった検査も、呼び先までは届いているので合格に数える。合格の中で何件が
            // 表示で止まったのかを内訳として添える——合格から引くと、行キーごとの内訳が数える
            // 合格と食い違う。
            const stopped = results.filter(
                (r) => r.reason === null && Array.isArray(r.stopped) && r.stopped.length !== 0);
            if (stopped.length !== 0) {
                console.log("");
                console.log("表示で止まった検査: " + stopped.length + " 件");
                for (const result of stopped) {
                    console.log("  " + result.case.tool + " — " + result.case.purpose);
                    for (const note of result.stopped) {
                        console.log("    " + note);
                    }
                }
            }

            console.log("");
            console.log(
                "検査: " + results.length + " 件・合格 " + (results.length - failed.length) +
                "(うち表示で止まった " + stopped.length + ")・不合格 " + failed.length);
            report(results, "rowKey", "行キー");
            report(results, "editKind", "編集の流れ");
            report(results, "connectionPath", "接続の経路");

            settle(failed.length === 0 ? EXIT_SUCCESS : EXIT_FAILED, null);
        };

        socket.on("error", (error) => settle(EXIT_INPUT_UNAVAILABLE, "接続に失敗しました: " + error.message));
        // 応答が返らないのは、答えられない表示がUIスレッドを塞いでいるときである。表示を片付け
        // れば、塞がっていた呼び出しが終わって応答が返るので、投げ直さずにもう1回ぶん待つ——
        // 実行されたかどうかが分からない要求を投げ直すと、一度だけ頼んだ操作が二度実行されうる。
        // それでも返らなければ、その検査を不合格にして先へ進む。1件のために残りを見ないまま
        // 終えると、落ちた理由がどこにあるのかも分からなくなる。
        socket.on("timeout", () => {
            if (settled) {
                return;
            }

            const one = cases[index];
            if (one === undefined) {
                settle(EXIT_INPUT_UNAVAILABLE, "応答が時間内に返りませんでした。");

                return;
            }

            const cleared = clearPrompts(processId);
            noted.push(...(cleared.answered ?? []));
            if (waited !== index && cleared.answered !== null && cleared.answered.length > 0) {
                waited = index;
                socket.setTimeout(RESPONSE_TIMEOUT_MS);

                return;
            }

            results.push({
                case: one,
                reason: waited === index
                    ? "応答が時間内に返りませんでした。出ている表示を片付けても進みませんでした。"
                    : "応答が時間内に返りませんでした。" + (cleared.unavailable === null
                        ? "出ている表示は見つかりませんでした。"
                        : cleared.unavailable),
                stopped: noted,
            });

            stalled += 1;
            if (stalled >= STALLED_LIMIT) {
                console.error(
                    "応答の返らない検査が " + stalled + " 件続いたので、"
                        + one.tool + " で打ち切りました。");
                finish();

                return;
            }

            next();
        });
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

                // 先へ進んだあとに遅れて返った応答は、もう待っている相手が居ない。読み飛ばす
                // ——番号で取り違えないためにここで落とす。
                if (typeof response.id === "number" && response.id < requestId(index)) {
                    continue;
                }

                // 答えが届いたので、続けて答えの返らなかった数は数え直す。判定まで進まない
                // 断りも、ホストが答えたことに変わりはない。
                stalled = 0;

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
                const before = notStarted(response) && retried !== index
                    ? clearPrompts(processId)
                    : null;
                if (before !== null) {
                    noted.push(...(before.answered ?? []));
                }

                if (before !== null && before.answered !== null && before.answered.length > 0) {
                    retried = index;
                    send(requestId(index), one.tool, expanded(borrowing(one, remembered)));
                    continue;
                }

                // 片付かない表示を残したまま先へ進むと、後の検査が巻き添えで落ちる。
                if (prompted(response)) {
                    const cleared = clearPrompts(processId);
                    noted.push(...(cleared.answered ?? []));
                    if (cleared.answered === null) {
                        results.push({
                            case: one,
                            reason: "出ている表示を片付けられませんでした: " + cleared.unavailable,
                            stopped: noted,
                        });
                        next();

                        continue;
                    }
                }

                let reason = judge(one, response, capture, remembered);
                if (reason === null && wrote !== null && !fs.existsSync(wrote)) {
                    reason = "書いたはずのファイルがありません: " + wrote;
                }

                if (reason === null && one.produces !== undefined && produced(response)) {
                    remembered.set(one.produces, response.result.value);
                }

                results.push({ case: one, reason, stopped: noted });
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
