// 参照クライアントに呼ばせるツールと、乗っていなければならない引数。
// 実機で呼ばせる側と、実行器を確かめる題材の側が同じ並びを読む。

/** 登録するMCPサーバーの名前。ツールの綴りはこの名前から組み立てられる。 */
export const SERVER_NAME = "pmx-editor-mcp";

/** 呼ばせるツールと、乗っていなければならない引数。画像を返すツールは、返りが画像として届くことも見る。 */
export const CASES = [
    {
        tool: "model_list_materials",
        arguments: { all: true, limit: 3 },
        image: false,
    },
    {
        tool: "model_list_morph_offsets",
        arguments: { parentAll: true, all: true, limit: 3 },
        image: false,
    },
    {
        tool: "view_get_client_image_pmd_view_connector",
        arguments: {},
        image: true,
    },
];

/** ツールの綴り。参照クライアントが呼ぶ名前は、サーバーの名前とツール名から決まる。 */
export function named(tool) {
    return "mcp__" + SERVER_NAME + "__" + tool;
}
