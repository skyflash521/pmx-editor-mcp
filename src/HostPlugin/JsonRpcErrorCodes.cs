namespace PmxEditorMcp
{
    public static class JsonRpcErrorCodes
    {
        /// <summary>本文がJSONとして解釈できない。</summary>
        public const int ParseError = -32700;

        /// <summary>要求の構造が契約に合わない。</summary>
        public const int InvalidRequest = -32600;

        /// <summary>method に対応する処理が無い。</summary>
        public const int MethodNotFound = -32601;

        /// <summary>params が契約に合わない。</summary>
        public const int InvalidParams = -32602;

        /// <summary>要求処理で予期しない例外が起きた。</summary>
        public const int InternalError = -32603;

        /// <summary>プロトコル番号が合わない。</summary>
        public const int ProtocolMismatch = -32001;

        /// <summary>要求処理が上限の時間を超えた。</summary>
        public const int RequestTimeout = -32002;

        /// <summary>ハンドシェイクの前に他の要求が来た。</summary>
        public const int HandshakeRequired = -32003;

        /// <summary>入力が上限を超えた(本文のバイト数、または解析前に数える構造トークン)。</summary>
        public const int RequestTooLarge = -32004;

        /// <summary>応答のメッセージが上限のバイト数を超えた。</summary>
        public const int ResponseTooLarge = -32005;

        /// <summary>接続元のプロセスを所有者にできないか、提示された session が別のプロセスのもの。</summary>
        public const int SessionRefused = -32006;
    }
}
