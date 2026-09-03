using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 共通契約割当の機械的な根拠。シグネチャの引数・戻り値・レシーバーがどのスロットへ束縛されるかを
    /// 型と方向から決め、常駐アクセスオブジェクトを返すシグネチャを列挙から拾う。
    /// </summary>
    public static class CommonAssignmentEvidence
    {
        /// <summary>現在のPMXを表す型。</summary>
        public const string CurrentPmxType = "PEPlugin.Pmx.IPXPmx";

        /// <summary>反映する対象の種別を表す型。</summary>
        public const string UpdateKindType = "PEPlugin.Pmx.PmxUpdateObject";

        private const string IndexType = "System.Int32";

        private const string TextType = "System.String";

        private const string FlagType = "System.Boolean";

        /// <summary>提供対象のうち、解放・破棄のシグネチャ。</summary>
        public static ISet<string> ReleaseSignatures(InventoryRecord inventory, ISet<string> provided)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (provided == null)
            {
                throw new ArgumentNullException(nameof(provided));
            }

            return new HashSet<string>(
                inventory.Signatures
                    .Where(s => provided.Contains(s.Key) && IsRelease(s))
                    .Select(s => s.Key),
                StringComparer.Ordinal);
        }

        /// <summary>
        /// 提供対象のうち、値の型がコネクタ型のシグネチャ。常駐アクセスオブジェクトの取得に当たる。
        /// </summary>
        public static ISet<string> ResidentObjectSignatures(
            InventoryRecord inventory, IDictionary<string, TypeRole> roles, ISet<string> provided)
        {
            RequireArguments(inventory, roles);
            if (provided == null)
            {
                throw new ArgumentNullException(nameof(provided));
            }

            return new HashSet<string>(
                inventory.Signatures
                    .Where(s => provided.Contains(s.Key) && IsConnector(s.ValueType, roles))
                    .Select(s => s.Key),
                StringComparer.Ordinal);
        }

        /// <summary>
        /// 提供対象のシグネチャの行キーから、そのシグネチャの束縛。どのスロットとも決まらない引数・
        /// 戻り値が在れば <see cref="InvalidOperationException"/>——決め方がその形を覆えていない。
        /// </summary>
        public static IDictionary<string, SlotBinding> Bindings(
            InventoryRecord inventory, IDictionary<string, TypeRole> roles, ISet<string> keys)
        {
            RequireArguments(inventory, roles);
            if (keys == null)
            {
                throw new ArgumentNullException(nameof(keys));
            }

            Dictionary<string, SlotBinding> bindings =
                new Dictionary<string, SlotBinding>(StringComparer.Ordinal);
            foreach (SignatureRecord signature in inventory.Signatures
                .Where(s => keys.Contains(s.Key)))
            {
                bindings.Add(signature.Key, Binding(signature, roles));
            }

            return new ReadOnlyDictionary<string, SlotBinding>(bindings);
        }

        private static SlotBinding Binding(
            SignatureRecord signature, IDictionary<string, TypeRole> roles)
        {
            Dictionary<string, BindingSlot> parameters =
                new Dictionary<string, BindingSlot>(StringComparer.Ordinal);
            foreach (ParameterRecord parameter in signature.Parameters)
            {
                parameters.Add(parameter.Name, ParameterSlot(signature, parameter, roles));
            }

            return new SlotBinding(ReturnSlot(signature, roles), ReceiverSlot(signature), parameters);
        }

        private static BindingSlot? ReturnSlot(
            SignatureRecord signature, IDictionary<string, TypeRole> roles)
        {
            string name = Bare(signature.ValueType);
            if (string.Equals(name, "System.Void", StringComparison.Ordinal))
            {
                return null;
            }

            if (TypeRoleEvidence.ConnectionRoots.Contains(name))
            {
                return BindingSlot.RunArgsClone;
            }

            if (IsConnector(signature.ValueType, roles))
            {
                return BindingSlot.ResidentObject;
            }

            if (string.Equals(name, CurrentPmxType, StringComparison.Ordinal))
            {
                return BindingSlot.PmxClone;
            }

            throw Undecided(signature, "戻り値の型: " + signature.ValueType);
        }

        private static BindingSlot ParameterSlot(
            SignatureRecord signature, ParameterRecord parameter, IDictionary<string, TypeRole> roles)
        {
            if (parameter.Direction != ParameterDirection.In)
            {
                throw Undecided(signature, "入力でない引数: " + parameter.Name);
            }

            string name = Bare(parameter.TypeName);
            if (string.Equals(name, TypeRoleEvidence.InjectedConnector, StringComparison.Ordinal))
            {
                return BindingSlot.InjectedConnector;
            }

            if (string.Equals(name, CurrentPmxType, StringComparison.Ordinal))
            {
                return BindingSlot.PmxClone;
            }

            if (string.Equals(name, UpdateKindType, StringComparison.Ordinal))
            {
                return BindingSlot.UpdateKind;
            }

            if (string.Equals(name, IndexType, StringComparison.Ordinal))
            {
                return BindingSlot.UpdateIndices;
            }

            if (string.Equals(name, FlagType, StringComparison.Ordinal))
            {
                return BindingSlot.UndoLock;
            }

            if (string.Equals(name, TextType, StringComparison.Ordinal)
                && TypeRoleEvidence.ConnectionRoots.Contains(Bare(signature.ValueType)))
            {
                return BindingSlot.ModulePath;
            }

            TypeRole role;
            if (roles.TryGetValue(name, out role) && role == TypeRole.HandleTarget)
            {
                return BindingSlot.TargetHandle;
            }

            throw Undecided(signature, "引数の型: " + parameter.TypeName);
        }

        private static BindingSlot? ReceiverSlot(SignatureRecord signature)
        {
            if (signature.IsStatic)
            {
                return null;
            }

            return IsRelease(signature) && signature.Parameters.Count == 0
                ? BindingSlot.TargetHandle
                : BindingSlot.OwningObject;
        }

        /// <summary>解放・破棄のメンバーか。名前で見分ける。</summary>
        private static bool IsRelease(SignatureRecord signature)
        {
            return string.Equals(signature.MemberName, "Dispose", StringComparison.Ordinal)
                || signature.MemberName.StartsWith("Release", StringComparison.Ordinal);
        }

        private static bool IsConnector(string typeName, IDictionary<string, TypeRole> roles)
        {
            TypeRole role;

            return roles.TryGetValue(Bare(typeName), out role) && role == TypeRole.Connector;
        }

        /// <summary>参照と配列の印を外し、総称型を型引数の数の形へそろえた型の名前。</summary>
        private static string Bare(string typeName)
        {
            string name = typeName ?? string.Empty;
            if (name.EndsWith("&", StringComparison.Ordinal))
            {
                name = name.Substring(0, name.Length - 1);
            }

            while (name.EndsWith("[]", StringComparison.Ordinal))
            {
                name = name.Substring(0, name.Length - 2);
            }

            return TypeDefinitionName.Of(name);
        }

        private static InvalidOperationException Undecided(SignatureRecord signature, string what)
        {
            return new InvalidOperationException(
                "束縛先のスロットを決められない: " + signature.Key + "(" + what + ")");
        }

        private static void RequireArguments(
            InventoryRecord inventory, IDictionary<string, TypeRole> roles)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (roles == null)
            {
                throw new ArgumentNullException(nameof(roles));
            }
        }
    }
}
