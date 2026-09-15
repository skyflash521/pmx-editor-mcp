// 参照クライアントに呼ばせるツールと、乗っていなければならない引数。
// 実機で呼ばせる側と、実行器を確かめる題材の側が同じ並びを読む——片方だけが知っていると、
// 題材が返す呼び出しと実行器が待つ呼び出しが食い違う。

/** 登録するMCPサーバーの名前。ツールの綴りはこの名前から組み立てられる。 */
export const SERVER_NAME = "pmx-editor-mcp";

/**
 * 呼ばせるツールと、乗っていなければならない引数。画像を返すツールは、返りが画像として届くことも
 * 見る。
 * 組の直下を分岐で綴っていたときに引数が落ちたものから採る——落ちない形だけを並べると、
 * 落ちたことをこの検査が見逃す。
 */
export const CASES = [
    {
        tool: "model_list_materials",
        // 真偽と数を1つずつ含む。綴りが読まれないと、真偽が文字列になってホストが弾く。
        arguments: { all: true, limit: 3 },
        image: false,
    },
    {
        tool: "model_list_morph_offsets",
        // 親を選ぶ綴りと自分を選ぶ綴りが重なる一覧。分岐がもっとも深くなる形である。
        arguments: { parentAll: true, all: true, limit: 3 },
        image: false,
    },
    {
        // ビューの画像。既定の窓の大きさのまま1回で得られること、そして文字列でなく画像として
        // 届くことを見る——文字列で届くと、参照クライアントは中身を見られない。仕様に合っているかでは
        // なく、クライアントが画像として受け取るかを見る。
        tool: "view_get_client_image_pmd_view_connector",
        arguments: {},
        image: true,
    },
];

/** ツールの綴り。参照クライアントが呼ぶ名前は、サーバーの名前とツール名から決まる。 */
export function named(tool) {
    return "mcp__" + SERVER_NAME + "__" + tool;
}
