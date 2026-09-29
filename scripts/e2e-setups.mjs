// 自動E2E検査の段取りのうち、重複するものを省く。
//
// プリミティブビルダーの行は、どれも材質とボーンを位置0で指し、材質・ボーンの中身は書かない。
// 省くのは、走らせる並びの中で、先に同じ種類を加えた組が残っているときの、あとの組だけである。

/** 要素を1つ作る段の目的。 */
const MADE = "段取りが要素を1つ作れること";

/** 作った要素を並びへ加える段の目的。 */
const ADDED = "段取りが作った要素を並びへ加えられること";

/** 段取りを省いてよい行。要素の中身を書かない、プリミティブビルダーの呼び出しの行。 */
function isBuilderRow(one) {
    return typeof one.rowKey === "string" && one.rowKey.includes("PrimitiveBuilder.");
}

/** 借りる名前から、末尾の要素の番号を除いた、作った側の名前。 */
function madeName(borrowed) {
    return borrowed.slice(0, borrowed.lastIndexOf("/"));
}

/** 加えても、すでにあるボーン・材質を消しも入れ替えもしない呼び出し。 */
function keepsElements(one) {
    return one.tool.startsWith("model_list_")
        || one.tool.startsWith("model_get_")
        || one.tool.startsWith("model_find_")
        || one.tool.endsWith("_primitive_builder")
        || one.purpose === MADE
        || one.purpose === ADDED;
}

/**
 * 重複する段取りのひと組を省いた並びと、省いた件数を返す。
 * 省く組は、プリミティブビルダーの行で、要素を作る段とそれを加える段が連なり、同じ加える段の組が
 * 先の行に残っているもの。作った要素を借りる段がほかにあるものは省かない。
 */
export function dropRedundantSetups(cases) {
    const borrowed = new Map();
    for (const one of cases) {
        for (const value of Object.values(one.borrowed ?? {})) {
            const made = madeName(value);
            borrowed.set(made, (borrowed.get(made) ?? 0) + 1);
        }
    }

    const kept = [];
    const present = new Set();
    let dropped = 0;
    for (let at = 0; at < cases.length; at++) {
        const one = cases[at];
        if (one.tool === "session_initialize_pmx") {
            present.clear();
            kept.push(one);

            continue;
        }

        const next = cases[at + 1];
        if (one.purpose === MADE && isBuilderRow(one) && typeof one.produces === "string"
            && next !== undefined && next.purpose === ADDED
            && Object.values(next.borrowed ?? {}).some((value) => madeName(value) === one.produces)
            && borrowed.get(one.produces) === 1) {
            if (present.has(next.tool)) {
                dropped += 2;
            } else {
                present.add(next.tool);
                kept.push(one, next);
            }

            at++;

            continue;
        }

        if (!keepsElements(one)) {
            present.clear();
        }

        kept.push(one);
    }

    return { cases: kept, dropped };
}
