// 自動E2E検査の実行器が、走らせる検査を行のキーとツールの名前で絞る規則。

/**
 * 指した行の検査と、指したツールを呼ぶ検査かそのツールが持ち主の検査を持つ持ち主の検査一式と、
 * それらが借りる値を出す検査を残す。残した検査の並びは元のままなので、出す側は借りる側より先に
 * 来る。どの検査にも当たらないものは、名前を挙げて返す。
 */
export function only(cases, wanted) {
    const keys = new Set(wanted);
    const matched = new Set();
    const owners = new Set();
    for (const one of cases) {
        if (keys.has(one.rowKey)) {
            matched.add(one.rowKey);
        }

        for (const key of [one.tool, one.owner]) {
            if (keys.has(key) && one.owner !== undefined) {
                matched.add(key);
                owners.add(one.owner);
            }
        }
    }

    const source = new Map(
        cases.filter((one) => one.produces !== undefined).map((one) => [one.produces, one]));
    const queue = cases.filter((one) => keys.has(one.rowKey) || owners.has(one.owner));
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

    return {
        kept: cases.filter((one) => taken.has(one)),
        missing: [...keys].filter((key) => !matched.has(key)),
    };
}
