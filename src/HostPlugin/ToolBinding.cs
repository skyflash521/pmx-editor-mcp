using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PmxEditorMcp
{
    /// <summary>受け手をどう得るか。</summary>
    public enum ToolReceiverKind
    {
        /// <summary>接続の根から辿って得る。道はビルド時に決めてある。</summary>
        Connection,

        /// <summary>どのPMXを見るかの切り替えで選ぶ。</summary>
        Pmx,
    }

    /// <summary>ツールが受け取る引数1件。並びはSDKのシグネチャの引数の並びと同じ。</summary>
    public sealed class ToolArgument
    {
        public ToolArgument(string name, Type type)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            Name = name;
            Type = type;
        }

        /// <summary>要求の引数の名前。</summary>
        public string Name { get; }

        /// <summary>その引数の宣言型。</summary>
        public Type Type { get; }
    }

    /// <summary>項目を集めるツールが持つ項目1件。</summary>
    public sealed class ToolField
    {
        public ToolField(string name, string rowKey, Type type)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (rowKey == null)
            {
                throw new ArgumentNullException(nameof(rowKey));
            }

            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            Name = name;
            RowKey = rowKey;
            Type = type;
        }

        /// <summary>応答と要求に現れる項目の名前。</summary>
        public string Name { get; }

        /// <summary>その項目を読み書きする行キー。</summary>
        public string RowKey { get; }

        /// <summary>その項目の宣言型。</summary>
        public Type Type { get; }
    }

    /// <summary>受け手の得方と、呼び出しがエディタの状態へどう作用するか。</summary>
    public sealed class ToolReceiver
    {
        public ToolReceiver(ToolReceiverKind kind, string typeName, EditKind edit)
        {
            Kind = kind;
            TypeName = typeName;
            Edit = edit;
        }

        public ToolReceiverKind Kind { get; }

        /// <summary>接続の道を引く鍵。静的なメンバーと、PMXから得る受け手では null。</summary>
        public string TypeName { get; }

        /// <summary>呼び出しの分類。複製編集型はまとめて反映するところまでを1回で行う。</summary>
        public EditKind Edit { get; }
    }

    /// <summary>SDKのメンバーへ中継するツール1件。</summary>
    public sealed class ToolCall
    {
        public ToolCall(
            string rowKey,
            ToolReceiver receiver,
            DangerKind danger,
            IList<ToolArgument> arguments,
            Type result)
        {
            if (rowKey == null)
            {
                throw new ArgumentNullException(nameof(rowKey));
            }

            if (receiver == null)
            {
                throw new ArgumentNullException(nameof(receiver));
            }

            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            RowKey = rowKey;
            Receiver = receiver;
            Danger = danger;
            Arguments = new ReadOnlyCollection<ToolArgument>(arguments);
            Result = result;
        }

        /// <summary>呼ぶ行のキー。</summary>
        public string RowKey { get; }

        /// <summary>受け手の得方。</summary>
        public ToolReceiver Receiver { get; }

        /// <summary>取り返しの付かなさの種別。</summary>
        public DangerKind Danger { get; }

        /// <summary>受け取る引数。</summary>
        public IList<ToolArgument> Arguments { get; }

        /// <summary>返す値の宣言型。値を返さないメンバーでは null。</summary>
        public Type Result { get; }
    }

    /// <summary>項目を集めるツール1件。取得か更新のどちらかを受け持つ。</summary>
    public sealed class ToolFields
    {
        public ToolFields(bool writes, bool listing, ToolReceiver receiver, IList<ToolField> fields)
        {
            if (receiver == null)
            {
                throw new ArgumentNullException(nameof(receiver));
            }

            if (fields == null)
            {
                throw new ArgumentNullException(nameof(fields));
            }

            Writes = writes;
            Listing = listing;
            Receiver = receiver;
            Fields = new ReadOnlyCollection<ToolField>(fields);
        }

        /// <summary>項目を書き込む側か。偽なら読み取る側。</summary>
        public bool Writes { get; }

        /// <summary>総数と切り出した並びを返す形か。偽なら項目の組をそのまま返す。</summary>
        public bool Listing { get; }

        /// <summary>受け手の得方。</summary>
        public ToolReceiver Receiver { get; }

        /// <summary>集める項目。</summary>
        public IList<ToolField> Fields { get; }
    }

    /// <summary>所有するリストへ加える・から取り除くツール1件。</summary>
    public sealed class ToolElements
    {
        public ToolElements(bool removes, string listRowKey, ToolReceiver receiver, Type element)
        {
            if (listRowKey == null)
            {
                throw new ArgumentNullException(nameof(listRowKey));
            }

            if (receiver == null)
            {
                throw new ArgumentNullException(nameof(receiver));
            }

            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            Removes = removes;
            ListRowKey = listRowKey;
            Receiver = receiver;
            Element = element;
        }

        /// <summary>取り除く側か。偽なら加える側。</summary>
        public bool Removes { get; }

        /// <summary>そのリストへ至る行のキー。</summary>
        public string ListRowKey { get; }

        /// <summary>そのリストを持つ受け手の得方。</summary>
        public ToolReceiver Receiver { get; }

        /// <summary>リストが並べる要素の宣言型。</summary>
        public Type Element { get; }
    }
}
