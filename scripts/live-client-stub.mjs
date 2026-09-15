// 参照クライアントの実機動作確認を確かめるための、呼び出しの記録を作って返すだけのクライアント。
// 実物と同じ形の流れる記録を書き出し、実行器が読む項目——呼んだツール・乗った引数・返りの本文・
// 画像の数——を、そのとおりに埋めるか、1か所だけ違えて埋める。
// 実機のエディタもブリッジも参照クライアントも要らないので、常設の検査から走らせられる。

import process from "node:process";
import { CASES, named } from "./live-client-cases.mjs";

/** 返りの本文。実行器は空でないことだけを見る。 */
const SAID = "作った返りである。";

/** 呼び出しが通らなかったことを指す書き出し。ホスト側の実装が定める。 */
const REFUSED = "TOOL_OPERATION_FAILED: 作った断りである。";

/** 画像の塊に詰める中身。実行器は数だけを見る。 */
const DRAWN = "iVBORw0KGgo=";

/** 違え方の名前。実行器が見る項目ごとに1つずつ置く。 */
const BROKEN = ["call", "arguments", "result", "refused", "image.missing", "image.extra"];

const EXIT_INVALID_ARGUMENTS = 2;

/** 引数を読み分ける。実物と同じ引数も渡されるので、知らないものは読み飛ばす。 */
function parseArguments(args) {
    let broken = "";
    for (let at = 0; at < args.length; at++) {
        if (args[at] !== "--broken") {
            continue;
        }

        broken = args[at + 1] ?? "";
        if (broken !== "" && !BROKEN.includes(broken)) {
            return { error: "知らない違え方: " + broken };
        }
    }

    return { broken };
}

/** 呼び出しへ乗せる引数。違えるときは、要る引数のうち1つを落とす。 */
function input(one, broken) {
    const given = { ...one.arguments };
    const names = Object.keys(given);
    if (broken === "arguments" && names.length !== 0) {
        delete given[names[0]];
    }

    return given;
}

/** 返りの塊。本文と、画像を返すツールなら画像を1枚載せる。 */
function content(one, broken) {
    const blocks = [];
    if (broken === "result") {
        blocks.push({ type: "text", text: "" });
    } else {
        blocks.push({ type: "text", text: broken === "refused" ? REFUSED : SAID });
    }

    const drawn = one.image ? broken !== "image.missing" : broken === "image.extra";
    if (drawn) {
        blocks.push({ type: "image", source: { type: "base64", data: DRAWN } });
    }

    return blocks;
}

/** その違え方を当てる呼び出しか。違えるのは1か所だけなので、当たるのは1件に限る。 */
function applies(one, at, broken) {
    if (broken === "image.missing") {
        return one.image;
    }

    if (broken === "image.extra") {
        return !one.image && at === 0;
    }

    return at === 0;
}

function transcribe(broken) {
    const lines = [];
    // 呼び出しそのものを落とす形では、1件目を呼ばない。実行器はその1件を呼ばれていないものとして
    // 見る。
    const listed = broken === "call" ? CASES.slice(1) : CASES;
    for (const [at, one] of listed.entries()) {
        const id = "call-" + (at + 1);
        const wrong = applies(one, at, broken) ? broken : "";
        lines.push({
            type: "assistant",
            message: {
                content: [{ type: "tool_use", id, name: named(one.tool), input: input(one, wrong) }],
            },
        });
        lines.push({
            type: "user",
            message: {
                content: [
                    { type: "tool_result", tool_use_id: id, content: content(one, wrong) },
                ],
            },
        });
    }

    return lines.map((line) => JSON.stringify(line)).join("\n") + "\n";
}

const read = parseArguments(process.argv.slice(2));
if (read.error !== undefined) {
    console.error(read.error);
    process.exit(EXIT_INVALID_ARGUMENTS);
}

process.stdout.write(transcribe(read.broken));
