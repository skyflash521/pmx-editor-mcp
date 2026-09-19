// MCPサーバーへ stdio で繋ぐクライアント。ブリッジを起こして話す側は、受入の実行器も
// 単独起動の確認もこれを使う——話し方は1つなので、書く場所も1つにする。

import { spawn } from "node:child_process";

/** 要求と応答の jsonrpc に固定で置く値。 */
const JSONRPC_VERSION = "2.0";

/** MCPの版。クライアントが名乗り、サーバーが合わせる。 */
export const MCP_PROTOCOL_VERSION = "2025-06-18";

/** 1件の応答を待つ上限。ホスト側の処理上限へ往復の余裕を足した値。 */
export const RESPONSE_TIMEOUT_MS = 130000;

/** MCPサーバーへ stdio で繋ぐクライアント。要求は1件ずつ投げ、応答を識別子で対応づける。 */
export class McpClient {
    constructor(server) {
        this._server = server;
        this._child = null;
        this._buffer = "";
        this._pending = new Map();
        this._nextId = 1;
        this._ended = null;
        this._instructions = "";
    }

    /** サーバーが初期化のときに名乗った使い方。名乗らなければ空。 */
    get serverInstructions() {
        return this._instructions;
    }

    /** サーバーを起こし、初期化まで済ませる。 */
    async start() {
        // 診断は受け取り手の標準エラー出力へ素通しする。捨てると、落ちた原因を書いていても
        // 読めなくなる。
        const child = spawn(this._server.command, this._server.arguments, {
            stdio: ["pipe", "pipe", "inherit"],
        });
        this._child = child;
        this._buffer = "";
        this._pending = new Map();
        this._nextId = 1;
        this._ended = null;

        // 落とした相手の後始末が、起こし直した相手へ及ばないようにする。落ちるのを待ち切れずに
        // 起こし直すことがあるので、いま繋いでいる相手かどうかを見てから畳む。
        const finish = (reason) => {
            if (this._child === child) {
                this._end(reason);
            }
        };
        child.on("error", (error) => finish("MCPサーバーを起こせません: " + error.message));
        child.on("exit", (code) => finish("MCPサーバーが " + code + " で終わりました。"));
        child.stdout.setEncoding("utf8");
        child.stdout.on("data", (chunk) => {
            if (this._child === child) {
                this._receive(chunk);
            }
        });

        const opened = await this._request("initialize", {
            protocolVersion: MCP_PROTOCOL_VERSION,
            capabilities: {},
            clientInfo: { name: "acceptance", version: "1" },
        });
        const told = opened.result;
        this._instructions =
            told !== null && typeof told === "object" && typeof told.instructions === "string"
                ? told.instructions
                : "";
        this._notify("notifications/initialized", {});
    }

    /** サーバーを落とし、起こし直す。接続を保っていない状態から始めたい段で使う。 */
    async restart() {
        await this.stop();
        await this.start();
    }

    /** サーバーを落とす。 */
    stop() {
        if (this._child === null) {
            return Promise.resolve();
        }

        const child = this._child;
        this._child = null;
        if (child.exitCode !== null || child.signalCode !== null) {
            // もう終わっている相手は、終わるのを待つと知らせが来ないまま止まる。
            return Promise.resolve();
        }

        return new Promise((resolve) => {
            child.once("exit", () => resolve());
            child.kill();
            // 落ちない相手を待ち続けない。次の段はサーバーを起こし直すところから始まる。
            setTimeout(() => resolve(), 5000).unref();
        });
    }

    /** サーバーが公開するツールの名前。 */
    async listTools() {
        const response = await this._request("tools/list", {});
        if (response.error !== undefined) {
            throw new Error(
                "MCPサーバーがツールの一覧を断りました(" + response.error.code + "): "
                    + response.error.message);
        }

        const result = response.result;
        if (result === null || typeof result !== "object" || !Array.isArray(result.tools)) {
            throw new Error("tools/list の結果が tools の並びを持ちません。");
        }

        return result.tools.map((tool) => String(tool.name));
    }

    /**
     * ツールを1件呼び、返った本文と画像と誤りの印を返す。extend を渡すと、待ちの上限に達した
     * ときにそれを呼び、真が返ればもう1回ぶん待ち直す。投げ直しではないので、頼んだ操作が二度
     * 実行されることはない。
     */
    async callTool(name, args, extend = null) {
        const response = await this._request(
            "tools/call", { name, arguments: args }, extend);
        if (response.error !== undefined) {
            throw new Error(
                "MCPサーバーが要求を断りました(" + response.error.code + "): "
                    + response.error.message);
        }

        const result = response.result;
        if (result === null || typeof result !== "object" || !Array.isArray(result.content)) {
            throw new Error("ツールの結果が content の並びを持ちません。");
        }

        const blocks = result.content
            .filter((block) => block !== null && typeof block === "object");
        const texts = blocks
            .filter((block) => block.type === "text")
            .map((block) => String(block.text));
        if (texts.length === 0) {
            throw new Error("ツールの結果に本文がありません。");
        }

        // 画像は本文と別の塊で届く。文字へ均してしまうと、画像として返ったのか文字列で返ったのかを
        // このクライアントを使う検査が見分けられなくなる。
        const images = blocks
            .filter((block) => block.type === "image")
            .map((block) => ({
                data: String(block.data),
                mimeType: block.mimeType === undefined ? null : String(block.mimeType),
            }));

        return { isError: result.isError === true, text: texts.join("\n"), images };
    }

    _request(method, params, extend = null) {
        const id = this._nextId;
        this._nextId += 1;

        return new Promise((resolve, reject) => {
            if (this._ended !== null) {
                reject(new Error(this._ended));
                return;
            }

            const waiting = { resolve, reject, timer: null };
            const fire = () => {
                if (extend !== null && extend()) {
                    waiting.timer = setTimeout(fire, RESPONSE_TIMEOUT_MS);
                    waiting.timer.unref();

                    return;
                }

                this._pending.delete(id);
                const failure = new Error(method + " の応答が時間内に返りませんでした。");
                failure.timedOut = true;
                reject(failure);
            };

            waiting.timer = setTimeout(fire, RESPONSE_TIMEOUT_MS);
            waiting.timer.unref();
            this._pending.set(id, waiting);
            this._child.stdin.write(
                JSON.stringify({ jsonrpc: JSONRPC_VERSION, id, method, params }) + "\n");
        });
    }

    _notify(method, params) {
        this._child.stdin.write(
            JSON.stringify({ jsonrpc: JSONRPC_VERSION, method, params }) + "\n");
    }

    _receive(chunk) {
        this._buffer += chunk;
        for (;;) {
            const at = this._buffer.indexOf("\n");
            if (at < 0) {
                return;
            }

            const line = this._buffer.slice(0, at).trim();
            this._buffer = this._buffer.slice(at + 1);
            if (line === "") {
                continue;
            }

            let message;
            try {
                message = JSON.parse(line);
            } catch {
                // サーバーが診断を標準出力へ混ぜることがある。要求の応答ではないので読み飛ばす。
                continue;
            }

            if (message === null || typeof message !== "object" || message.id === undefined) {
                continue;
            }

            const waiting = this._pending.get(message.id);
            if (waiting === undefined) {
                continue;
            }

            this._pending.delete(message.id);
            clearTimeout(waiting.timer);
            waiting.resolve(message);
        }
    }

    _end(reason) {
        this._ended = reason;
        for (const waiting of this._pending.values()) {
            clearTimeout(waiting.timer);
            waiting.reject(new Error(reason));
        }
        this._pending.clear();
    }
}
