using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 台帳が行を作らない対象の理由を決める。理由は挙げた順に評価し、最初に該当した一つを採る。
    /// 記録した理由の述語を満たすことだけを見ると、先に評価される理由にも当たる項目を見逃すので、
    /// 照合の側はここが算出した値との一致を求める。
    /// </summary>
    public sealed class OutOfScopeClassifier
    {
        private readonly Dictionary<string, TypeRecord> _types;

        private readonly Dictionary<string, List<SignatureRecord>> _declared;

        private readonly ISet<string> _population;

        private readonly Dictionary<string, bool> _routes;

        public OutOfScopeClassifier(InventoryRecord inventory, ISet<string> population)
        {
            if (inventory == null)
            {
                throw new ArgumentNullException(nameof(inventory));
            }

            if (population == null)
            {
                throw new ArgumentNullException(nameof(population));
            }

            InventoryAmbiguity.Require(inventory);

            _types = inventory.Types.ToDictionary(t => t.Name, StringComparer.Ordinal);
            _declared = inventory.Signatures
                .GroupBy(s => s.DeclaringType, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
            _population = population;
            _routes = new Dictionary<string, bool>(StringComparer.Ordinal);
            Signatures = inventory.Signatures;
        }

        private IList<SignatureRecord> Signatures { get; }

        /// <summary>
        /// 型を対象外にできる理由。どの理由にも当たらなければ null。
        /// </summary>
        public OutOfScopeReason? ClassifyType(string name)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            TypeRecord type;
            if (!_types.TryGetValue(name, out type))
            {
                throw new ArgumentException("対象アセンブリの公開型でない: " + name, nameof(name));
            }

            if (type.Kind == TypeKind.Enum)
            {
                return OutOfScopeReason.EnumType;
            }

            if (type.Kind == TypeKind.Delegate)
            {
                return OutOfScopeReason.DelegateType;
            }

            if (IsRouteType(name, new HashSet<string>(StringComparer.Ordinal)))
            {
                return OutOfScopeReason.Route;
            }

            if (IsArgumentOnly(name))
            {
                return OutOfScopeReason.ArgumentOnly;
            }

            return null;
        }

        /// <summary>
        /// シグネチャを対象外にできる理由。どの理由にも当たらなければ null。列挙型・デリゲート型・
        /// 引数専用型は型ごと対象外になるので、シグネチャ単位では経路だけを採る。
        /// </summary>
        public OutOfScopeReason? ClassifySignature(SignatureRecord signature)
        {
            if (signature == null)
            {
                throw new ArgumentNullException(nameof(signature));
            }

            return IsRouteMember(signature) ? OutOfScopeReason.Route : (OutOfScopeReason?)null;
        }

        private bool IsRouteMember(SignatureRecord signature)
        {
            if (signature.MemberKind != MemberKind.Method
                && signature.MemberKind != MemberKind.Property
                && signature.MemberKind != MemberKind.Field)
            {
                return false;
            }

            return signature.Parameters.Count == 0
                && signature.ValueType != null
                && _types.ContainsKey(signature.ValueType)
                && !string.Equals(signature.ValueType, signature.DeclaringType, StringComparison.Ordinal);
        }

        private bool IsRouteType(string name, ISet<string> visiting)
        {
            bool route;
            if (_routes.TryGetValue(name, out route))
            {
                return route;
            }

            if (!visiting.Add(name))
            {
                return false;
            }

            route = Evaluate(name, visiting);
            visiting.Remove(name);
            _routes[name] = route;
            return route;
        }

        private bool Evaluate(string name, ISet<string> visiting)
        {
            List<SignatureRecord> declared;
            if (_declared.TryGetValue(name, out declared) && declared.Count > 0)
            {
                return declared.All(IsRouteMember);
            }

            TypeRecord type;
            if (!_types.TryGetValue(name, out type) || type.BaseTypes.Count == 0)
            {
                return false;
            }

            return type.BaseTypes.All(b => _types.ContainsKey(b) && IsRouteType(b, visiting));
        }

        private bool IsArgumentOnly(string name)
        {
            List<SignatureRecord> declared;
            if (_declared.TryGetValue(name, out declared) && declared.Any(s => _population.Contains(s.Key)))
            {
                return false;
            }

            bool used = false;
            foreach (SignatureRecord signature in Signatures)
            {
                if (string.Equals(signature.DeclaringType, name, StringComparison.Ordinal))
                {
                    continue;
                }

                if (signature.ValueType != null
                    && string.Equals(signature.ValueType.TrimEnd('&'), name, StringComparison.Ordinal))
                {
                    return false;
                }

                if (signature.Parameters.Any(p => string.Equals(
                        p.TypeName.TrimEnd('&'), name, StringComparison.Ordinal)))
                {
                    used = true;
                }
            }

            return used;
        }
    }
}
