using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace PmxEditorMcp
{
    /// <summary>メソッドへ渡された引数が契約に合わないことを表す。応答では不正な引数として扱う。</summary>
    public sealed class InvalidParamsException : Exception
    {
        /// <summary>要求元へ返す説明を添えて生成する。</summary>
        public InvalidParamsException(string message)
            : base(message)
        {
        }
    }

    /// <summary>メソッドを呼ぶときに渡す一式。</summary>
    public sealed class McpMethodContext
    {
        /// <summary>引数・UIスレッドへの委譲・応答サイズ予算を与えて生成する。</summary>
        public McpMethodContext(IDictionary<string, object> parameters, IUiInvoker ui, int budgetChars)
        {
            throw new NotImplementedException();
        }

        /// <summary>要求の引数。省略されていたときは空。</summary>
        public IDictionary<string, object> Params => throw new NotImplementedException();

        /// <summary>UIスレッドへの委譲。PEPlugin API の呼び出しはすべてこれを通す。</summary>
        public IUiInvoker Ui => throw new NotImplementedException();

        /// <summary>ホストが読んだ応答サイズ予算の文字数。結果の量を抑える判定に用いる。</summary>
        public int BudgetChars => throw new NotImplementedException();
    }

    /// <summary>ホストが公開する処理。戻り値がそのまま応答の result になる。</summary>
    public delegate object McpMethod(McpMethodContext context);

    /// <summary>メソッド名から処理を引く表。</summary>
    public sealed class McpMethodTable
    {
        /// <summary>
        /// 処理を登録する。同じ名前を二度登録することと、接続自身が受け持つ基盤メソッドの名前
        /// (handshake・ping)を登録することは、いずれも黙って無視されるのを避けるため拒む。
        /// </summary>
        public void Add(string name, McpMethod method)
        {
            throw new NotImplementedException();
        }

        /// <summary>名前に対応する処理を引く。</summary>
        public bool TryGet(string name, out McpMethod method)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 接続1本ぶんの処理。要求を1件ずつ読み、契約の順に判定して応答を1件返す。切断が要る
    /// エラーでは応答を書いてから戻り、待受ループが切断として扱う。ハンドシェイクの成否は
    /// <see cref="Handle"/> の呼び出しごとに独立していて、同じインスタンスで次の接続を
    /// 処理するときは持ち越さない。
    /// </summary>
    public sealed class JsonRpcConnection
    {
        /// <summary>ハンドシェイクで一致していなければならないプロトコル番号。</summary>
        public const int Protocol = 1;

        /// <summary>接続自身が受け持つ基盤メソッドの名前。</summary>
        public static readonly ReadOnlyCollection<string> BaseMethodNames =
            Array.AsReadOnly(new[] { "handshake", "ping" });

        /// <summary>要求1件の処理に許す時間の既定。</summary>
        public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(120);

        /// <summary>ログ・メソッド表・ハンドシェイク応答に載せる値を与えて生成する。</summary>
        public JsonRpcConnection(HostLog log, McpMethodTable methods, string hostVersion, int budgetChars)
            : this(log, methods, hostVersion, budgetChars, DefaultRequestTimeout, MessageChannel.DefaultMaxMessageBytes)
        {
        }

        /// <summary>
        /// 要求処理の時間の上限とメッセージの上限も指定して生成する。どちらもテストから
        /// 差し替えるための引数で、通常は既定を用いる。
        /// </summary>
        public JsonRpcConnection(
            HostLog log,
            McpMethodTable methods,
            string hostVersion,
            int budgetChars,
            TimeSpan requestTimeout,
            int maxMessageBytes)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 接続を処理する。相手が切断するか、切断が要るエラーを返すまで戻らない。
        /// </summary>
        public void Handle(Stream stream, IUiInvoker ui)
        {
            throw new NotImplementedException();
        }
    }
}
