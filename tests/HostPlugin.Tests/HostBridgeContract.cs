using System;
using System.Collections.Generic;

namespace PmxEditorMcp.Contract.Tests
{
    /// <summary>
    /// ホストとブリッジが揃えて持つ取り決め。ホストのテストとブリッジのテストが同じこの1つを読み、
    /// それぞれの実装をこれと突き合わせる。ブリッジのテストはこのファイルをリンクして読む。
    /// </summary>
    internal static class HostBridgeContract
    {
        /// <summary>ホストが要求1件を受け取ってから応答を返すまでに許す時間。</summary>
        public static readonly TimeSpan HostRequestTimeout = TimeSpan.FromSeconds(120);

        /// <summary>1件のメッセージの上限のバイト数。要求と応答の両方に掛かる。</summary>
        public const int MaxMessageBytes = 16 * 1024 * 1024;

        /// <summary>警告1件がツール結果の本文で使う、警告そのもの以外の文字数(行の区切りと接頭辞)。</summary>
        public const int WarningLineOverheadChars = 5;

        /// <summary>ホストが応答に載せるエラーコードの全部と、そのあとホストが切断するかどうか。</summary>
        public static readonly IList<HostErrorCode> HostErrorCodes = new[]
        {
            new HostErrorCode("ParseError", -32700, true),
            new HostErrorCode("InvalidRequest", -32600, false),
            new HostErrorCode("MethodNotFound", -32601, false),
            new HostErrorCode("InvalidParams", -32602, false),
            new HostErrorCode("InternalError", -32603, false),
            new HostErrorCode("ProtocolMismatch", -32001, true),
            new HostErrorCode("RequestTimeout", -32002, false),
            new HostErrorCode("HandshakeRequired", -32003, true),
            new HostErrorCode("RequestTooLarge", -32004, true),
            new HostErrorCode("ResponseTooLarge", -32005, false),
            new HostErrorCode("SessionRefused", -32006, true),
        };
    }

    /// <summary>ホストのエラーコード1つ。</summary>
    internal sealed class HostErrorCode
    {
        public HostErrorCode(string name, int code, bool disconnects)
        {
            Name = name;
            Code = code;
            Disconnects = disconnects;
        }

        /// <summary>ホストの定数の名前。</summary>
        public string Name { get; }

        /// <summary>JSON-RPC のエラーコード。</summary>
        public int Code { get; }

        /// <summary>このコードを返したあと、ホストが接続を切るかどうか。</summary>
        public bool Disconnects { get; }
    }
}
