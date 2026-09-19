// MCPサーバーを起こして疎通を確かめる確認クライアント。
// 渡された実行ファイルをMCPサーバーとして起こし、初期化してから ping を呼び、返った本文を書き出す。
// 見るのはMCPの往復が成り立つところまでで、ホストへ繋がるかどうかは本文が述べる。
// 初期化でサーバーが名乗る使い方も見る。名乗らないものと、公開しているツールの系統のうち
// 名乗りが触れていないものがあるものは不合格とする。

import process from "node:process";
import { McpClient } from "./mcp-client.mjs";

/** ホストが応答することを確かめるツールの名前。ブリッジの実装が定める。 */
const PING = "ping";

/** ホストの接続自身が受け持つツールの名前。どの系統にも属さない。ブリッジの実装が定める。 */
const CONNECTION_TOOLS = [PING, "sdk_status"];

const EXIT_SUCCESS = 0;
const EXIT_FAILED = 1;
const EXIT_INVALID_ARGUMENTS = 2;

const [command, ...args] = process.argv.slice(2);
if (command === undefined) {
    console.error("使い方: node mcp-check.mjs <MCPサーバーの実行ファイル> [引数]...");
    process.exit(EXIT_INVALID_ARGUMENTS);
}

const client = new McpClient({ command, arguments: args });
let said;
let named;
try {
    await client.start();
    said = await client.callTool(PING, {});
    named = await client.listTools();
} catch (error) {
    console.error("MCPサーバーと話せませんでした: " + error.message);
    await client.stop();
    process.exit(EXIT_FAILED);
}

const guidance = client.serverInstructions;

await client.stop();

if (guidance === "") {
    console.error("MCPサーバーが初期化で使い方を名乗りませんでした。");
    process.exit(EXIT_FAILED);
}

const families = [...new Set(
    named
        .filter((name) => !CONNECTION_TOOLS.includes(name))
        .map((name) => name.slice(0, name.indexOf("_") + 1)),
)].sort();
const absent = families.filter((family) => !guidance.includes(family));
if (absent.length > 0) {
    console.error("使い方が触れていない系統があります: " + absent.join("・"));
    process.exit(EXIT_FAILED);
}

console.log(said.text);
console.log("使い方が触れた系統: " + families.join("・"));
process.exit(EXIT_SUCCESS);
