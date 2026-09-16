using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 能力対応表を、シグネチャの側から機械で決まるものと照合する。問うのは載っている行についてで、
    /// 母集合との過不足は見ない。
    /// </summary>
    public static class ToolMapGate
    {
        /// <summary>
        /// 効果を呼び出しの記録だけで確かめると述べる言い回し。正本が既に使っているものへ揃える。
        /// </summary>
        private const string LoggedOnlyReason = "呼び出しの記録で確かめる";

        /// <summary>食い違いがあれば <see cref="InvalidOperationException"/>。</summary>
        public static void Require(
            ToolMap map,
            ToolMapEvidence evidence,
            CommonAssignmentTable assignments)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            if (assignments == null)
            {
                throw new ArgumentNullException(nameof(assignments));
            }

            IDictionary<string, ToolMapRowKind> kinds = RowKinds(map, evidence, assignments);
            foreach (ToolMapRow row in map.Rows)
            {
                RequireProvided(row, evidence, kinds);
            }

            foreach (ToolMapRow row in map.Rows)
            {
                RequireFields(row, kinds[row.SignatureKey]);
                RequireUpdateKind(row, evidence);
                RequireSetup(row, evidence);
                RequireObservedOrExplained(row);
                RequireNoBannedReason(row);
                RequireSdkArguments(row, evidence);
            }

            RequireCommonContract(map, kinds, assignments);
        }

        /// <summary>
        /// 行キーから、その行が採る種別を引く表。行は種別を書かないので、照合も要約もここから引く。
        /// 行キーが公開API列挙に在ることを前提とする。
        /// </summary>
        public static IDictionary<string, ToolMapRowKind> RowKinds(
            ToolMap map, ToolMapEvidence evidence, CommonAssignmentTable assignments)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            if (assignments == null)
            {
                throw new ArgumentNullException(nameof(assignments));
            }

            return RowKindRule.Resolve(
                map,
                evidence.Signatures,
                evidence.EmbeddedTypes,
                new HashSet<string>(
                    assignments.Assignments.Select(a => a.SignatureKey), StringComparer.Ordinal),
                evidence.IndependentTypes,
                evidence.HandleTypes,
                evidence.ElementCollections,
                evidence.Traversed);
        }

        /// <summary>
        /// その行が、引数の組の項目そのものを表す行か。組の項目を持ち込む種別のうち、項目になる
        /// プロパティとフィールドに限る——ほかは組の項目にならないので、台帳が数えない行だけが
        /// 残る。
        /// </summary>
        private static bool Carried(
            ToolMapRow row, ToolMapEvidence evidence, IDictionary<string, ToolMapRowKind> kinds)
        {
            ToolMapRowKind kind;
            SignatureRecord signature;

            return kinds.TryGetValue(row.SignatureKey, out kind)
                && kind == ToolMapRowKind.SchemaEmbedded
                && evidence.Signatures.TryGetValue(row.SignatureKey, out signature)
                && (signature.MemberKind == MemberKind.Property
                    || signature.MemberKind == MemberKind.Field)
                && evidence.Carried.Contains(signature.DeclaringType);
        }

        private static void RequireProvided(
            ToolMapRow row, ToolMapEvidence evidence, IDictionary<string, ToolMapRowKind> kinds)
        {
            if (!evidence.Provided.Contains(row.SignatureKey)
                && !Carried(row, evidence, kinds))
            {
                throw new InvalidOperationException(
                    "提供対象でないシグネチャの行がある: " + row.SignatureKey);
            }
        }

        /// <summary>反映の指定は列挙子の名前なので、その列挙型に実在することまで求める。</summary>
        private static void RequireUpdateKind(ToolMapRow row, ToolMapEvidence evidence)
        {
            string update = row.UpdateSpec == null ? null : row.UpdateSpec.Update;
            if (update != null && !evidence.UpdateKinds.Contains(update))
            {
                throw new InvalidOperationException(
                    "反映の指定が列挙型に無い: " + row.SignatureKey + "(" + update + ")");
            }
        }

        /// <summary>その行が持つ用意の操作。呼ぶ前の段取りと、判定ごとの段取りの両方を並べる。</summary>
        internal static IEnumerable<SetupOperation> Setups(ToolMapRow row)
        {
            return (row.Setup ?? (IList<SetupOperation>)new SetupOperation[0])
                .Concat((row.Postcondition ?? (IList<Postcondition>)new Postcondition[0])
                    .SelectMany(j => j.Setup ?? (IList<SetupOperation>)new SetupOperation[0]));
        }

        /// <summary>
        /// 用意の操作が指す要素型と型は別の正本が持つ語なので、そこに実在することまで求める。
        /// </summary>
        private static void RequireSetup(ToolMapRow row, ToolMapEvidence evidence)
        {
            foreach (SetupOperation operation in Setups(row))
            {
                if (operation.ElementType != null
                    && !evidence.ElementNouns.Contains(operation.ElementType))
                {
                    throw new InvalidOperationException(
                        "用意の操作が足す要素型が型役割表に無い: " + row.SignatureKey
                            + "(" + operation.ElementType + ")");
                }

                if (operation.Args == null)
                {
                    continue;
                }

                foreach (string type in operation.Args.Values
                    .Select(SampledType).Where(t => t != null))
                {
                    if (!evidence.TypeNames.Contains(type))
                    {
                        throw new InvalidOperationException(
                            "サンプル値を引く型が公開API列挙に無い: " + row.SignatureKey
                                + "(" + type + ")");
                    }
                }
            }
        }

        /// <summary>
        /// 根拠へ書いてはならない文言。実機の検査が届かないことを述べる綴りで、書いても真偽を
        /// 確かめる相手が無い。届かないことは、ツールの名前と導ける理由だけを載せる覆えないツールの
        /// 正本が受け持つ。
        /// </summary>
        private static readonly string[] Banned =
        {
            "呼び先まで届かせられない",
            "確認の表示が出る",
        };

        /// <summary>根拠が、確かめる相手を持たない文言を持たないことを求める。</summary>
        private static void RequireNoBannedReason(ToolMapRow row)
        {
            foreach (string banned in Banned)
            {
                if (row.Basis.IndexOf(banned, StringComparison.Ordinal) >= 0)
                {
                    throw new InvalidOperationException(
                        "根拠へ書けない文言がある: " + row.SignatureKey
                            + "(「" + banned + "」。実機の検査が届かないことは覆えないツールの"
                            + "正本が受け持つ)");
                }
            }
        }

        /// <summary>
        /// 効果を呼び出しの記録だけで確かめる行に、確かめられない理由を求める。確かめられるのに
        /// 確かめていないのか、確かめられないから記録で済ませているのかは、行を読むだけでは
        /// 分かれない。理由の言い回しをここが指定するので、書き手が書いたかどうかは機械で見える
        /// ——書いた理由が本当かどうかは見えないので、そこはレビューが受け持つ。
        /// </summary>
        private static void RequireObservedOrExplained(ToolMapRow row)
        {
            if (row.Postcondition == null
                || !row.Postcondition.Any(j => Observed(j.EffectType)
                    && j.Kind == EffectCheckKind.CallLogOnly)
                || row.Basis.IndexOf(LoggedOnlyReason, StringComparison.Ordinal) >= 0)
            {
                return;
            }

            throw new InvalidOperationException(
                "効果を確かめられない理由を述べていない: " + row.SignatureKey
                    + "(根拠へ「" + LoggedOnlyReason + "」を含む一文を書く)");
        }

        /// <summary>
        /// 出たハンドルと、見える状態の変化のどちらかか。この2つだけを挙げるのは、実機へ投げる
        /// 検査が呼んだ後に見に行く手立てを持つのがこの2つだからである——ハンドルは観測ツールで
        /// 引き、見える状態の変化は一覧を読み比べる。ほかの効果まで広げるなら、その効果を見に行く
        /// 段を組み立てられるようにしてからここへ足す。
        /// </summary>
        private static bool Observed(EffectType effectType)
        {
            return effectType == EffectType.HandleCreated
                || effectType == EffectType.ObservableChange;
        }

        /// <summary>
        /// 事後条件がSDKの引数を指すときは、その引数がその行のシグネチャに実在することまで求める。
        /// ファイルの生成を見る判定は、確かめるパスを取る引数を効果の識別子で指す。
        /// </summary>
        private static void RequireSdkArguments(ToolMapRow row, ToolMapEvidence evidence)
        {
            if (row.Postcondition == null)
            {
                return;
            }

            SignatureRecord signature = evidence.Signatures[row.SignatureKey];
            foreach (Postcondition judgement in row.Postcondition)
            {
                IEnumerable<string> names = judgement.Bound
                    .Where(r => r.StartsWith(ReferenceSpace.SdkArg, StringComparison.Ordinal))
                    .Select(r => r.Substring(ReferenceSpace.SdkArg.Length));
                if (judgement.Kind == EffectCheckKind.File)
                {
                    names = names.Concat(new[] { judgement.EffectKey });
                }

                foreach (string name in names)
                {
                    if (!signature.Parameters.Any(
                        p => string.Equals(p.Name, name, StringComparison.Ordinal)))
                    {
                        throw new InvalidOperationException(
                            "事後条件が指すSDKの引数がシグネチャに無い: " + row.SignatureKey
                                + "(" + name + ")");
                    }
                }
            }
        }

        /// <summary>サンプル値への参照が指す型の名前。参照でない束縛では null。</summary>
        private static string SampledType(object value)
        {
            string text = value as string;
            if (text == null)
            {
                return null;
            }

            foreach (string prefix in new[] { "sample:", "sample2:" })
            {
                if (text.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return text.Substring(prefix.Length);
                }
            }

            return null;
        }

        /// <summary>導いた種別ごとに、持たなければならない項目と持ってはならない項目を求める。</summary>
        private static void RequireFields(ToolMapRow row, ToolMapRowKind kind)
        {
            bool dispatch = kind == ToolMapRowKind.DirectDispatch;
            RequireField(row, row.Postcondition != null, dispatch, "postcondition");
            RequireField(
                row, row.EventType != null, kind == ToolMapRowKind.EventBranch, "eventType");
            RequireField(
                row, row.EmbeddedIn != null, kind == ToolMapRowKind.SchemaEmbedded, "embeddedIn");
        }

        private static void RequireField(
            ToolMapRow row, bool written, bool required, string name)
        {
            if (required && !written)
            {
                throw new InvalidOperationException(
                    "導いた種別が求める項目が無い: " + row.SignatureKey + "(" + name + ")");
            }

            if (!required && written)
            {
                throw new InvalidOperationException(
                    "導いた種別が持てない項目がある: " + row.SignatureKey + "(" + name + ")");
            }
        }

        /// <summary>
        /// 共通契約割当の正本の項目が、すべて行を持つことを求める。割当の内容はその正本が持つので、
        /// ここで見るのは行の側に載っているかどうかだけである。
        /// </summary>
        private static void RequireCommonContract(
            ToolMap map,
            IDictionary<string, ToolMapRowKind> kinds,
            CommonAssignmentTable assignments)
        {
            IEnumerable<string> assigned = map.Rows
                .Where(r => kinds[r.SignatureKey] == ToolMapRowKind.CommonContract)
                .Select(r => r.SignatureKey);
            IEnumerable<string> missing = assignments.Assignments
                .Select(a => a.SignatureKey)
                .Except(assigned, StringComparer.Ordinal);
            string first = missing.OrderBy(k => k, StringComparer.Ordinal).FirstOrDefault();
            if (first != null)
            {
                throw new InvalidOperationException(
                    "特別規則の表の項目に対応する共通契約割当行が無い: " + first);
            }
        }
    }
}
