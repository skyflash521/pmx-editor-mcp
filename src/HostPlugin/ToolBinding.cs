using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

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

    /// <summary>受け手がPMXのどこに居るか。</summary>
    public enum ToolAccessKind
    {
        /// <summary>受け手そのもの。PMX全体か、接続の道から得たもの。</summary>
        Whole,

        /// <summary>PMXが1つだけ持つ子。</summary>
        Child,

        /// <summary>リストが並べる要素。</summary>
        Element,
    }

    /// <summary>リストが並べうる具象の型1つ。</summary>
    public sealed class ToolItem
    {
        public ToolItem(string itemType, Type element, Func<object, bool> isItem)
        {
            if (itemType == null)
            {
                throw new ArgumentNullException(nameof(itemType));
            }

            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            if (isItem == null)
            {
                throw new ArgumentNullException(nameof(isItem));
            }

            ItemType = itemType;
            Element = element;
            IsItem = isItem;
        }

        /// <summary>その型の要素名詞。応答の実行時の型として載る。</summary>
        public string ItemType { get; }

        /// <summary>その型そのもの。台帳はこの名前でハンドルを覚えている。</summary>
        public Type Element { get; }

        /// <summary>その実体がこの型か。</summary>
        public Func<object, bool> IsItem { get; }
    }

    /// <summary>要素へ至る途中の一歩。</summary>
    public sealed class ToolHop
    {
        public ToolHop(string rowKey, bool listed)
        {
            if (rowKey == null)
            {
                throw new ArgumentNullException(nameof(rowKey));
            }

            RowKey = rowKey;
            Listed = listed;
        }

        /// <summary>その一歩を進む行のキー。</summary>
        public string RowKey { get; }

        /// <summary>リストの段か。偽なら、そのプロパティを1つ辿る段。</summary>
        public bool Listed { get; }
    }

    /// <summary>
    /// 相手にするPMXから受け手へ至る道。要素を相手にする道は、親の列を作る一歩の並びと、
    /// 要素を並べるリストの行からなる。
    /// </summary>
    public sealed class ToolAccess
    {
        private static readonly ToolHop[] NoHops = new ToolHop[0];

        private static readonly ToolItem[] NoItems = new ToolItem[0];

        public ToolAccess(
            ToolAccessKind kind,
            string rowKey,
            IList<ToolHop> parents,
            bool listed,
            Type element,
            Func<object, bool> isElement,
            string itemType = null,
            IList<ToolItem> items = null)
        {
            if (kind != ToolAccessKind.Whole && rowKey == null)
            {
                throw new ArgumentNullException(nameof(rowKey));
            }

            if (kind == ToolAccessKind.Element && (element == null || isElement == null))
            {
                throw new ArgumentNullException(nameof(element));
            }

            Kind = kind;
            RowKey = rowKey;
            Parents = new ReadOnlyCollection<ToolHop>(parents ?? NoHops);
            Listed = listed;
            Element = element;
            IsElement = isElement;
            ItemType = itemType;
            Items = new ReadOnlyCollection<ToolItem>(items ?? NoItems);
        }

        public ToolAccessKind Kind { get; }

        /// <summary>子を得る行、または要素を並べるリストの行。受け手そのものでは null。</summary>
        public string RowKey { get; }

        /// <summary>要素までに辿る親の一歩。PMXが直に持つリストでは空。</summary>
        public IList<ToolHop> Parents { get; }

        /// <summary>最後の一歩がリストの段か。偽なら、親ごとに1つだけ辿る段である。</summary>
        public bool Listed { get; }

        /// <summary>要素として扱う宣言型。要素を相手にしない道では null。</summary>
        public Type Element { get; }

        /// <summary>その実体を要素として扱えるか。要素を相手にしない道では null。</summary>
        public Func<object, bool> IsElement { get; }

        /// <summary>そのツールが相手にする型の要素名詞。要素を相手にしない道では null。</summary>
        public string ItemType { get; }

        /// <summary>
        /// そのリストが並べうる具象の型。要素の型が抽象で実体が複数の型に分かれるリストだけが持ち、
        /// ほかは空——分かれないリストでは、実行時の型を載せても要素の型の言い直しにしかならない。
        /// </summary>
        public IList<ToolItem> Items { get; }

        /// <summary>受け手そのものを相手にする道。</summary>
        public static ToolAccess Whole()
        {
            return new ToolAccess(ToolAccessKind.Whole, null, null, false, null, null);
        }
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
            ToolAccess access,
            DangerKind danger,
            IList<ToolArgument> arguments,
            IList<ToolArgument> outputs,
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

            if (access == null)
            {
                throw new ArgumentNullException(nameof(access));
            }

            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            if (outputs == null)
            {
                throw new ArgumentNullException(nameof(outputs));
            }

            RowKey = rowKey;
            Receiver = receiver;
            Access = access;
            Danger = danger;
            Arguments = new ReadOnlyCollection<ToolArgument>(arguments);
            Outputs = new ReadOnlyCollection<ToolArgument>(outputs);
            Result = result;
        }

        /// <summary>呼ぶ行のキー。</summary>
        public string RowKey { get; }

        /// <summary>受け手の得方。</summary>
        public ToolReceiver Receiver { get; }

        /// <summary>相手にするPMXから受け手へ至る道。</summary>
        public ToolAccess Access { get; }

        /// <summary>取り返しの付かなさの種別。</summary>
        public DangerKind Danger { get; }

        /// <summary>受け取る引数。</summary>
        public IList<ToolArgument> Arguments { get; }

        /// <summary>出力に現れる引数。呼び出した後の値を応答へ載せる。</summary>
        public IList<ToolArgument> Outputs { get; }

        /// <summary>返す値の宣言型。値を返さないメンバーでは null。</summary>
        public Type Result { get; }
    }

    /// <summary>実行時の型ごとに集める項目。型で分かれないツールは1件だけを持つ。</summary>
    public sealed class ToolFieldSet
    {
        public ToolFieldSet(string itemType, IList<ToolField> fields)
        {
            if (fields == null)
            {
                throw new ArgumentNullException(nameof(fields));
            }

            ItemType = itemType;
            Fields = new ReadOnlyCollection<ToolField>(fields);
        }

        /// <summary>その項目を持つ要素の実行時の型。型で分かれないツールでは null。</summary>
        public string ItemType { get; }

        /// <summary>集める項目。</summary>
        public IList<ToolField> Fields { get; }
    }

    /// <summary>項目を集めるツール1件。取得か更新のどちらかを受け持つ。</summary>
    public sealed class ToolFields
    {
        public ToolFields(
            bool writes,
            bool listing,
            ToolReceiver receiver,
            ToolAccess access,
            IList<ToolFieldSet> sets)
        {
            if (receiver == null)
            {
                throw new ArgumentNullException(nameof(receiver));
            }

            if (access == null)
            {
                throw new ArgumentNullException(nameof(access));
            }

            if (sets == null)
            {
                throw new ArgumentNullException(nameof(sets));
            }

            Writes = writes;
            Listing = listing;
            Receiver = receiver;
            Access = access;
            Sets = new ReadOnlyCollection<ToolFieldSet>(sets);
            List<ToolField> all = new List<ToolField>();
            foreach (ToolFieldSet set in sets)
            {
                foreach (ToolField field in set.Fields)
                {
                    if (!all.Any(f => string.Equals(f.Name, field.Name, StringComparison.Ordinal)))
                    {
                        all.Add(field);
                    }
                }
            }

            Fields = new ReadOnlyCollection<ToolField>(all);
        }

        /// <summary>項目を書き込む側か。偽なら読み取る側。</summary>
        public bool Writes { get; }

        /// <summary>総数と切り出した並びを返す形か。偽なら項目の組をそのまま返す。</summary>
        public bool Listing { get; }

        /// <summary>受け手の得方。</summary>
        public ToolReceiver Receiver { get; }

        /// <summary>相手にするPMXから受け手へ至る道。</summary>
        public ToolAccess Access { get; }

        /// <summary>実行時の型ごとに集める項目。</summary>
        public IList<ToolFieldSet> Sets { get; }

        /// <summary>どの実行時の型かに依らず、このツールが扱う項目の全体。名前は重ならない。</summary>
        public IList<ToolField> Fields { get; }
    }

    /// <summary>所有するリストへ加える・から取り除くツール1件。</summary>
    public sealed class ToolElements
    {
        public ToolElements(bool removes, ToolReceiver receiver, ToolAccess access)
        {
            if (receiver == null)
            {
                throw new ArgumentNullException(nameof(receiver));
            }

            if (access == null)
            {
                throw new ArgumentNullException(nameof(access));
            }

            if (access.Kind != ToolAccessKind.Element)
            {
                throw new ArgumentException("要素を相手にする道でなければならない。", nameof(access));
            }

            Removes = removes;
            Receiver = receiver;
            Access = access;
        }

        /// <summary>取り除く側か。偽なら加える側。</summary>
        public bool Removes { get; }

        /// <summary>そのリストを持つ受け手の得方。</summary>
        public ToolReceiver Receiver { get; }

        /// <summary>相手にするPMXから要素へ至る道。</summary>
        public ToolAccess Access { get; }
    }
}
