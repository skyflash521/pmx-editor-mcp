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
            if (!string.Equals(
                    TypeDefinitionName.Of(signature.DeclaringType),
                    GuideTypeName,
                    StringComparison.Ordinal)
                || !string.Equals(signature.MemberName, PickingMemberName, StringComparison.Ordinal))
            {
                return false;
            }

            kind = PreconditionKind.PickedObjects;

            return true;
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
