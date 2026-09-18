// 自動E2E検査の実行器。
// 生成器が書き出した検査を、MCPサーバーとして起こしたブリッジへ1件ずつ投げ、結果を
// 行キー・編集の流れ・接続の経路ごとに数えて出す。ホストの待受へ直に繋がないのは、クライアントが
// 通る経路をそのまま通すためである——ブリッジが公開していないツールは、直に繋ぐと通ってしまう。
// 検査の中身はこの実行器が決めず、生成器が書いたものだけを読む。

import { spawnSync } from "node:child_process";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import process from "node:process";
import url from "node:url";
import { McpClient } from "./mcp-client.mjs";

/** ブリッジが本文の先頭へ置く、接続先の名乗りの書き出し。中継した応答だけがこれを持つ。 */
const TARGET_PREFIX = "接続先: ";

/** 接続先が前の呼び出しから変わったときの名乗りの書き出し。 */
const TARGET_CHANGED_PREFIX = "接続先が変わった: ";

/** ブリッジが本文の末尾へ足す警告の行の頭。 */
const WARNING_PREFIX = "警告: ";

/** 中継の状態を返すツールの名前。ホストが受け持ち、ブリッジが固定のツールとして公開する。 */
const STATUS_TOOL = "sdk_status";

/** ホストが受け持ち、ブリッジが固定のツールとして公開する名前。突き合わせでは両側から引く。 */
const FIXED_TOOLS = ["ping", STATUS_TOOL];

/** 検査からだけ使う入口が開いているときだけ在るツールの名前の頭。突き合わせでは両側から引く。 */
const DEBUG_PREFIX = "debug_";

/**
 * 読み込む中身が要るツールへ渡す形状。頂点3つと面1つと材質1つだけを持つ、文字で書いた
 * DirectXの形である。材質を宣言しないと、読み取る側が材質の並びを持たないまま添字で引く。
 * 書き出す手立てがエディタに無いので、読める最小のものをここで組む。
 */
const MESH = [
    "xof 0303txt 0032",
    "",
    "Mesh {",
    " 3;",
    " 0.000000;0.000000;0.000000;,",
    " 1.000000;0.000000;0.000000;,",
    " 0.000000;1.000000;0.000000;;",
    " 1;",
    " 3;0,1,2;;",
    "",
    " MeshMaterialList {",
    "  1;",
    "  1;",
    "  0;",
    "  Material {",
    "   1.000000;1.000000;1.000000;1.000000;;",
    "   0.000000;",
    "   0.000000;0.000000;0.000000;;",
    "   0.000000;0.000000;0.000000;;",
    "  }",
    " }",
    "}",
    "",
].join("\r\n");

/**
 * 読み込む中身が要るツールへ渡すモーション。表示とIKのキーを1つだけ持つ。綴りと並びはVMDの形が
 * 定めるもので、書き出す手立てがエディタのSDKに無いのでここで組む。
 */
function motion() {
    const head = Buffer.alloc(30);
    head.write("Vocaloid Motion Data 0002", 0, "latin1");
    const named = Buffer.alloc(20);
    named.write("e2e", 0, "latin1");
    const counts = Buffer.alloc(24);
    counts.writeUInt32LE(1, 20);
    const key = Buffer.alloc(9);
    key.writeUInt32LE(0, 0);
    key.writeUInt8(1, 4);
    key.writeUInt32LE(0, 5);

    return Buffer.concat([head, named, counts, key]);
}

/**
 * 読み込む中身が要るツールへ渡すポーズ。持つ骨を0件にしてあるので、開いているモデルの骨の名前に
 * 依らない。書き出す手立てがエディタのSDKに無いのでここで組む。
 */
const POSE = [
    "Vocaloid Pose Data file",
    "",
    "e2e.osm;",
    "0;",
    "",
].join("\r\n");

/**
 * 検査が書く先の置き場。走るたびに作り直して終わりに消すので、前の実行が残したものが在ることに
 * ならない。正本はこの名前で書き先を綴り、値はここで決まる——走らせる機械ごとに位置が変わる。
 */
const TEMPORARY_PLACE = path.join(os.tmpdir(), "pmx-editor-mcp-e2e");

process.env.PMX_EDITOR_MCP_E2E_TEMP = TEMPORARY_PLACE;

/**
 * 答えの返らない検査が何件出たら、塞がりが解けていないと見なして実行を打ち切るか。表示を
 * 片付けても返らなかった1件は、応答待ちとその待ち直しで実行の時間の上限をほぼ使い切る。
 * 2件目を待っても、その実行が上限に収まる見込みはもう無い——待つ時間が伸びるだけである。
 * 表示を片付けて進めた検査はここに数えないので、片付く塞がりで実行が終わることはない。
 */
const STALLED_LIMIT = 1;

/** この実行器と同じ置き場にある道具を指す。 */
function beside(name) {
    return path.join(path.dirname(url.fileURLToPath(import.meta.url)), name);
}

/**
 * 応答待ちの表示へ応答する操作役。画面を触るのはこの1本に寄せる。実機のエディタを相手にしない
 * 実行では代わりを差し替える——この実行器そのものを確かめる検査が、画面も実機も無しで走る。
 */
let CONTROL_SCRIPT = beside("host-control.ps1");

/** 呼び出しを始めていないことを表す断りの綴り。共通契約が定める。 */
const NOT_STARTED = "TOOL_NOT_STARTED";

/** 人の応答を待つ表示が出ていて進められないことを表す断りの綴り。共通契約が定める。 */
const PROMPT_SHOWN = "TOOL_PROMPT_SHOWN";

/** 同じビューを写した2枚と見なす明るさの差の上限。 */
const MATCHING_IMAGE_LIMIT = 0.1;

/** ビューの写しと画像を見比べるスクリプト。 */
let COMPARE_SCRIPT = beside("compare-view-image.ps1");

const EXIT_SUCCESS = 0;
const EXIT_FAILED = 1;
const EXIT_INVALID_ARGUMENTS = 2;
const EXIT_INPUT_UNAVAILABLE = 3;

/** 指した行に当たる検査を引けず、絞った実行ではその行を確かめられないことを表す。 */
const EXIT_ROWS_UNCOVERED = 4;

/**
 * 導入の前置を行い、MCPサーバーとして起こす相手を受け取る。前置が何をするかはこの実行器の
 * 知るところではなく、最後の行へ置いた組だけを読む。
 */
function prepare(setup, setupArgs) {
    const done = invokeControl(["-File", setup, "-Action", "prepare", ...setupArgs]);
    if (done.written === null) {
        return { server: null, unavailable: done.unavailable };
    }

    const lines = done.written.split("\n").map((line) => line.trim())
        .filter((line) => line !== "");
    if (lines.length === 0) {
        return { server: null, unavailable: setup + " が起こす相手を書きませんでした。" };
    }

    let server;
    try {
        server = JSON.parse(lines[lines.length - 1]);
    } catch (error) {
        return {
            server: null,
            unavailable: setup + " が書いた相手を読み解けません: " + error.message,
        };
    }

    return server === null || typeof server !== "object" || typeof server.command !== "string"
            || !Array.isArray(server.arguments)
        ? {
            server: null,
            unavailable: setup + " が書いた相手が command と arguments の組ではありません。",
        }
        : { server, unavailable: null };
}

/**
 * ブリッジが返したツールの結果から、ホストの包みを組み直す。結末の判定はどれも包みの形を見るので、
 * 戻すのはここ1か所にする。接続先の名乗りも包みの警告も、本文の中での位置ではなく行の書き出しで
 * 見分ける。
 */
function envelopeOf(said) {
    // 名乗りは書き出しで見分ける。1行目と決め打つと、ブリッジ自身が返す誤り——ホストへ繋げない・
    // 応答が返らないなど、名乗る接続先を持たない誤り——の本文を丸ごと捨てて、落ちた理由が残らない。
    let text = said.text;
    if (text.startsWith(TARGET_PREFIX) || text.startsWith(TARGET_CHANGED_PREFIX)) {
        const at = text.indexOf("\n");
        text = at < 0 ? "" : text.slice(at + 1);
    }

    // 警告も書き出しで見分ける。末尾から剥がすと、値の行を持たない画像のツールで警告を値と
    // 読み違える。
    const lines = text.split("\n");
    const warnings = lines.filter((line) => line.startsWith(WARNING_PREFIX))
        .map((line) => line.slice(WARNING_PREFIX.length));
    const body = lines.filter((line) => !line.startsWith(WARNING_PREFIX)).join("\n");
    if (said.isError) {
        const at = body.indexOf(": ");

        return {
            ok: false,
            error: {
                code: at < 0 ? body : body.slice(0, at),
                message: at < 0 ? "" : body.slice(at + 2),
            },
            warnings,
        };
    }

    // 画像を返すツールの値は本文でなく画像の塊で届く。文字の本文から読むと、文字列で返して
    // しまっていても気づけない。
    if (said.images.length !== 0) {
        return { ok: true, value: said.images[0].data, warnings };
    }

    try {
        return { ok: true, value: JSON.parse(body === "" ? "null" : body), warnings };
    } catch (error) {
        return {
            ok: false,
            error: { code: "TOOL_BROKEN_RESPONSE", message: "値を読み解けません: " + body },
            warnings,
        };
    }
}

/**
 * ツールを1件呼び、包みの形へ戻して返す。答えが時間内に返らなければ <code>response</code> が
 * null、契約から外れた応答なら <code>broken</code> にその事情が入る。
 */
async function ask(client, tool, given, extend = null) {
    try {
        return {
            response: { result: envelopeOf(await client.callTool(tool, given, extend)) },
            broken: null,
        };
    } catch (error) {
        return { response: null, broken: error.timedOut === true ? null : error.message };
    }
}

/** 中継の状態。読み解けなければその事情を返す。 */
async function askStatus(client) {
    const said = await ask(client, STATUS_TOOL, {});
    if (said.response === null) {
        return {
            status: null,
            unavailable: said.broken === null
                ? STATUS_TOOL + " の応答が時間内に返りませんでした。"
                : said.broken,
        };
    }

    const envelope = said.response.result;
    if (envelope.ok !== true || envelope.value === null || typeof envelope.value !== "object") {
        return { status: null, unavailable: STATUS_TOOL + " が状態を返しませんでした。" };
    }

    return { status: envelope.value, unavailable: null };
}

/** 突き合わせに使う名前。固定のツールと、検査からだけ使う入口のツールを引いたものである。 */
function measured(names) {
    return [...names]
        .filter((name) => !FIXED_TOOLS.includes(name) && !name.startsWith(DEBUG_PREFIX))
        .sort();
}

/** 左に在って右に無い名前。 */
function lacking(left, right) {
    const held = new Set(right);

    return left.filter((name) => !held.has(name));
}

/**
 * 走らせる前に、スキーマ正本・ブリッジが公開するツール・ホストが答える名前の3つが同じ集合である
 * ことを確かめる。合っていれば null。ずれていれば、そのずれは呼んでみるまで分からない——呼ばない
 * ツールのずれは、どの検査も落とさないまま残る。
 */
async function agreed(client, schemasPath) {
    let authored;
    try {
        const read = JSON.parse(fs.readFileSync(schemasPath, "utf8"));
        authored = measured(read.tools.map((tool) => tool.tool));
    } catch (error) {
        return "スキーマ正本を読めません(" + schemasPath + "): " + error.message;
    }

    let published;
    try {
        published = measured(await client.listTools());
    } catch (error) {
        return "ブリッジのツールを数えられません: " + error.message;
    }

    const said = await askStatus(client);
    if (said.status === null) {
        return "ホストが答える名前を数えられません: " + said.unavailable;
    }

    const answered = measured(said.status.toolNames ?? []);
    const differences = [
        ["スキーマ正本に在ってブリッジが公開しない", lacking(authored, published)],
        ["ブリッジが公開してスキーマ正本に無い", lacking(published, authored)],
        ["スキーマ正本に在ってホストが答えない", lacking(authored, answered)],
        ["ホストが答えてスキーマ正本に無い", lacking(answered, authored)],
    ].filter((pair) => pair[1].length !== 0);
    if (differences.length === 0) {
        console.log(
            "名前の集合が合っています: スキーマ正本 " + authored.length + " 件・ブリッジ "
                + published.length + " 件・ホスト " + answered.length + " 件");

        return null;
    }

    return differences
        .map((pair) => pair[0] + "名前が " + pair[1].length + " 件: " + pair[1].join("・"))
        .join("\n");
}

/** 走らせた後に、中継を作れなかった行も無効にした行も残っていないことを確かめる。 */
async function settled(client) {
    const said = await askStatus(client);
    if (said.status === null) {
        return "走らせた後の状態を読めません: " + said.unavailable;
    }

    const left = [
        ["中継を作れなかった行", said.status.unresolvedRows ?? []],
        ["呼び出しの失敗で無効にした行", said.status.disabledRows ?? []],
    ].filter((pair) => pair[1].length !== 0);

    return left.length === 0
        ? null
        : left.map((pair) => pair[0] + "が " + pair[1].length + " 件: " + pair[1].join("・"))
            .join("\n");
}

/**
 * 1件の検査の結末。合っていれば null、違っていればその理由を返す。
 * 包みの形は共通契約が定めるので、ここでは成功・失敗と理由の綴りだけを見る。
 */
function judge(one, response, remembered) {
    const envelope = response.result;

    if (one.expect === "success") {
        return envelope.ok === true ? null : "成功するはずが断られました: " + describe(envelope);
    }

    if (one.expect === "called") {
        if (envelope.ok === true) {
            return null;
        }

        return envelope.error !== undefined && envelope.error.code === PROMPT_SHOWN
            ? "確認の表示で止まりました。表示が求めるものを段取りで先に満たしてください: "
                + describe(envelope)
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
 * 指したビューの写しを1回の呼び出しでまとめて取り、ビューの名前から写しへの表を返す。取れな
 * かったときは、どのビューも同じ事情を持つ——操作役は1つでも撮れなければ落ちる。
 */
function captureViews(processId, views) {
    const places = views.map(
        (view) => path.join(os.tmpdir(), "pmx-editor-mcp-view-" + view + ".png"));
    const done = invokeListed(CONTROL_SCRIPT, [
        ["Action", "capture"], ["ProcessId", String(processId)],
        ["View", views], ["Path", places],
    ]);

    const taken = new Map();
    for (let at = 0; at < views.length; at++) {
        taken.set(views[at], done.written === null
            ? { path: null, unavailable: done.unavailable }
            : { path: places[at], unavailable: null });
    }

    return taken;
}

/**
 * 写しと画像の明るさの差を、組の並びの順に返す。比べられなければその事情を返す。
 */
function differences(pairs) {
    const done = invokeListed(COMPARE_SCRIPT, [
        ["Reference", pairs.map((pair) => pair.reference)],
        ["Candidate", pairs.map((pair) => pair.candidate)],
    ]);
    if (done.written === null) {
        return { measured: null, unavailable: done.unavailable };
    }

    const lines = done.written.split("\n").map((line) => line.trim())
        .filter((line) => line !== "");
    if (lines.length !== pairs.length) {
        return {
            measured: null,
            unavailable: "明るさの差が " + pairs.length + " 個ではなく " + lines.length + " 個です。",
        };
    }

    const measured = lines.map((line) => Number.parseFloat(line));

    return measured.every((one) => Number.isFinite(one))
        ? { measured, unavailable: null }
        : { measured: null, unavailable: "明るさの差を数として読めません: " + done.written };
}

/**
 * 返った画像を、その行が名乗るビューの写しと見比べる組として控える。画像が返っていなければ
 * その場で理由を返す。見比べるのは走り切ってからで、控えた組をまとめて1回で測る。
 */
function heldImage(one, response, capture, held) {
    if (capture === null || capture.path === null) {
        return one.view
            + " のビューを写し取れませんでした: "
            + (capture === null ? "写しを撮っていません。" : capture.unavailable);
    }

    const envelope = response.result;
    if (envelope === null || typeof envelope !== "object" || envelope.ok !== true) {
        return "画像が返りませんでした: " + JSON.stringify(response).slice(0, 200);
    }
    if (typeof envelope.value !== "string" || envelope.value.length === 0) {
        return "画像が文字列で返りませんでした。";
    }

    const candidate = path.join(os.tmpdir(), "pmx-editor-mcp-view-" + held.length + ".b64");
    fs.writeFileSync(candidate, envelope.value, "utf8");
    held.push({ view: one.view, reference: capture.path, candidate, outcome: null });

    return null;
}

/**
 * 控えた組をまとめて見比べ、合わなかった行へ理由を書き入れる。測れなかったときは、控えた行を
 * すべて落とす——測れていない行を合格のまま残すと、見比べていない実行が通ってしまう。
 */
function settleImages(held) {
    if (held.length === 0) { return; }

    const compared = differences(held);
    for (let at = 0; at < held.length; at++) {
        if (compared.measured === null) {
            held[at].outcome.reason = "画像を写しと見比べられませんでした: " + compared.unavailable;
            continue;
        }

        if (compared.measured[at] > MATCHING_IMAGE_LIMIT) {
            held[at].outcome.reason = held[at].view
                + " のビューの姿と合いません(明るさの差 " + compared.measured[at] + ")。";
        }
    }
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
 * 2つの値が同じものか。並びと組は中身をたどって比べる——応答は綴りを解いて作り直した値なので、
 * 同じ中身でも別の実体になる。
 */
function same(one, other) {
    if (Array.isArray(one) || Array.isArray(other)) {
        return Array.isArray(one) && Array.isArray(other)
            && one.length === other.length
            && one.every((value, at) => same(value, other[at]));
    }

    if (one !== null && other !== null
        && typeof one === "object" && typeof other === "object") {
        const names = Object.keys(one);

        return names.length === Object.keys(other).length
            && names.every((name) =>
                Object.prototype.hasOwnProperty.call(other, name)
                && same(one[name], other[name]));
    }

    return one === other;
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
        if (!same(item[expected.member], expected.value)) {
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
 * PowerShellのスクリプトを起こし、書き出したものと、落ちたときの事情を返す。
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
 * 並びを渡す相手を、PowerShellの式として起こす。-File で起こすと引数はどれも文字列1つとして
 * 渡るので、読点で並べても1つの値になってしまう。値は引用符で括り、引用符そのものは重ねて逃がす。
 */
function invokeListed(script, named) {
    const quoted = (one) => "'" + String(one).replace(/'/g, "''") + "'";
    const parts = [];
    for (const [name, value] of named) {
        parts.push("-" + name);
        parts.push(Array.isArray(value) ? value.map(quoted).join(",") : quoted(value));
    }

    return invokeControl(["-Command", "& " + quoted(script) + " " + parts.join(" ")]);
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

/**
 * 検査を1件ずつブリッジへ投げ、結末を数えて終了コードを返す。ホストが契約から外れた応答を返した
 * ときだけは、数える前に打ち切る——どの検査の結末も信じられない。
 */
async function run(client, cases, processId) {
    const results = [];
    const remembered = new Map();
    const held = [];
    let captures = null;
    let stalled = 0;
    let broke = null;

    // 写しを撮るビューは、走らせる前に出揃っている。
    const shooting = [...new Set(
        cases.filter((one) => one.expect === "viewImage").map((one) => one.view))];

    for (const one of cases) {
        const startedAt = Date.now();
        const noted = [];
        const elapsed = () => (Date.now() - startedAt) / 1000;

        if (one.expect === "viewImage" && captures === null) {
            captures = captureViews(processId, shooting);
        }

        const given = expanded(borrowing(one, remembered));
        if (given === null) {
            results.push({
                case: one,
                reason: "借りる値をまだ覚えていません: " + JSON.stringify(one.borrowed),
                stopped: noted,
                seconds: elapsed(),
            });

            continue;
        }

        const wrote = written(one, given);

        // 応答が返らないのは、答えられない表示がUIスレッドを塞いでいるときである。表示を片付け
        // れば、塞がっていた呼び出しが終わって応答が返るので、投げ直さずにもう1回ぶん待つ——
        // 実行されたかどうかが分からない要求を投げ直すと、一度だけ頼んだ操作が二度実行されうる。
        let asked = false;
        let cleared = null;
        const extend = () => {
            if (asked) {
                return false;
            }

            asked = true;
            cleared = clearPrompts(processId);
            noted.push(...(cleared.answered ?? []));

            return cleared.answered !== null && cleared.answered.length > 0;
        };

        let said = await ask(client, one.tool, given, extend);
        if (said.broken !== null) {
            broke = said.broken;
            break;
        }

        if (said.response === null) {
            const waited = cleared !== null && cleared.answered !== null
                && cleared.answered.length > 0;
            results.push({
                case: one,
                reason: waited
                    ? "応答が時間内に返りませんでした。出ている表示を片付けても進みませんでした。"
                    : "応答が時間内に返りませんでした。"
                        + (cleared === null || cleared.unavailable === null
                            ? "出ている表示は見つかりませんでした。"
                            : cleared.unavailable),
                stopped: noted,
                seconds: elapsed(),
            });

            stalled += 1;
            if (stalled >= STALLED_LIMIT) {
                console.error(
                    "応答の返らない検査が " + stalled + " 件続いたので、"
                        + one.tool + " で打ち切りました。");
                break;
            }

            continue;
        }

        // 答えが届いたので、続けて答えの返らなかった数は数え直す。判定まで進まない断りも、
        // ホストが答えたことに変わりはない。
        stalled = 0;
        let response = said.response;
        if (notStarted(response)) {
            const before = clearPrompts(processId);
            noted.push(...(before.answered ?? []));
            if (before.answered !== null && before.answered.length > 0) {
                said = await ask(client, one.tool, expanded(borrowing(one, remembered)));
                if (said.broken !== null) {
                    broke = said.broken;
                    break;
                }

                if (said.response !== null) {
                    response = said.response;
                }
            }
        }

        // 片付かない表示を残したまま先へ進むと、後の検査が巻き添えで落ちる。
        if (prompted(response)) {
            const after = clearPrompts(processId);
            noted.push(...(after.answered ?? []));
            if (after.answered === null) {
                results.push({
                    case: one,
                    reason: "出ている表示を片付けられませんでした: " + after.unavailable,
                    stopped: noted,
                    seconds: elapsed(),
                });

                continue;
            }
        }

        const holding = held.length;
        let reason = one.expect === "viewImage"
            ? heldImage(one, response, captures?.get(one.view) ?? null, held)
            : judge(one, response, remembered);
        if (reason === null && wrote !== null && !fs.existsSync(wrote)) {
            reason = "書いたはずのファイルがありません: " + wrote;
        }

        if (reason === null && one.produces !== undefined && produced(response)) {
            remembered.set(one.produces, response.result.value);
        }

        const outcome = { case: one, reason, stopped: noted, seconds: elapsed() };
        results.push(outcome);

        // 控えた組は、走り切ってから測った理由をこの結末へ書き入れる。
        if (held.length > holding) { held[held.length - 1].outcome = outcome; }
    }

    if (broke !== null) {
        console.error(broke);

        return EXIT_INPUT_UNAVAILABLE;
    }

    settleImages(held);

    return finish(results, cases, await settled(client));
}

/** 結末を数えて出し、終了コードを返す。 */
function finish(results, cases, left) {
    const failed = results.filter((r) => r.reason !== null);

    const told = process.env.PMX_EDITOR_MCP_FELL_PATH;
    if (told) {
        fs.writeFileSync(told, failed.map((r) => cases.indexOf(r.case)).join(","), "utf8");
    }

    const ranTold = process.env.PMX_EDITOR_MCP_RAN_PATH;
    if (ranTold) {
        fs.writeFileSync(ranTold, results.map((r) => cases.indexOf(r.case)).join(","), "utf8");
    }
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
    console.log("1件ごとの所要(長い順):");
    for (const result of [...results].sort((a, b) => (b.seconds ?? 0) - (a.seconds ?? 0))) {
        console.log(
            "  " + (result.seconds ?? 0).toFixed(2) + "秒  " + result.case.tool +
            " — " + result.case.purpose);
    }

    console.log("");
    console.log(
        "検査: " + results.length + " 件・合格 " + (results.length - failed.length) +
        "(うち表示で止まった " + stopped.length + ")・不合格 " + failed.length);
    report(results, "rowKey", "行キー");
    report(results, "editKind", "編集の流れ");
    report(results, "connectionPath", "接続の経路");

    if (left !== null) {
        console.log("");
        console.log("走らせた後の中継の状態に残りがあります:");
        console.log(left);

        return EXIT_FAILED;
    }

    return failed.length === 0 ? EXIT_SUCCESS : EXIT_FAILED;
}

function readCases(path) {
    const text = fs.readFileSync(path, "utf8");
    const read = JSON.parse(text);
    if (read === null || typeof read !== "object" || !Array.isArray(read.cases)) {
        throw new Error("cases の並びを持たない。");
    }

    return read.cases;
}

/**
 * 指した行の検査と、それが借りる値を出す検査を残す。残した検査の並びは元のままなので、出す側は
 * 借りる側より先に来る。どの検査にも当たらない行は、引き方が壊れているので名前を挙げて返す。
 */
function only(cases, rows) {
    const wanted = new Set(rows);
    const source = new Map(
        cases.filter((one) => one.produces !== undefined).map((one) => [one.produces, one]));
    const queue = cases.filter((one) => wanted.has(one.rowKey));
    const taken = new Set(queue);
    while (queue.length !== 0) {
        const one = queue.pop();
        for (const from of Object.values(one.borrowed ?? {})) {
            const produced = source.get(from.split("/")[0]);
            if (produced !== undefined && !taken.has(produced)) {
                taken.add(produced);
                queue.push(produced);
            }
        }
    }

    const kept = cases.filter((one) => taken.has(one));
    const held = new Set(kept.map((one) => one.rowKey));

    return { kept, missing: [...wanted].filter((key) => !held.has(key)) };
}

const given = process.argv.slice(2);
const named = { "--control": null, "--compare": null, "--rows": null, "--setup": null,
    "--schemas": null };
const setupArgs = [];
const loose = [];
for (let at = 0; at < given.length; at++) {
    if (given[at] === "--setup-arg") {
        setupArgs.push(given[at + 1]);
        at += 1;
        continue;
    }

    if (!Object.prototype.hasOwnProperty.call(named, given[at])) {
        loose.push(given[at]);
        continue;
    }

    named[given[at]] = given[at + 1];
    at += 1;
}

const [processId, casesPath] = loose;
if (processId === undefined || casesPath === undefined
    || Object.values(named).some((value) => value === undefined)
    || setupArgs.some((value) => value === undefined)) {
    console.error(
        "使い方: node e2e-tools.mjs <エディタのプロセスID> <検査のパス>"
            + " [--control <操作役のパス>] [--compare <見比べるスクリプトのパス>]"
            + " [--rows <走らせる行のキーを並べたパス>] [--setup <前置のパス>]"
            + " [--setup-arg <前置へ渡す引数>] [--schemas <スキーマ正本のパス>]");
    process.exit(EXIT_INVALID_ARGUMENTS);
}

if (named["--control"] !== null) {
    CONTROL_SCRIPT = named["--control"];
}

if (named["--compare"] !== null) {
    COMPARE_SCRIPT = named["--compare"];
}

let cases;
try {
    cases = readCases(casesPath);
} catch (error) {
    console.error("検査を読めません(" + casesPath + "): " + error.message);
    process.exit(EXIT_INPUT_UNAVAILABLE);
}

if (named["--rows"] !== null) {
    let rows;
    try {
        rows = fs.readFileSync(named["--rows"], "utf8")
            .split("\n").map((line) => line.trim()).filter((line) => line !== "");
    } catch (error) {
        console.error("走らせる行を読めません(" + named["--rows"] + "): " + error.message);
        process.exit(EXIT_INPUT_UNAVAILABLE);
    }

    const chosen = only(cases, rows);
    if (chosen.missing.length !== 0) {
        console.error(
            "どの検査にも当たらない行がある: " + chosen.missing.join("・"));
        console.error("この行は絞った実行では確かめられない。絞らずに走らせること。");
        process.exit(EXIT_ROWS_UNCOVERED);
    }

    console.log(
        "指した行だけを走らせる: 行 " + rows.length + " 件・検査 " + chosen.kept.length + " 件");
    cases = chosen.kept;
}

if (cases.length === 0) {
    console.log("検査が1件も無い。");
    process.exit(EXIT_SUCCESS);
}

fs.rmSync(TEMPORARY_PLACE, { recursive: true, force: true });
fs.mkdirSync(TEMPORARY_PLACE, { recursive: true });
fs.writeFileSync(path.join(TEMPORARY_PLACE, "読み込み元.x"), MESH, "utf8");
fs.writeFileSync(path.join(TEMPORARY_PLACE, "読み込み元.vmd"), motion());
fs.writeFileSync(path.join(TEMPORARY_PLACE, "読み込み元.vpd"), POSE, "utf8");

const prepared = prepare(named["--setup"] ?? beside("e2e-setup-dev.ps1"), setupArgs);
if (prepared.server === null) {
    console.error(prepared.unavailable);
    fs.rmSync(TEMPORARY_PLACE, { recursive: true, force: true });
    process.exit(EXIT_INPUT_UNAVAILABLE);
}

const client = new McpClient(prepared.server);
let finished;
try {
    await client.start();
    const mismatch = await agreed(
        client, named["--schemas"] ?? beside("../catalog/authored/tool-schemas.json"));
    if (mismatch === null) {
        finished = await run(client, cases, processId);
    } else {
        console.error("公開している名前の集合が合っていません:");
        console.error(mismatch);
        finished = EXIT_FAILED;
    }
} catch (error) {
    console.error("ブリッジと話せません: " + error.message);
    finished = EXIT_INPUT_UNAVAILABLE;
} finally {
    await client.stop();
}

fs.rmSync(TEMPORARY_PLACE, { recursive: true, force: true });
process.exit(finished);
