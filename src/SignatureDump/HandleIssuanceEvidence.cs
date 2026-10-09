using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// ハンドルを返しうるシグネチャと、その発行の種別を列挙から導く。新しいハンドルを
    /// 発行するのか既にあるものを返すだけなのかは、ここでは決めない。
    /// </summary>
    public static class HandleIssuanceEvidence
    {
        /// <summary>どの型の値も入る戻り値の綴り。この綴りは預ける型を決めない。</summary>
        private const string ObjectTypeName = "System.Object";

        /// <summary>その行が生成物を台帳へ預けるか。効果にハンドルの発行が並ぶ行が当たる。</summary>
        public static bool Issues(ToolMapRow row, SignatureRecord signature)
        {
            if (row == null)
            {
                throw new ArgumentNullException(nameof(row));
            }

            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }

            return !string.Equals(signature.ValueType, "System.Void", StringComparison.Ordinal)
                && row.Postcondition != null
                && row.Postcondition.Any(p => p.EffectType == EffectType.HandleCreated);
        }

        /// <summary>
        /// エディタが返す型の表を持たずに、台帳へ預ける生成物の型を決める。戻り値の綴りが
        /// <see cref="object"/> の行は受け手の型で預ける。
        /// </summary>
        public static string Issued(SignatureRecord signature)
        {
            return Issued(signature, null);
        }

        /// <summary>
        /// その行が台帳へ預ける生成物の型。戻り値の綴りが <see cref="object"/> の行は、
        /// <paramref name="returns"/>(行キーから、エディタがその呼び出しで実際に返すものの型へ引く表)
        /// の型で預ける。表を渡さなければ受け手の型で預ける。表を渡したのに行が無ければ
        /// <see cref="InvalidOperationException"/>。
        /// </summary>
        public static string Issued(SignatureRecord signature, IDictionary<string, string> returns)
        {
            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }

            string made = ValueTypeName.Contained(signature.ValueType);
            if (!string.Equals(made, ObjectTypeName, StringComparison.Ordinal))
            {
                return made;
            }

            if (returns == null)
            {
                return TypeDefinitionName.Of(signature.DeclaringType);
            }

            string returned;
            if (!returns.TryGetValue(signature.Key, out returned))
            {
                throw new InvalidOperationException(
                    "戻り値の綴りが object の行が、エディタが返す型の表に無い: " + signature.Key);
            }

            return returned;
        }

        /// <summary>
        /// どれかの行が作ると述べる型の名前。作る行の戻り値から並びと配列の印を外して集め、枝の型を
        /// 作れる抽象の型も併せて持つ。<paramref name="concrete"/> は抽象の型からその枝の型を
        /// 引く表である。戻り値の綴りが <see cref="object"/> の行はどの型も持ち込まない。
        /// </summary>
        public static ISet<string> Made(
            ToolMap map,
            IDictionary<string, SignatureRecord> signatures,
            IDictionary<string, IList<string>> concrete)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (signatures == null)
            {
                throw new ArgumentNullException(nameof(signatures));
            }

            if (concrete == null)
            {
                throw new ArgumentNullException(nameof(concrete));
            }

            HashSet<string> made = new HashSet<string>(StringComparer.Ordinal);
            foreach (ToolMapRow row in map.Rows)
            {
                SignatureRecord signature;
                if (!signatures.TryGetValue(row.SignatureKey, out signature)
                    || !Issues(row, signature))
                {
                    continue;
                }

                string value = TypeDefinitionName.OfElement(
                    ValueTypeName.Contained(signature.ValueType));

                if (!string.Equals(value, ObjectTypeName, StringComparison.Ordinal))
                {
                    made.Add(value);
                }
            }

            foreach (KeyValuePair<string, IList<string>> branched in concrete
                .Where(c => c.Value.Any(made.Contains)))
            {
                made.Add(branched.Key);
            }

            return made;
        }

        /// <summary>
        /// 提供対象のうち、ハンドル操作型の実体を返しうるシグネチャと、その発行の種別。公開
        /// コンストラクタは <see cref="HandleIssuanceKind.Constructor"/>、コネクタ型のメソッドは
        /// <see cref="HandleIssuanceKind.Factory"/>、ハンドル操作型のインスタンスメソッドは
        /// <see cref="HandleIssuanceKind.ReceiverBound"/> になる。種別の決まらないレシーバーの
        /// シグネチャが在れば <see cref="InvalidOperationException"/>。
        /// </summary>
        public static IDictionary<string, HandleIssuanceKind> Candidates(
            InventoryRecord inventory,
            IDictionary<string, TypeRole> roles,
            ISet<string> provided)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (roles == null)
            {
                throw new ArgumentNullException(nameof(roles));
            }

            if (provided == null)
            {
                throw new ArgumentNullException(nameof(provided));
            }

            Dictionary<string, HandleIssuanceKind> candidates =
                new Dictionary<string, HandleIssuanceKind>(StringComparer.Ordinal);
            foreach (SignatureRecord signature in inventory.Signatures
                .Where(s => provided.Contains(s.Key)))
            {
                HandleIssuanceKind kind;
                if (TryClassify(signature, roles, out kind))
                {
                    candidates.Add(signature.Key, kind);
                }
            }

            return new ReadOnlyDictionary<string, HandleIssuanceKind>(candidates);
        }

        /// <summary>
        /// その行が、1回の呼び出しで頼まれた数だけ発行できるか。コネクタから作る行と公開の
        /// コンストラクタが当たる。受け手に紐づく発行は当たらない。
        /// </summary>
        public static bool Batches(
            ToolMapRow row, SignatureRecord signature, IDictionary<string, TypeRole> roles)
        {
            if (roles == null)
            {
                throw new ArgumentNullException(nameof(roles));
            }

            if (!Issues(row, signature))
            {
                return false;
            }

            if (signature.MemberKind == MemberKind.Constructor)
            {
                return true;
            }

            TypeRole role;

            return roles.TryGetValue(
                    TypeDefinitionName.Of(signature.DeclaringType), out role)
                && role == TypeRole.Connector;
        }

        private static bool TryClassify(
            SignatureRecord signature,
            IDictionary<string, TypeRole> roles,
            out HandleIssuanceKind kind)
        {
            kind = HandleIssuanceKind.Constructor;
            string declaring = TypeDefinitionName.Of(signature.DeclaringType);
            if (signature.MemberKind == MemberKind.Constructor)
            {
                return IsHandleTarget(declaring, roles);
            }

            if (signature.MemberKind != MemberKind.Method
                || !IsHandleTarget(ElementTypeName(signature.ValueType), roles))
            {
                return false;
            }

            TypeRole role;
            if (!roles.TryGetValue(declaring, out role))
            {
                return false;
            }

            if (role == TypeRole.Connector)
            {
                kind = HandleIssuanceKind.Factory;
                return true;
            }

            if (role == TypeRole.HandleTarget && !signature.IsStatic)
            {
                kind = HandleIssuanceKind.ReceiverBound;
                return true;
            }

            throw new InvalidOperationException(
                "発行の種別を決められないレシーバーのシグネチャが在る: " + signature.Key);
        }

        private static bool IsHandleTarget(string typeName, IDictionary<string, TypeRole> roles)
        {
            TypeRole role;

            return roles.TryGetValue(typeName, out role) && role == TypeRole.HandleTarget;
        }

        /// <summary>配列の印を外した型の名前。ハンドルは配列で返ることがある。</summary>
        private static string ElementTypeName(string typeName)
        {
            return TypeDefinitionName.OfElement(typeName);
        }
    }
}
