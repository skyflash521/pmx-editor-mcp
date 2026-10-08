// node:test のテスト。tests/SignatureDump.Tests の E2eSetupsScriptTests が node で走らせる。
import assert from "node:assert/strict";
import { test } from "node:test";
import { dropRedundantSetups } from "./e2e-setups.mjs";

const MADE = "段取りが要素を1つ作れること";
const ADDED = "段取りが作った要素を並びへ加えられること";
const BUILDER_ROW = (n) => "PEPlugin.Pmx.IPXPrimitiveBuilder.Member" + n + "(System.Int32,PEPlugin.Pmx.IPXBone)";

/** ある行の、要素を1つ作って並びへ加える段のひと組。 */
function pair(row, tool, made) {
    const key = row + "#" + tool;

    return [
        { rowKey: row, tool: made, purpose: MADE, arguments: {}, produces: key },
        { rowKey: row, tool, purpose: ADDED, arguments: { handles: [null] }, borrowed: { "handles/0": key + "/0" } },
    ];
}

/** プリミティブビルダーの行。ボーンと材質を加えて、頂点を読み比べてビルダーを呼ぶ。 */
function builderRow(n) {
    const row = BUILDER_ROW(n);

    return [
        ...pair(row, "model_add_materials", "model_material"),
        ...pair(row, "model_add_bones", "model_bone"),
        { rowKey: row, tool: "model_list_vertices", purpose: "呼ぶ前の姿を読めること", arguments: {} },
        { rowKey: row, tool: "model_add_box_primitive_builder", purpose: "呼び出して成功すること", arguments: { bone: 0, material: 0 } },
        { rowKey: row, tool: "model_list_vertices", purpose: "呼び出しの後に読めるものが、呼ぶ前と違うこと", arguments: {} },
    ];
}

const tools = (cases) => cases.map((c) => c.tool);

test("2つ目以降の行の、ボーンと材質を加えるひと組を省き、1つ目の行は残す", () => {
    const cases = [...builderRow(1), ...builderRow(2), ...builderRow(3)];
    const done = dropRedundantSetups(cases);

    assert.equal(done.dropped, 8);
    assert.equal(done.cases.length, cases.length - 8);
    assert.deepEqual(tools(done.cases.slice(0, 7)), [
        "model_material", "model_add_materials", "model_bone", "model_add_bones",
        "model_list_vertices", "model_add_box_primitive_builder", "model_list_vertices",
    ]);
    assert.equal(done.cases.filter((c) => c.tool === "model_add_bones").length, 1);
    assert.equal(done.cases.filter((c) => c.tool === "model_add_box_primitive_builder").length, 3);
});

test("モデルを空へ戻したあとの行は、また加える", () => {
    const cases = [
        ...builderRow(1),
        { rowKey: "", tool: "session_initialize_pmx", purpose: "段取りがモデルを空へ揃えられること", arguments: { confirm: true } },
        ...builderRow(2),
    ];
    const done = dropRedundantSetups(cases);

    assert.equal(done.dropped, 0);
    assert.equal(done.cases.length, cases.length);
});

test("構造を変えうる呼び出しが間に入ったら、その先の行は、また加える", () => {
    const cases = [
        ...builderRow(1),
        { rowKey: "Other.Remove()", tool: "model_remove_bones", purpose: "呼び出して成功すること", arguments: {} },
        ...builderRow(2),
    ];
    const done = dropRedundantSetups(cases);

    assert.equal(done.dropped, 0);
});

test("作った要素を、加える段のほかの段も借りるなら、その組は省かない", () => {
    const first = builderRow(1);
    const second = builderRow(2);
    const key = BUILDER_ROW(2) + "#model_add_bones";
    second.push({ rowKey: BUILDER_ROW(2), tool: "model_update_bones", purpose: "呼び出して成功すること", arguments: {}, borrowed: { "handles/0": key + "/0" } });
    const done = dropRedundantSetups([...first, ...second]);

    assert.equal(done.dropped, 2);
    assert.equal(done.cases.filter((c) => c.tool === "model_add_bones").length, 2);
    assert.equal(done.cases.filter((c) => c.tool === "model_add_materials").length, 1);
});

test("プリミティブビルダーの行でないものは、省かない", () => {
    const other = (n) => {
        const row = "Other.Member" + n + "(PEPlugin.Pmx.IPXBone)";

        return [...pair(row, "model_add_bones", "model_bone"), { rowKey: row, tool: "model_update_bones", purpose: "呼び出して成功すること", arguments: {} }];
    };
    const cases = [...other(1), ...other(2)];
    const done = dropRedundantSetups(cases);

    assert.equal(done.dropped, 0);
    assert.equal(done.cases.length, cases.length);
});

test("1つの行だけを走らせるときは、その行のひと組を残す", () => {
    const done = dropRedundantSetups(builderRow(5));

    assert.equal(done.dropped, 0);
});
