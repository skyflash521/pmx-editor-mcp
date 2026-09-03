using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>取り返しの付かなさの種別。</summary>
    public enum DangerKind
    {
        /// <summary>エディタそのものを終わらせる。</summary>
        Shutdown,

        /// <summary>ファイルへ書き込む。</summary>
        Overwrite,

        /// <summary>いま開いているPMXの中身を一度に空にする。</summary>
        Reset,
    }

    /// <summary>
    /// どのシグネチャが危険操作に当たるかを決める。名前だけで決めると意味の近い別のメンバーを
    /// 巻き込むので、宣言型と組で名指しできるものはそうする。
    /// </summary>
    public static class DangerousOperationRule
    {
        private const string FormConnectorTypeName = "PEPlugin.Form.IPEFormConnector";

        private const string PmxTypeName = "PEPlugin.Pmx.IPXPmx";

        private const string SavePrefix = "Save";

        private const string ToFileMemberName = "ToFile";

        private const string CloseMemberName = "Close";

        private const string ClearMemberName = "Clear";

        private static readonly ReadOnlyCollection<string> ResetFormMembers =
            Array.AsReadOnly(new[] { "InitializePMD", "InitializePMX" });

        /// <summary>種別が決まれば真。決まらなければ偽で、<paramref name="kind"/> は既定のまま。</summary>
        public static bool TryClassify(SignatureRecord signature, out DangerKind kind)
        {
            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }

            string type = TypeDefinitionName.Of(signature.DeclaringType);
            string member = signature.MemberName;

            if (IsFormMember(type, member, CloseMemberName))
            {
                kind = DangerKind.Shutdown;
                return true;
            }

            if (member.StartsWith(SavePrefix, StringComparison.Ordinal)
                || string.Equals(member, ToFileMemberName, StringComparison.Ordinal))
            {
                kind = DangerKind.Overwrite;
                return true;
            }

            if (ResetFormMembers.Any(m => IsFormMember(type, member, m))
                || (string.Equals(type, PmxTypeName, StringComparison.Ordinal)
                    && string.Equals(member, ClearMemberName, StringComparison.Ordinal)))
            {
                kind = DangerKind.Reset;
                return true;
            }

            kind = default(DangerKind);

            return false;
        }

        /// <summary>渡したシグネチャのうち、危険操作に当たるものを行キーから種別へ引く形で返す。</summary>
        public static IDictionary<string, DangerKind> Classify(IEnumerable<SignatureRecord> signatures)
        {
            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            Dictionary<string, DangerKind> found =
                new Dictionary<string, DangerKind>(StringComparer.Ordinal);
            foreach (SignatureRecord signature in signatures)
            {
                DangerKind kind;
                if (TryClassify(signature, out kind))
                {
                    found[signature.Key] = kind;
                }
            }

            return found;
        }

        private static bool IsFormMember(string type, string member, string name)
        {
            return string.Equals(type, FormConnectorTypeName, StringComparison.Ordinal)
                && string.Equals(member, name, StringComparison.Ordinal);
        }
    }
}
