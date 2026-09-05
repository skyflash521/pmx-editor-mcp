using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>能力対応表の行の種別。</summary>
    public enum ToolMapRowKind
    {
        /// <summary>個別のツールを持たず、共通契約の側が受け持つ行。</summary>
        CommonContract,

        /// <summary>イベントの取り出しが返す種別の分岐に当たる行。</summary>
        EventBranch,

        /// <summary>独立したツールを持たず、ほかのツールかイベントの分岐へ埋め込まれる行。</summary>
        SchemaEmbedded,

        /// <summary>独立したツールを持ってSDKのメンバーへ中継する行。</summary>
        DirectDispatch,
    }

    /// <summary>編集の分類。</summary>
    public enum ToolMapEditKind
    {
        /// <summary>いまの状態を複製して変え、まとめて反映するもの。</summary>
        DuplicateEdit,

        /// <summary>モデルのデータ・長寿命のオブジェクト・ファイルへ直に作用するもの。</summary>
        DirectChange,

        /// <summary>表示と設定とセッションだけを動かすもの。</summary>
        ViewSession,

        /// <summary>どちらへも作用しないもの。</summary>
        Read,
    }

    /// <summary>反映の後に呼ぶ表示更新。</summary>
    public enum RefreshTarget
    {
        /// <summary>モデルの実体。</summary>
        Model,

        /// <summary>フォームの一覧。</summary>
        List,

        /// <summary>描画。</summary>
        View,
    }

    /// <summary>事後条件の判定が当たる効果の種別。</summary>
    public enum EffectType
    {
        /// <summary>ファイルが生まれる。</summary>
        FileWritten,

        /// <summary>ハンドルが使えなくなる。</summary>
        HandleConsumed,

        /// <summary>依存するハンドルまで連れて使えなくなる。</summary>
        HandleCascaded,

        /// <summary>ハンドルが新しく出る。</summary>
        HandleCreated,

        /// <summary>件数が変わる。</summary>
        CountChanged,

        /// <summary>指した状態そのものが書き換わる。</summary>
        StateWritten,

        /// <summary>外から読める値のどれかが変わる。</summary>
        ObservableChange,

        /// <summary>値を読み出す。</summary>
        ValueRead,

        /// <summary>外から観測できる効果を持たない。</summary>
        None,
    }

    /// <summary>事後条件の確かめ方。</summary>
    public enum EffectCheckKind
    {
        /// <summary>読み取りのツールで読み戻して比べる。</summary>
        Readback,

        /// <summary>ファイルの生成を見る。</summary>
        File,

        /// <summary>ハンドルの状態の変化を見る。</summary>
        Handle,

        /// <summary>ディスパッチの記録だけを合格とする。</summary>
        CallLogOnly,
    }

    /// <summary>観測した値と期待の比べ方。</summary>
    public enum EffectComparison
    {
        /// <summary>期待と等しい。</summary>
        Equals,

        /// <summary>在る。</summary>
        Exists,

        /// <summary>以後ハンドルとして使えない。</summary>
        Invalidated,

        /// <summary>呼ぶ前の値との差が期待に等しい。</summary>
        DeltaEquals,

        /// <summary>観測した値の少なくとも1つが呼ぶ前と違う。</summary>
        AnyChanged,
    }

    /// <summary>事後条件が値を指せる名前空間。接頭辞で見分ける。</summary>
    public static class ReferenceSpace
    {
        /// <summary>ツールが直に受け取る引数。</summary>
        public const string Arg = "arg:";

        /// <summary>SDKのシグネチャの引数。</summary>
        public const string SdkArg = "sdkArg:";

        /// <summary>その行の応答から取った値。</summary>
        public const string Result = "result:";

        /// <summary>用意の操作が出した値。</summary>
        public const string SetupOut = "setupOut:";
    }

    /// <summary>用意の操作のタグ。</summary>
    public enum SetupTag
    {
        /// <summary>空のモデルへ初期化する。</summary>
        InitPmx,

        /// <summary>要素型を1件足す。</summary>
        AddElement,

        /// <summary>ツールを1回呼ぶ。</summary>
        CallTool,
    }

    /// <summary>
    /// 事後条件の用意の操作1件。タグごとに持つ項目が変わるので、取らない項目は null で置く。
    /// </summary>
    public sealed class SetupOperation
    {
        private SetupOperation(
            SetupTag tag,
            string elementType,
            string toolName,
            IDictionary<string, object> args,
            string outName)
        {
            Tag = tag;
            ElementType = elementType;
            ToolName = toolName;
            Args = args == null
                ? null
                : new ReadOnlyDictionary<string, object>(
                    new Dictionary<string, object>(args, StringComparer.Ordinal));
            Out = outName;
        }

        public SetupTag Tag { get; }

        /// <summary>足す要素型。<see cref="SetupTag.AddElement"/> だけが持つ。</summary>
        public string ElementType { get; }

        /// <summary>呼ぶツールの名前。<see cref="SetupTag.CallTool"/> だけが持つ。</summary>
        public string ToolName { get; }

        /// <summary>呼ぶときの引数の束縛。<see cref="SetupTag.CallTool"/> だけが持つ。</summary>
        public IDictionary<string, object> Args { get; }

        /// <summary>出した値の名前。出さない操作では null。</summary>
        public string Out { get; }

        public static SetupOperation InitPmx()
        {
            return new SetupOperation(SetupTag.InitPmx, null, null, null, null);
        }

        public static SetupOperation AddElement(string elementType, string outName)
        {
            PropertyRecord.RequireText(elementType, nameof(elementType));
            return new SetupOperation(SetupTag.AddElement, elementType, null, null, outName);
        }

        public static SetupOperation CallTool(
            string toolName, IDictionary<string, object> args, string outName)
        {
            PropertyRecord.RequireText(toolName, nameof(toolName));
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            return new SetupOperation(SetupTag.CallTool, null, toolName, args, outName);
        }
    }

    /// <summary>事後条件の判定1件。</summary>
    public sealed class Postcondition
    {
        public Postcondition(
            EffectType effectType,
            string effectKey,
            EffectCheckKind kind,
            string observerTool,
            IDictionary<string, string> observerArgs,
            string valuePath,
            EffectComparison comparison,
            object expected,
            bool hasExpected,
            IList<SetupOperation> setup)
        {
            if (effectKey == null)
            {
                throw new ArgumentNullException(nameof(effectKey));
            }

            EffectType = effectType;
            EffectKey = effectKey;
            Kind = kind;
            ObserverTool = observerTool;
            ObserverArgs = observerArgs == null
                ? null
                : new ReadOnlyDictionary<string, string>(
                    new Dictionary<string, string>(observerArgs, StringComparer.Ordinal));
            ValuePath = valuePath;
            Comparison = comparison;
            Expected = expected;
            HasExpected = hasExpected;
            Setup = setup == null ? null : new ReadOnlyCollection<SetupOperation>(setup);
        }

        public EffectType EffectType { get; }

        /// <summary>効果を行の中で一意にする文字列。1つしか無い種別では空文字。</summary>
        public string EffectKey { get; }

        public EffectCheckKind Kind { get; }

        /// <summary>観測に使うツールの名前。持たない判定では null。</summary>
        public string ObserverTool { get; }

        /// <summary>観測ツールの引数の名前から引く参照元。持たない判定では null。</summary>
        public IDictionary<string, string> ObserverArgs { get; }

        /// <summary>比べる値の位置。持たない判定では null。</summary>
        public string ValuePath { get; }

        public EffectComparison Comparison { get; }

        /// <summary>期待。JSONのリテラルか参照元1つ。持たない判定では null。</summary>
        public object Expected { get; }

        /// <summary>期待を持つか。null そのものを期待にできるので、値の有無とは別に持つ。</summary>
        public bool HasExpected { get; }

        /// <summary>既知の状態を作る操作の列。持たない判定では null。</summary>
        public IList<SetupOperation> Setup { get; }

        /// <summary>効果を行の中で一意にする組。</summary>
        public string EffectId
        {
            get { return EffectType + "/" + EffectKey; }
        }

        /// <summary>
        /// 判定が束縛できる位置に置かれた文字列。参照元が現れうる場所はこの3つで尽きるので、
        /// どこを見るかはここに一度だけ書く。接頭辞で見分けるのは読む側の役目とする。
        /// </summary>
        public IEnumerable<string> Bound
        {
            get
            {
                IEnumerable<object> values = (ObserverArgs == null
                        ? Enumerable.Empty<object>()
                        : ObserverArgs.Values.Cast<object>())
                    .Concat(new[] { Expected })
                    .Concat(Setup == null
                        ? Enumerable.Empty<object>()
                        : Setup.Where(o => o.Args != null).SelectMany(o => o.Args.Values));

                return values.OfType<string>();
            }
        }
    }

    /// <summary>複製編集型の行が持つ反映の指定。</summary>
    public sealed class UpdateSpec
    {
        public UpdateSpec(string update, IList<RefreshTarget> refresh)
        {
            if (refresh == null)
            {
                throw new ArgumentNullException(nameof(refresh));
            }

            Update = update;
            Refresh = new ReadOnlyCollection<RefreshTarget>(refresh);
        }

        /// <summary>反映を一部に限るときの列挙子名。全体を反映する行では null。</summary>
        public string Update { get; }

        /// <summary>反映の後に呼ぶ表示更新。呼ばない行では空。</summary>
        public IList<RefreshTarget> Refresh { get; }
    }

    /// <summary>能力対応表の行1件。行の種別ごとに持つ項目が変わるので、取らない項目は null で置く。</summary>
    public sealed class ToolMapRow
    {
        public ToolMapRow(
            string signatureKey,
            IList<string> capabilityIds,
            ToolMapRowKind rowKind,
            ToolMapEditKind editKind,
            OperationDirection direction,
            DangerKind? dangerKind,
            UpdateSpec updateSpec,
            string note,
            string basis,
            string tool,
            IList<Postcondition> postcondition,
            CommonAssignmentKind? assignment,
            string target,
            SlotBinding slotBinding,
            string eventType,
            IList<string> embeddedIn)
        {
            PropertyRecord.RequireText(signatureKey, nameof(signatureKey));
            PropertyRecord.RequireText(basis, nameof(basis));
            if (capabilityIds == null)
            {
                throw new ArgumentNullException(nameof(capabilityIds));
            }

            SignatureKey = signatureKey;
            CapabilityIds = new ReadOnlyCollection<string>(capabilityIds);
            RowKind = rowKind;
            EditKind = editKind;
            Direction = direction;
            DangerKind = dangerKind;
            UpdateSpec = updateSpec;
            Note = note;
            Basis = basis;
            Tool = tool;
            Postcondition = postcondition == null
                ? null
                : new ReadOnlyCollection<Postcondition>(postcondition);
            Assignment = assignment;
            Target = target;
            SlotBinding = slotBinding;
            EventType = eventType;
            EmbeddedIn = embeddedIn == null ? null : new ReadOnlyCollection<string>(embeddedIn);
        }

        public string SignatureKey { get; }

        /// <summary>その行を出した提供能力のID。1件以上。</summary>
        public IList<string> CapabilityIds { get; }

        public ToolMapRowKind RowKind { get; }

        public ToolMapEditKind EditKind { get; }

        public OperationDirection Direction { get; }

        /// <summary>危険操作に当たる行だけが持つ。</summary>
        public DangerKind? DangerKind { get; }

        /// <summary>複製編集型の行だけが持つ。</summary>
        public UpdateSpec UpdateSpec { get; }

        /// <summary>台帳の契約注記の転記。持たない行では null。</summary>
        public string Note { get; }

        /// <summary>編集の分類と反映の指定をそう決めた根拠の一文。</summary>
        public string Basis { get; }

        /// <summary>ツールの名前。直接ディスパッチの行だけが持つ。</summary>
        public string Tool { get; }

        /// <summary>事後条件。直接ディスパッチの行だけが持つ。</summary>
        public IList<Postcondition> Postcondition { get; }

        /// <summary>共通契約割当行だけが持つ割当の種別。</summary>
        public CommonAssignmentKind? Assignment { get; }

        /// <summary>共通契約割当行だけが持つ割当の対象名。</summary>
        public string Target { get; }

        /// <summary>共通契約割当行だけが持つ束縛。</summary>
        public SlotBinding SlotBinding { get; }

        /// <summary>イベント行だけが持つ分岐の種別。</summary>
        public string EventType { get; }

        /// <summary>スキーマ埋め込み行だけが持つ埋め込み先の名前。1件以上。</summary>
        public IList<string> EmbeddedIn { get; }
    }

    /// <summary>能力対応表。</summary>
    public sealed class ToolMap
    {
        public ToolMap(IList<ToolMapRow> rows)
        {
            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            Rows = new ReadOnlyCollection<ToolMapRow>(rows);
        }

        public IList<ToolMapRow> Rows { get; }
    }
}
