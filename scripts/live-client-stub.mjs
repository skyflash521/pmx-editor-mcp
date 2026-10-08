// 参照クライアントの実機動作確認を確かめるための、呼び出しの記録を作って返すだけのクライアント。
// 実物と同じ形の流れる記録を書き出し、実行器が読む項目——呼んだツール・乗った引数・返りの本文・
// 誤りの印・画像の数——を、そのとおりに埋めるか、1か所だけ違えて埋める。
// 実機のエディタもブリッジも参照クライアントも要らないので、常設の検査から走らせられる。

import process from "node:process";
import { CASES, named } from "./live-client-cases.mjs";

/** ブリッジがホストへ中継した返りの1行目。ブリッジの実装が定める。 */
const REACHED = "接続先: pmx-editor-mcp-1";

/** 返りの本文。ブリッジが中継した形で、1行目に接続先を名乗る。 */
const SAID = REACHED + "\n作った返りである。";

/** 呼び出しがホストまで届いたうえで通らなかった返り。書き出しはホスト側の実装が定める。 */
const REFUSED = REACHED + "\nTOOL_OPERATION_FAILED: 作った断りである。";

/**
 * 呼び出しがサーバーへ届く前に参照クライアントの側で断られた返り。本文は塊の並びでなく文字列で
 * 載せ、接続先を名乗らない。誤りの印を付ける。
 */
const UNREACHED = "<tool_use_error>作ったクライアント側の断りである。</tool_use_error>";

/** 画像の塊に詰める中身。実行器は数だけを見る。 */
const DRAWN = "iVBORw0KGgo=";

/** 違え方の名前。実行器が見る項目ごとに1つずつ置く。 */
const BROKEN = [
    "call", "arguments", "result", "refused", "image.missing", "image.extra", "unreached", "isError", "ended",
];

/** 許した応答の数を使い切って終わったことを指す、結果の行の印。実物の終わり方と同じ。 */
const MAX_TURNS_REACHED = "error_max_turns";

/** 応答の数を使い切って終わったときの終了コード。実物と同じ。 */
const EXIT_MAX_TURNS = 1;

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
    if (broken === "unreached") {
        return UNREACHED;
    }

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

/** 返りに誤りの印を付けるか。通らなかった返りには、参照クライアントが印を付ける。 */
function failed(broken) {
    return broken === "refused" || broken === "unreached" || broken === "isError";
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
                    {
                        type: "tool_result",
                        tool_use_id: id,
                        content: content(one, wrong),
                        is_error: failed(wrong),
                    },
                ],
            },
        });
    }

    lines.push({ type: "result", subtype: broken === "ended" ? "error_during_execution" : MAX_TURNS_REACHED });

    return lines.map((line) => JSON.stringify(line)).join("\n") + "\n";
}

const read = parseArguments(process.argv.slice(2));
if (read.error !== undefined) {
    console.error(read.error);
    process.exit(EXIT_INVALID_ARGUMENTS);
}

process.stdout.write(transcribe(read.broken));
process.exitCode = EXIT_MAX_TURNS;
