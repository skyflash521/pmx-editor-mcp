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

        /// <summary>
        /// 呼び出しが渡すハンドルから得る。どのリストにも並ばず、台帳が持つ間だけ生きる相手が
        /// これに当たる。
        /// </summary>
        Handle,
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
            IList<ToolItem> items = null,
            Type owner = null)
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
            Owner = owner;
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

        /// <summary>
        /// そのリストを直に持つ型。親をハンドルで指せる道だけが持ち、ほかは null——親が所有する
        /// リストの要素でなければ、その親を指すハンドルは発行されない。
        /// </summary>
        public Type Owner { get; }

        /// <summary>受け手そのものを相手にする道。</summary>
        public static ToolAccess Whole()
        {
            return new ToolAccess(ToolAccessKind.Whole, null, null, false, null, null);
        }
    }

    /// <summary>ツールが受け取る引数1件。並びはSDKのシグネチャの引数の並びと同じ。</summary>
    public sealed class ToolArgument
    {
        public ToolArgument(
            string name,
            Type type,
            bool injected = false,
            ToolAccess referenced = null,
            bool connector = false,
            Type held = null,
            string resident = null,
            ToolValueShape built = null,
            Func<object, bool> holds = null)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (held != null && holds == null)
            {
                throw new ArgumentNullException(nameof(holds));
            }

            Name = name;
            Type = type;
            Injected = injected;
            Referenced = referenced;
            Connector = connector;
            Held = held;
            Resident = resident;
            Built = built;
            Holds = holds;
        }

        /// <summary>要求の引数の名前。</summary>
        public string Name { get; }

        /// <summary>その引数の宣言型。</summary>
        public Type Type { get; }

        /// <summary>ホストが自分で入れる引数か。呼び出す側はこの引数を渡さない。</summary>
        public bool Injected { get; }

        /// <summary>
        /// ホストが入れるのが、Cプラグイン連携のコネクタかどうか。偽なら相手にするPMXを入れる。
        /// </summary>
        public bool Connector { get; }

        /// <summary>
        /// その引数が指す実体を並べるリストへの道。位置で受け取る引数だけが持ち、ほかは null。
        /// </summary>
        public ToolAccess Referenced { get; }

        /// <summary>
        /// その引数が指す実体の型。ハンドルで受け取る引数だけが持ち、ほかは null。並びを取る引数
        /// では、並びの要素1つが指す実体の型になる。
        /// </summary>
        public Type Held { get; }

        /// <summary>
        /// 預かっている実体を、その引数へ渡せるか。ハンドルで受け取る引数だけが持ち、ほかは null。
        /// 生成時に作る判定で、台帳が覚えている型の名前は見ない——名前で照らすと、その型を実装する
        /// 実体を基底の型の引数へ渡せない。
        /// </summary>
        public Func<object, bool> Holds { get; }

        /// <summary>
        /// その引数へ入れる常駐の受け手を引く鍵。接続の道から得る受け手を取る引数だけが持ち、
        /// ほかは null。
        /// </summary>
        public string Resident { get; }

        /// <summary>
        /// その引数の組み立て方。組で受け取ってSDKへ渡す実体を作る引数だけが持ち、ほかは null。
        /// </summary>
        public ToolValueShape Built { get; }
    }

    /// <summary>項目を集めるツールが持つ項目1件。</summary>
    public sealed class ToolField
    {
        public ToolField(
            string name,
            string rowKey,
            Type type,
            IList<ToolField> members = null,
            ToolAccess referenced = null,
            bool listed = false)
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
            Members = members == null ? null : new ReadOnlyCollection<ToolField>(members);
            Referenced = referenced;
            Listed = listed;
        }

        /// <summary>応答と要求に現れる項目の名前。</summary>
        public string Name { get; }

        /// <summary>その項目を読み書きする行キー。</summary>
        public string RowKey { get; }

        /// <summary>その項目の宣言型。</summary>
        public Type Type { get; }

        /// <summary>
        /// その項目が値として写せない型のとき、その中の項目。写せる型では null で、値をそのまま
        /// 写す。
        /// </summary>
        public IList<ToolField> Members { get; }

        /// <summary>
        /// その項目が操作対象の実体を指すとき、位置を数えるリストへの道。値はこの列の中の位置で
        /// 写り、列に居ない実体は null で写る。指さない項目では null。
        /// </summary>
        public ToolAccess Referenced { get; }

        /// <summary>その項目が実体を並びで指すか。1つだけ指す項目では偽。</summary>
        public bool Listed { get; }
    }

    /// <summary>受け手の得方と、呼び出しがエディタの状態へどう作用するか。</summary>
    public sealed class ToolReceiver
    {
        public ToolReceiver(
            ToolReceiverKind kind,
            string typeName,
            EditKind edit,
            bool bridged = false,
            Func<object, bool> accepts = null)
        {
            if (kind == ToolReceiverKind.Handle && accepts == null)
            {
                throw new ArgumentNullException(nameof(accepts));
            }

            Kind = kind;
            TypeName = typeName;
            Edit = edit;
            Bridged = bridged;
            Accepts = accepts;
        }

        public ToolReceiverKind Kind { get; }

        /// <summary>接続の道を引く鍵。静的なメンバーと、PMXから得る受け手では null。</summary>
        public string TypeName { get; }

        /// <summary>呼び出しの分類。複製編集型はまとめて反映するところまでを1回で行う。</summary>
        public EditKind Edit { get; }

        /// <summary>
        /// Cプラグイン連携の橋渡しから受け手を得るか。複製と反映も、そちらの流れで行う——片方の
        /// 流れで作った中身は、もう片方の流れでは反映できない。
        /// </summary>
        public bool Bridged { get; }

        /// <summary>
        /// その実体を受け手にできるか。ハンドルから得る受け手だけが持ち、派生した型も通す。
        /// </summary>
        public Func<object, bool> Accepts { get; }
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
            Type result,
            Type issues = null,
            IList<ToolField> projected = null,
            string releases = null,
            bool releasesIssued = false,
            bool returnsMany = false,
            bool respondsMany = false,
            string selectorName = null,
            string selectorValue = null,
            bool paged = false)
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
            Issues = issues;
            Projected = projected == null ? null : new ReadOnlyCollection<ToolField>(projected);
            Releases = releases;
            ReleasesIssued = releasesIssued;
            ReturnsMany = returnsMany;
            RespondsMany = respondsMany;
            SelectorName = selectorName;
            SelectorValue = selectorValue;
            Paged = paged;
        }

        /// <summary>
        /// 返す並びを位置と件数で切り出すか。真なら offset と limit を受け取り、総数と切り出した並びと
        /// 続きの位置を返す。
        /// </summary>
        public bool Paged { get; }

        /// <summary>呼び分けを選ぶ項目の名前。名前だけでは見分けられないツールが持つ。</summary>
        public string SelectorName { get; }

        /// <summary>この呼び分けを選ぶ値。</summary>
        public string SelectorValue { get; }

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

        /// <summary>
        /// 返す値を台帳へ預ける型。生成物を返す行だけが持ち、応答はその型のハンドルになる。
        /// ほかは null で、返す値をそのまま写す。
        /// </summary>
        public Type Issues { get; }

        /// <summary>
        /// 返す値が並びか。真なら1件ずつを預ける・写して、その並びを返す。値としてそのまま写せる
        /// 並びでは偽——写し方の側が並びを扱う。
        /// </summary>
        public bool ReturnsMany { get; }

        /// <summary>
        /// 応答がハンドルの並びか。同じ名前の呼び分けのどれかが並びを預けるなら、どの呼び分けでも
        /// 並びで返す——選んだ呼び分けで応答の形が変わらないようにする。
        /// </summary>
        public bool RespondsMany { get; }

        /// <summary>
        /// 預けた生成物を手放すときに呼ぶ行のキー。手放す手順を持つ型を預ける行だけが持ち、ほかは
        /// null で、台帳の失効だけで足りる。
        /// </summary>
        public string Releases { get; }

        /// <summary>
        /// 手放す呼び出しが、預けた生成物を引数に取るか。真なら受け手はこの呼び出しの受け手で、
        /// 偽なら生成物そのものが受け手になる。
        /// </summary>
        public bool ReleasesIssued { get; }

        /// <summary>
        /// 返す値が値として写せない型のとき、その中の項目を読む行。ほかは null で、値をそのまま
        /// 写す。
        /// </summary>
        public IList<ToolField> Projected { get; }
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

    /// <summary>所有するリストの要素を相手にするツールが行うこと。</summary>
    public enum ToolElementKind
    {
        /// <summary>リストの末尾へ加える。</summary>
        Add,

        /// <summary>リストから取り除く。</summary>
        Remove,

        /// <summary>リストに在る要素を台帳へ預けて、そのハンドルを返す。</summary>
        Hold,
    }

    /// <summary>所有するリストの要素を相手にするツール1件。</summary>
    public sealed class ToolElements
    {
        public ToolElements(ToolElementKind kind, ToolReceiver receiver, ToolAccess access)
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

            Kind = kind;
            Receiver = receiver;
            Access = access;
        }

        /// <summary>そのツールが要素に対して行うこと。</summary>
        public ToolElementKind Kind { get; }

        /// <summary>そのリストを持つ受け手の得方。</summary>
        public ToolReceiver Receiver { get; }

        /// <summary>相手にするPMXから要素へ至る道。</summary>
        public ToolAccess Access { get; }
    }
}
