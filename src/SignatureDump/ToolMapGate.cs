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

            ISet<string> assigned = new HashSet<string>(
                assignments.Assignments.Select(a => a.SignatureKey), StringComparer.Ordinal);
            foreach (ToolMapRow row in map.Rows)
            {
                RequireProvided(row, evidence);
                RequireRowKind(row, evidence, assigned);
                RequireCapabilities(row, evidence);
                RequireDirection(row, evidence);
                RequireDangerKind(row, evidence);
                RequireNote(row, evidence);
                RequireUpdateKind(row, evidence);
                RequireSetup(row, evidence);
                RequireSdkArguments(row, evidence);
            }

            RequireCommonContract(map, assignments);
        }

        private static void RequireProvided(ToolMapRow row, ToolMapEvidence evidence)
        {
            if (!evidence.Provided.Contains(row.SignatureKey))
            {
                throw new InvalidOperationException(
                    "提供対象でないシグネチャの行がある: " + row.SignatureKey);
            }
        }

        private static void RequireCapabilities(ToolMapRow row, ToolMapEvidence evidence)
        {
            ISet<string> expected;
            if (!evidence.Owners.TryGetValue(row.SignatureKey, out expected))
            {
                throw new InvalidOperationException(
                    "台帳のどの行も指していないシグネチャの行がある: " + row.SignatureKey);
            }

            if (!expected.SetEquals(row.CapabilityIds))
            {
                throw new InvalidOperationException(
                    "提供能力のIDが台帳と合わない: " + row.SignatureKey
                        + "(表: " + Join(row.CapabilityIds) + " / 台帳: " + Join(expected) + ")");
            }
        }

        private static void RequireDirection(ToolMapRow row, ToolMapEvidence evidence)
        {
            SignatureRecord signature;
            if (!evidence.Signatures.TryGetValue(row.SignatureKey, out signature))
            {
                throw new InvalidOperationException(
                    "公開API列挙に無いシグネチャの行がある: " + row.SignatureKey);
            }

            if (signature.OperationDirection != row.Direction)
            {
                throw new InvalidOperationException(
                    "操作の向きがシグネチャから決まる向きと合わない: " + row.SignatureKey
                        + "(表: " + row.Direction + " / 決まる向き: "
                        + signature.OperationDirection + ")");
            }
        }

        private static void RequireDangerKind(ToolMapRow row, ToolMapEvidence evidence)
        {
            DangerKind kind;
            bool dangerous = evidence.Dangers.TryGetValue(row.SignatureKey, out kind);
            if (dangerous && row.DangerKind != kind)
            {
                throw new InvalidOperationException(
                    "危険操作の種別が規則の判定と合わない: " + row.SignatureKey
                        + "(表: " + Written(row.DangerKind) + " / 判定: " + kind + ")");
            }

            if (!dangerous && row.DangerKind.HasValue)
            {
                throw new InvalidOperationException(
                    "危険操作でない行が種別を持つ: " + row.SignatureKey);
            }
        }

        /// <summary>
        /// 備考は台帳の契約注記の写しなので、内容まで突き合わせる。持たない能力しか指していない行は
        /// 備考を任意とするので問わない。
        /// </summary>
        private static void RequireNote(ToolMapRow row, ToolMapEvidence evidence)
        {
            string expected = string.Join("。", row.CapabilityIds
                .OrderBy(id => id, StringComparer.Ordinal)
                .Where(evidence.Notes.ContainsKey)
                .Select(id => evidence.Notes[id])
                .Distinct(StringComparer.Ordinal));
            if (expected.Length == 0)
            {
                return;
            }

            if (row.Note == null)
            {
                throw new InvalidOperationException(
                    "契約注記を持つ提供能力から出た行に備考の転記が無い: " + row.SignatureKey);
            }

            if (!string.Equals(row.Note, expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "備考が台帳の契約注記と合わない: " + row.SignatureKey
                        + "(表: " + row.Note + " / 台帳: " + expected + ")");
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

        /// <summary>
        /// 用意の操作が指す要素型と型は別の正本が持つ語なので、そこに実在することまで求める。
        /// </summary>
        private static void RequireSetup(ToolMapRow row, ToolMapEvidence evidence)
        {
            if (row.Postcondition == null)
            {
                return;
            }

            foreach (SetupOperation operation in row.Postcondition
                .Where(j => j.Setup != null).SelectMany(j => j.Setup))
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

        /// <summary>
        /// 行の種別が、行の外の材料から導いた種別と一致することを求める。書き手が種別を選べると、
        /// 種別ごとの必須項目の検査そのものを取り違えた種別へ逃がせる。
        /// </summary>
        private static void RequireRowKind(
            ToolMapRow row, ToolMapEvidence evidence, ISet<string> assigned)
        {
            ToolMapRowKind derived = RowKindRule.Of(
                evidence.Signatures[row.SignatureKey].MemberKind,
                assigned.Contains(row.SignatureKey));
            if (row.RowKind != derived)
            {
                throw new InvalidOperationException(
                    "行の種別が導いた種別と合わない: " + row.SignatureKey
                        + "(表: " + row.RowKind + " / 規則: " + derived + ")");
            }
        }

        /// <summary>
        /// 共通契約割当行が、特別規則の表と行キーで完全一致し、割当も束縛も同じであることを求める。
        /// 対象名の実在を見るだけでは割当の取り違えを防げない。
        /// </summary>
        private static void RequireCommonContract(ToolMap map, CommonAssignmentTable assignments)
        {
            Dictionary<string, CommonAssignmentRecord> expected = assignments.Assignments
                .ToDictionary(a => a.SignatureKey, a => a, StringComparer.Ordinal);
            List<ToolMapRow> rows = map.Rows
                .Where(r => r.RowKind == ToolMapRowKind.CommonContract).ToList();

            foreach (ToolMapRow row in rows)
            {
                CommonAssignmentRecord assignment = expected[row.SignatureKey];
                if (row.Assignment != assignment.Assignment)
                {
                    throw new InvalidOperationException(
                        "割当が特別規則の表と合わない: " + row.SignatureKey
                            + "(表: " + row.Assignment + " / 正本: " + assignment.Assignment + ")");
                }

                if (!string.Equals(row.Target, assignment.Target, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "割当の対象名が特別規則の表と合わない: " + row.SignatureKey
                            + "(表: " + row.Target + " / 正本: " + assignment.Target + ")");
                }

                if (!row.SlotBinding.SameAs(assignment.SlotBinding))
                {
                    throw new InvalidOperationException(
                        "束縛が特別規則の表と合わない: " + row.SignatureKey
                            + "(表: " + row.SlotBinding + " / 正本: " + assignment.SlotBinding + ")");
                }
            }

            IEnumerable<string> missing = expected.Keys.Except(
                rows.Select(r => r.SignatureKey), StringComparer.Ordinal);
            string first = missing.OrderBy(k => k, StringComparer.Ordinal).FirstOrDefault();
            if (first != null)
            {
                throw new InvalidOperationException(
                    "特別規則の表の項目に対応する共通契約割当行が無い: " + first);
            }
        }

        private static string Written(DangerKind? kind)
        {
            return kind.HasValue ? kind.Value.ToString() : "無し";
        }

        private static string Join(IEnumerable<string> values)
        {
            return string.Join("・", values.OrderBy(v => v, StringComparer.Ordinal));
        }
    }
}
