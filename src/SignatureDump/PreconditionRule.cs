using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>呼ぶ前に確かめることの種別。</summary>
    public enum PreconditionKind
    {
        /// <summary>確かめることは無い。</summary>
        None,

        /// <summary>
        /// いま選ばれている対象を相手にする。選ばれているものが無いとエディタが人の応答を待つ表示を
        /// 出し、押されている修飾キーで相手の決まり方が変わる。どちらも呼ぶ前に確かめられる。
        /// </summary>
        PickedObjects,

        /// <summary>
        /// 取り消せる編集が残っていると、エディタが人の応答を待つ表示を出すことがある。出すかどうかは
        /// エディタが持つ保存済みの印との差で決まり、その印は読めない。プラグインからの保存もその印を
        /// 更新しないので、こちらから出ないと言えるのは、取り消せる編集が残っていないときだけである。
        /// </summary>
        SavedEdits,

        /// <summary>
        /// PMXビューの絞込が持つ一覧を相手にする。この一覧は絞込の窓を一度表示するまで組まれず、
        /// 組まれていない間、読み取りは並んでいる項目が無いまま空を返し、書き込みはどの項目へも
        /// 届かない。項目の数は呼ぶ前に読める。
        /// </summary>
        ListedParts,

        /// <summary>
        /// 操作を1つ取り消すか、やり直す。取り消せる操作・やり直せる操作が残っていないと、エディタは
        /// 何もせずに戻る。残っている数は呼ぶ前に読める。
        /// </summary>
        UndoHistory,

        /// <summary>
        /// TransformView のビューで選んでいるボーンを、入力欄の値だけ動かす。ビューでボーンが
        /// 選ばれていないと、エディタは何もせずに戻る。動かす量は押されている修飾キーで向きと倍率が
        /// 変わる。ビューの選択は読めず、呼ぶ前に読めるのは一覧で選ばれているボーンの位置だけである。
        /// 表示されているボーンを一覧で選ぶと、ビューの選択も同じボーンになる。
        /// </summary>
        TransformedBone,
    }

    /// <summary>
    /// どのシグネチャが、呼ぶ前に確かめることを持つかを決める。危険操作と同じく、名前だけで決めると
    /// 意味の近い別のメンバーを巻き込むので、宣言型と組で名指しする。
    /// </summary>
    public static class PreconditionRule
    {
        private const string GuideTypeName = "PEPlugin.View.IPEVertexGuideConnector";

        private const string PickingMemberName = "GetSelectedCurrentVertex";

        private const string ViewTypeName = "PEPlugin.View.IPEPMDViewConnector";

        private const string FormTypeName = "PEPlugin.Form.IPEFormConnector";

        private const string ClosingMemberName = "Close";

        private const string UndoCountMemberName = "UndoCount";

        private const string PartsTypeName = "PEPlugin.View.IPEPartsSelectConnector";

        private const string TransformTypeName = "PEPlugin.View.IPETransformViewConnector";

        private const string ChosenBoneMemberName = "SelectedBoneIndex";

        private static readonly ReadOnlyDictionary<string, string> RemainingByMember =
            new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "Undo", UndoCountMemberName },
                    { "Redo", "RedoCount" },
                });

        private static readonly ReadOnlyCollection<string> TransformingMembers =
            Array.AsReadOnly(new[] { "BoneRotate", "BoneTranslate", "BoneScaling" });

        private static readonly ReadOnlyDictionary<string, string> CountsByMember =
            new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { "GetCheckedMaterialIndices", "MaterialItemsCount" },
                    { "SetCheckedMaterialIndices", "MaterialItemsCount" },
                    { "GetCheckedBoneIndices", "BoneItemsCount" },
                    { "SetCheckedBoneIndices", "BoneItemsCount" },
                    { "GetCheckedExpressionIndices", "ExpressionItemsCount" },
                    { "SetCheckedExpressionIndices", "ExpressionItemsCount" },
                });

        private static readonly ReadOnlyCollection<string> PickedMembers =
            Array.AsReadOnly(new[]
            {
                "GetSelectedVertexIndices",
                "GetSelectedFaceIndices",
                "GetSelectedBoneIndices",
                "GetSelectedBodyIndices",
                "GetSelectedJointIndices",
            });

        /// <summary>種別が決まれば真。決まらなければ偽で、<paramref name="kind"/> は既定のまま。</summary>
        public static bool TryClassify(SignatureRecord signature, out PreconditionKind kind)
        {
            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }

            kind = PreconditionKind.None;
            string type = TypeDefinitionName.Of(signature.DeclaringType);
            if (string.Equals(type, GuideTypeName, StringComparison.Ordinal)
                && string.Equals(signature.MemberName, PickingMemberName, StringComparison.Ordinal))
            {
                kind = PreconditionKind.PickedObjects;

                return true;
            }

            if (string.Equals(type, FormTypeName, StringComparison.Ordinal)
                && string.Equals(signature.MemberName, ClosingMemberName, StringComparison.Ordinal))
            {
                kind = PreconditionKind.SavedEdits;

                return true;
            }

            if (string.Equals(type, PartsTypeName, StringComparison.Ordinal)
                && CountsByMember.ContainsKey(signature.MemberName))
            {
                kind = PreconditionKind.ListedParts;

                return true;
            }

            if (string.Equals(type, FormTypeName, StringComparison.Ordinal)
                && RemainingByMember.ContainsKey(signature.MemberName))
            {
                kind = PreconditionKind.UndoHistory;

                return true;
            }

            if (string.Equals(type, TransformTypeName, StringComparison.Ordinal)
                && TransformingMembers.Contains(signature.MemberName, StringComparer.Ordinal))
            {
                kind = PreconditionKind.TransformedBone;

                return true;
            }

            return false;
        }

        /// <summary>
        /// そのシグネチャが相手にする絞込の一覧について、並んでいる項目の数を読むシグネチャの行キー。
        /// 相手にする呼び出しと同じ受け手の上に在るので、受け手を解き直さずに読める。相手にする一覧を
        /// 持たないシグネチャと、数を読むシグネチャが見つからないときは null。
        /// </summary>
        public static string Listed(
            SignatureRecord signature, IEnumerable<SignatureRecord> signatures)
        {
            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }

            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            string counting;
            if (!string.Equals(
                    TypeDefinitionName.Of(signature.DeclaringType),
                    PartsTypeName,
                    StringComparison.Ordinal)
                || !CountsByMember.TryGetValue(signature.MemberName, out counting))
            {
                return null;
            }

            return signatures
                .Where(s => string.Equals(
                        TypeDefinitionName.Of(s.DeclaringType),
                        PartsTypeName,
                        StringComparison.Ordinal)
                    && string.Equals(s.MemberName, counting, StringComparison.Ordinal))
                .Select(s => s.Key)
                .OrderBy(k => k, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>
        /// そのシグネチャを呼ぶ前に、呼ぶ先と同じ受け手の上で読む値(数か位置)のシグネチャの行キー。
        /// 受け手を解き直さずに読める。読む値を持たない種別と、読む先が見つからないときは null。
        /// </summary>
        public static string CountingOf(SignatureRecord signature, IEnumerable<SignatureRecord> signatures)
        {
            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }

            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            PreconditionKind kind;
            if (!TryClassify(signature, out kind))
            {
                return null;
            }

            switch (kind)
            {
                case PreconditionKind.SavedEdits:
                    return Counting(signatures);

                case PreconditionKind.ListedParts:
                    return Listed(signature, signatures);

                case PreconditionKind.UndoHistory:
                    return Member(signatures, FormTypeName, RemainingByMember[signature.MemberName]);

                case PreconditionKind.TransformedBone:
                    return Member(signatures, TransformTypeName, ChosenBoneMemberName);

                default:
                    return null;
            }
        }

        private static string Member(IEnumerable<SignatureRecord> signatures, string typeName, string memberName)
        {
            return signatures
                .Where(s => string.Equals(TypeDefinitionName.Of(s.DeclaringType), typeName, StringComparison.Ordinal)
                    && string.Equals(s.MemberName, memberName, StringComparison.Ordinal))
                .Select(s => s.Key)
                .OrderBy(k => k, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>
        /// 取り消せる編集の数を読むシグネチャの行キー。閉じる呼び出しと同じ受け手の上に在るので、
        /// 受け手を解き直さずに読める。見つからなければ null。
        /// </summary>
        public static string Counting(IEnumerable<SignatureRecord> signatures)
        {
            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            return signatures
                .Where(s => string.Equals(
                        TypeDefinitionName.Of(s.DeclaringType), FormTypeName, StringComparison.Ordinal)
                    && string.Equals(s.MemberName, UndoCountMemberName, StringComparison.Ordinal))
                .Select(s => s.Key)
                .OrderBy(k => k, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        /// <summary>
        /// いま選ばれているものを読むシグネチャの行キー。選ばれているものが無いことは、これらが
        /// どれも空を返すことで分かる。
        /// </summary>
        public static IList<string> Picked(IEnumerable<SignatureRecord> signatures)
        {
            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            return signatures
                .Where(s => string.Equals(
                        TypeDefinitionName.Of(s.DeclaringType), ViewTypeName, StringComparison.Ordinal)
                    && PickedMembers.Contains(s.MemberName, StringComparer.Ordinal)
                    && s.Parameters.Count == 0)
                .Select(s => s.Key)
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();
        }
    }
}
