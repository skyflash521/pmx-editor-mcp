using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 型役割表が受け持つ型の母集合を機械導出する。提供対象のシグネチャに現れる型を推移的に
    /// 辿り、値の表現で写せる型を除いた残りが型役割表の母集合になる。
    ///
    /// どちらの集合にも入らないのは、総称型引数と、イベントのハンドラ型として現れるデリゲート型で
    /// ある。前者は宣言ごとに別の型で役割を持たず、後者は購読の仕組みが受け持つ。
    /// </summary>
    public sealed class TypeRolePopulation
    {
        private static readonly HashSet<string> ValueContainers = new HashSet<string>(
            new[] { "System.Nullable<1>", "System.Collections.Generic.IList<1>" }, StringComparer.Ordinal);

        private TypeRolePopulation(
            ISet<string> signatures, ISet<string> valueMapped, ISet<string> roleTypes)
        {
            Signatures = signatures;
            ValueMapped = valueMapped;
            RoleTypes = roleTypes;
        }

        /// <summary>提供対象のシグネチャの行キー。</summary>
        public ISet<string> Signatures { get; }

        /// <summary>値の表現で写せる型の名前。包みを外した形で持つ。</summary>
        public ISet<string> ValueMapped { get; }

        /// <summary>型役割表が受け持つ型の名前。包みを外した形で持つ。</summary>
        public ISet<string> RoleTypes { get; }

        /// <summary>
        /// 提供対象は、台帳の提供行が担当するシグネチャから除外一覧のものを除いた集合とする。
        /// </summary>
        public static TypeRolePopulation Resolve(
            IList<CapabilityRecord> ledger,
            InventoryRecord inventory,
            IList<ExcludedSignatureRecord> excluded)
        {
            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (excluded == null)
            {
                throw new ArgumentNullException(nameof(excluded));
            }

            InventoryAmbiguity.Require(inventory);
            LedgerPopulation population = LedgerPopulation.Resolve(ledger, inventory);

            return new Walk(inventory, Provided(ledger, population, excluded)).Run();
        }

        private static HashSet<string> Provided(
            IList<CapabilityRecord> ledger,
            LedgerPopulation population,
            IList<ExcludedSignatureRecord> excluded)
        {
            HashSet<string> providing = new HashSet<string>(
                ledger.Where(c => c.Status == CapabilityStatus.Provided).Select(c => c.Id),
                StringComparer.Ordinal);
            HashSet<string> removed = new HashSet<string>(
                excluded.Select(e => e.Key), StringComparer.Ordinal);

            return new HashSet<string>(
                population.Owners
                    .Where(o => !removed.Contains(o.Key) && o.Value.Any(providing.Contains))
                    .Select(o => o.Key),
                StringComparer.Ordinal);
        }

        private static string WithoutReferenceAndArrayMarks(string typeName)
        {
            string name = typeName.EndsWith("&", StringComparison.Ordinal)
                ? typeName.Substring(0, typeName.Length - 1)
                : typeName;

            while (name.EndsWith("]", StringComparison.Ordinal))
            {
                int open = name.LastIndexOf('[');
                if (open < 0)
                {
                    break;
                }

                name = name.Substring(0, open);
            }

            return name;
        }

        /// <summary>1つの列挙と提供対象の組について、母集合を組み立てる。</summary>
        private sealed class Walk
        {
            private readonly InventoryRecord inventory;

            private readonly HashSet<string> provided;

            private readonly Dictionary<string, TypeRecord> declaredTypes;

            private readonly Dictionary<string, IList<SignatureRecord>> declaredMembers;

            private readonly Dictionary<string, ISet<string>> declaredParameters;

            private readonly HashSet<string> delegates;

            private readonly ValueRepresentationRule rule;

            private readonly HashSet<string> direct = new HashSet<string>(StringComparer.Ordinal);

            private readonly HashSet<string> valueMapped = new HashSet<string>(StringComparer.Ordinal);

            private readonly HashSet<string> roleTypes = new HashSet<string>(StringComparer.Ordinal);

            private readonly HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

            public Walk(InventoryRecord inventory, HashSet<string> provided)
            {
                this.inventory = inventory;
                this.provided = provided;
                rule = ValueRepresentationRule.Create(inventory);
                declaredTypes = inventory.Types.ToDictionary(t => TypeDefinitionName.Of(t.Name), StringComparer.Ordinal);
                declaredMembers = inventory.Signatures
                    .GroupBy(s => TypeDefinitionName.Of(s.DeclaringType), StringComparer.Ordinal)
                    .ToDictionary(
                        g => g.Key, g => (IList<SignatureRecord>)g.ToList(), StringComparer.Ordinal);
                declaredParameters = inventory.Types
                    .Where(t => t.IsGenericTypeDefinition)
                    .ToDictionary(
                        t => TypeDefinitionName.Of(t.Name),
                        t => (ISet<string>)new HashSet<string>(
                            TypeDefinitionName.Arguments(t.Name), StringComparer.Ordinal),
                        StringComparer.Ordinal);
                delegates = new HashSet<string>(
                    inventory.Types.Concat(inventory.ReferencedTypes)
                        .Where(t => t.Kind == TypeKind.Delegate)
                        .Select(t => TypeDefinitionName.Of(t.Name)),
                    StringComparer.Ordinal);
            }

            /// <summary>到達順に結果が左右されないようにする。</summary>
            public TypeRolePopulation Run()
            {
                Dictionary<string, SignatureRecord> byKey =
                    inventory.Signatures.ToDictionary(s => s.Key, StringComparer.Ordinal);
                foreach (string key in provided)
                {
                    Collect(byKey[key], direct);
                }

                Queue<string> pending = new Queue<string>(direct);
                while (pending.Count != 0)
                {
                    Visit(pending.Dequeue(), pending);
                }

                return new TypeRolePopulation(provided, valueMapped, roleTypes);
            }

            private void Visit(string typeName, Queue<string> pending)
            {
                if (!seen.Add(typeName))
                {
                    return;
                }

                ValueRepresentation representation;
                if (ValueContainers.Contains(typeName) || rule.TryClassify(typeName, out representation))
                {
                    valueMapped.Add(typeName);
                    return;
                }

                if (delegates.Contains(typeName))
                {
                    return;
                }

                TypeRecord type;
                if (!declaredTypes.TryGetValue(typeName, out type))
                {
                    if (direct.Contains(typeName))
                    {
                        roleTypes.Add(typeName);
                    }

                    return;
                }

                roleTypes.Add(typeName);

                HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
                ISet<string> parameters = Parameters(typeName);
                foreach (string baseType in type.BaseTypes)
                {
                    Collect(baseType, names, parameters);
                }

                IList<SignatureRecord> members;
                if (declaredMembers.TryGetValue(typeName, out members))
                {
                    foreach (SignatureRecord member in members.Where(m => provided.Contains(m.Key)))
                    {
                        Collect(member, names);
                    }
                }

                foreach (string name in names)
                {
                    pending.Enqueue(name);
                }
            }

            private void Collect(SignatureRecord signature, ISet<string> names)
            {
                HashSet<string> parameters = new HashSet<string>(
                    Parameters(TypeDefinitionName.Of(signature.DeclaringType)), StringComparer.Ordinal);
                parameters.UnionWith(signature.TypeParameters);

                Collect(signature.DeclaringType, names, parameters);

                foreach (ParameterRecord parameter in signature.Parameters.Where(p => !p.IsTypeArgument))
                {
                    Collect(parameter.TypeName, names, parameters);
                }

                if (!signature.ValueTypeIsTypeArgument)
                {
                    Collect(signature.ValueType, names, parameters);
                }
            }

            /// <summary>1つの型名を構成する型の名前を集める。</summary>
            private void Collect(string typeName, ISet<string> names, ISet<string> parameters)
            {
                string name = WithoutReferenceAndArrayMarks(typeName);
                if (!parameters.Contains(name))
                {
                    names.Add(TypeDefinitionName.Of(name));
                }

                foreach (string argument in TypeDefinitionName.Arguments(name))
                {
                    Collect(argument, names, parameters);
                }
            }

            private ISet<string> Parameters(string definition)
            {
                ISet<string> parameters;

                return declaredParameters.TryGetValue(definition, out parameters)
                    ? parameters
                    : new HashSet<string>(StringComparer.Ordinal);
            }
        }
    }
}
