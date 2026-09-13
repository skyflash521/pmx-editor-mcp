// MCPサーバーを起こして疎通を確かめる確認クライアント。
// 渡された実行ファイルをMCPサーバーとして起こし、初期化してから ping を呼び、返った本文を書き出す。
// 見るのはMCPの往復が成り立つところまでで、ホストへ繋がるかどうかは本文が述べる。

import process from "node:process";
import { McpClient } from "./mcp-client.mjs";

/** ホストが応答することを確かめるツールの名前。ブリッジの実装が定める。 */
const PING = "ping";

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
try {
    await client.start();
    said = await client.callTool(PING, {});
} catch (error) {
    console.error("MCPサーバーと話せませんでした: " + error.message);
    await client.stop();
    process.exit(EXIT_FAILED);
}

await client.stop();
console.log(said.text);
process.exit(EXIT_SUCCESS);
