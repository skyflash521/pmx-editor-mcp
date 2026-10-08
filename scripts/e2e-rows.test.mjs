// node:test のテスト。tests/SignatureDump.Tests の E2eScriptTests が node で走らせる。
import assert from "node:assert/strict";
import { test } from "node:test";
import { only } from "./e2e-rows.mjs";

const ROW = "PEPlugin.Pmx.IPXPmx.Clear()";
const OTHER_ROW = "PEPlugin.Pmx.IPXPmx.Clone()";

const WRITING = [
    { rowKey: "", owner: "model_update_iks", tool: "editor_open_window", purpose: "段取り" },
    { rowKey: "", owner: "model_update_iks", tool: "model_update_iks", purpose: "呼ぶ" },
    { rowKey: "", owner: "model_update_iks", tool: "model_list_iks", purpose: "読み返す" },
];

const READING = [
    { rowKey: "", owner: "model_list_iks", tool: "editor_open_window", purpose: "段取り" },
    { rowKey: "", owner: "model_list_iks", tool: "model_list_iks", purpose: "呼ぶ" },
];

const MADE = { rowKey: "", owner: "model_pmx", tool: "model_pmx", purpose: "作る", produces: "made" };
const ROWED = [
    { rowKey: ROW, owner: "model_clear_pmx", tool: "model_clear_pmx", purpose: "呼ぶ", borrowed: { pmx: "made/0" } },
];
const UNRELATED = [
    { rowKey: OTHER_ROW, owner: "model_clone_pmx", tool: "model_clone_pmx", purpose: "呼ぶ" },
];

const CASES = [MADE, ...WRITING, ...READING, ...ROWED, ...UNRELATED];

test("行のキーはその行の検査と、それが借りる値を出す検査を残す", () => {
    const chosen = only(CASES, [ROW]);

    assert.deepEqual(chosen.kept, [MADE, ...ROWED]);
    assert.deepEqual(chosen.missing, []);
});

test("ツールの名前は、そのツールを呼ぶ検査を持つ持ち主の検査列を丸ごと残す", () => {
    const chosen = only(CASES, ["model_list_iks"]);

    assert.deepEqual(chosen.kept, [...WRITING, ...READING]);
    assert.deepEqual(chosen.missing, []);
});

test("ツールの名前に当たった検査列が借りる値を出す検査も残す", () => {
    const chosen = only(CASES, ["model_clear_pmx"]);

    assert.deepEqual(chosen.kept, [MADE, ...ROWED]);
    assert.deepEqual(chosen.missing, []);
});

test("行のキーとツールの名前を混ぜて指せる", () => {
    const chosen = only(CASES, [OTHER_ROW, "model_update_iks"]);

    assert.deepEqual(chosen.kept, [...WRITING, ...UNRELATED]);
    assert.deepEqual(chosen.missing, []);
});

test("どの検査にも当たらないものは名前を挙げて返す", () => {
    const chosen = only(CASES, ["model_absent_tool", ROW]);

    assert.deepEqual(chosen.kept, [MADE, ...ROWED]);
    assert.deepEqual(chosen.missing, ["model_absent_tool"]);
});
