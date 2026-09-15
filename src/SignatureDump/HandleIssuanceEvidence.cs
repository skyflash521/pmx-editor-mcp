using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// ハンドルを返しうるシグネチャと、その発行の種別を列挙から導く。新しいハンドルを発行するのか
    /// 既にあるものを返すだけなのかは列挙からは決まらないので、ここでは決めない。
    /// </summary>
    public static class HandleIssuanceEvidence
    {
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
        /// コンストラクタが当たる。受け手に紐づく発行は当たらない——受け手1件につき1個を発行する
        /// ので、発行する数は受け手の件数が決める。
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
