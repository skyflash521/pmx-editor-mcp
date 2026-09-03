using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 型ごとの役割が、規則どおりに割り当てられているかを検査する。役割の意味そのものは測れないので、
    /// 機械で確かめられる範囲——型の過不足、接続の根、イベント引数型の必要十分、コネクタ型を
    /// 呼び出し側が実体を用意せずに呼べること、接続の経路、ハンドルを返しうるシグネチャの
    /// 過不足と発行の種別、要素を並べるリストの過不足と所有の一意、担当群と台帳の担当の一致
    /// ——に限る。
    /// </summary>
    public static class TypeRoleGate
    {
        /// <summary>
        /// 規則に反していれば <see cref="InvalidOperationException"/>。<paramref name="roleTypes"/> は
        /// 表が覆うべき型の集合で、接続の根とその経路上の型を含めて渡すこと。
        /// <paramref name="connectorCandidates"/> には
        /// <see cref="TypeRoleEvidence.ConnectorCandidates"/> の結果を渡すこと——コネクタ型に
        /// なりうるかは、この集合に在るかどうかで見る。<paramref name="connectionPaths"/> には
        /// <see cref="TypeRoleEvidence.ReachableFromRoots"/> の結果を、
        /// <paramref name="issuanceCandidates"/> には
        /// <see cref="HandleIssuanceEvidence.Candidates"/> の結果を、
        /// <paramref name="collectionCandidates"/> には
        /// <see cref="ElementCollectionEvidence.Candidates"/> の結果を、
        /// <paramref name="ledgerOwners"/> には
        /// <see cref="TypeGroupEvidence.OwnersByType"/> の結果を渡すこと。
        /// </summary>
        public static void Require(
            TypeRoleTable table,
            ISet<string> roleTypes,
            IEnumerable<string> connectionRoots,
            ISet<string> eventArgumentTypes,
            ICollection<string> connectorCandidates,
            IDictionary<string, string> connectionPaths,
            IDictionary<string, HandleIssuanceKind> issuanceCandidates,
            IDictionary<string, string> collectionCandidates,
            IDictionary<string, ISet<CapabilityOwner>> ledgerOwners)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            if (roleTypes == null)
            {
                throw new ArgumentNullException(nameof(roleTypes));
            }

            if (connectionRoots == null)
            {
                throw new ArgumentNullException(nameof(connectionRoots));
            }

            if (eventArgumentTypes == null)
            {
                throw new ArgumentNullException(nameof(eventArgumentTypes));
            }

            if (connectorCandidates == null)
            {
                throw new ArgumentNullException(nameof(connectorCandidates));
            }

            if (connectionPaths == null)
            {
                throw new ArgumentNullException(nameof(connectionPaths));
            }

            if (issuanceCandidates == null)
            {
                throw new ArgumentNullException(nameof(issuanceCandidates));
            }

            if (collectionCandidates == null)
            {
                throw new ArgumentNullException(nameof(collectionCandidates));
            }

            if (ledgerOwners == null)
            {
                throw new ArgumentNullException(nameof(ledgerOwners));
            }

            IList<TypeRoleRecord> records = table.Types;
            RequireSameTypes(records, roleTypes);
            RequireRootsAreConnectors(records, connectionRoots);
            RequireEventArgumentsMatchTheEvidence(records, eventArgumentTypes);
            RequireConnectorsNeedNoInstanceFromTheCaller(records, connectorCandidates);
            RequireConnectionPathsMatchTheEvidence(records, connectionPaths);
            RequireIssuancesMatchTheEvidence(table.Issuances, issuanceCandidates);
            RequireCollectionsMatchTheEvidence(table.Collections, collectionCandidates);
            RequireGroupsMatchTheLedger(records, ledgerOwners);
        }

        /// <summary>
        /// 表の型と役割対象を一対一で突き合わせる。これが無いと、型を丸ごと書き落としても、対象外の型を
        /// 混ぜても、残った項目だけが条件を満たして通る。
        /// </summary>
        private static void RequireSameTypes(IList<TypeRoleRecord> records, ISet<string> roleTypes)
        {
            HashSet<string> listed = new HashSet<string>(StringComparer.Ordinal);
            foreach (TypeRoleRecord record in records)
            {
                if (!listed.Add(record.TypeName))
                {
                    throw new InvalidOperationException("表に同じ型が二度在る: " + record.TypeName);
                }
            }

            string missing = roleTypes.Except(listed, StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal).FirstOrDefault();
            if (missing != null)
            {
                throw new InvalidOperationException("表に無い型が在る: " + missing);
            }

            string extra = listed.Except(roleTypes, StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal).FirstOrDefault();
            if (extra != null)
            {
                throw new InvalidOperationException("役割対象に無い型が在る: " + extra);
            }
        }

        /// <summary>
        /// 接続の根は、接続初期化が最初に触る接続点そのものなので、表がコネクタ型として持つことを
        /// 課す。
        /// </summary>
        private static void RequireRootsAreConnectors(
            IList<TypeRoleRecord> records, IEnumerable<string> connectionRoots)
        {
            IDictionary<string, TypeRole> roles = records
                .ToDictionary(r => r.TypeName, r => r.Role, StringComparer.Ordinal);
            foreach (string root in connectionRoots)
            {
                TypeRole role;
                if (!roles.TryGetValue(root, out role))
                {
                    throw new InvalidOperationException("接続の根が表に無い: " + root);
                }

                if (role != TypeRole.Connector)
                {
                    throw new InvalidOperationException("接続の根をコネクタ型にしていない: " + root);
                }
            }
        }

        /// <summary>
        /// イベント引数型は列挙から必要十分に決まるので、両向きで突き合わせる。
        /// </summary>
        private static void RequireEventArgumentsMatchTheEvidence(
            IList<TypeRoleRecord> records, ISet<string> eventArgumentTypes)
        {
            foreach (TypeRoleRecord record in records)
            {
                bool isEvidence = eventArgumentTypes.Contains(record.TypeName);
                bool isDeclared = record.Role == TypeRole.EventArgs;
                if (isEvidence && !isDeclared)
                {
                    throw new InvalidOperationException(
                        "イベントのハンドラ引数なのにイベント引数型にしていない: " + record.TypeName);
                }

                if (isDeclared && !isEvidence)
                {
                    throw new InvalidOperationException(
                        "イベントのハンドラ引数に現れない型をイベント引数型にしている: " + record.TypeName);
                }
            }
        }

        /// <summary>
        /// コネクタ型は、呼び出し側が実体を用意せずに呼べる型でなければならない。この集合に在ること
        /// はその必要条件で、在るからといってコネクタ型とは限らない。
        /// </summary>
        private static void RequireConnectorsNeedNoInstanceFromTheCaller(
            IList<TypeRoleRecord> records, ICollection<string> connectorCandidates)
        {
            foreach (TypeRoleRecord record in records
                .Where(r => r.Role == TypeRole.Connector
                    && !connectorCandidates.Contains(r.TypeName)))
            {
                throw new InvalidOperationException(
                    "呼び出し側が実体を用意しなければ呼べない型をコネクタ型にしている: "
                        + record.TypeName);
            }
        }

        /// <summary>
        /// コネクタ型の接続の経路は、列挙から辿った経路と一致しなければならない。列挙が経路を持たない
        /// 型は、表も持たないことを求める。
        /// </summary>
        private static void RequireConnectionPathsMatchTheEvidence(
            IList<TypeRoleRecord> records, IDictionary<string, string> connectionPaths)
        {
            foreach (TypeRoleRecord record in records.Where(r => r.Role == TypeRole.Connector))
            {
                string reached;
                string expected = connectionPaths.TryGetValue(record.TypeName, out reached)
                    ? reached
                    : string.Empty;
                if (!string.Equals(record.ConnectionPath, expected, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "接続の経路が列挙と合わない: " + record.TypeName
                            + "(表: " + Shown(record.ConnectionPath)
                            + " / 列挙: " + Shown(expected) + ")");
                }
            }
        }

        /// <summary>
        /// 担当群が、台帳がその型へ与える担当と食い違わないことを求める。担当が一つに決まる型では
        /// 表の値が書き手の判断でなく台帳の写しになるので、そこだけを突き合わせる。
        /// </summary>
        private static void RequireGroupsMatchTheLedger(
            IList<TypeRoleRecord> records, IDictionary<string, ISet<CapabilityOwner>> ledgerOwners)
        {
            foreach (TypeRoleRecord record in records.Where(
                r => TypeRoleRecord.HasIndependentTool(r.Role)))
            {
                ISet<CapabilityOwner> owners;
                if (!ledgerOwners.TryGetValue(record.TypeName, out owners))
                {
                    continue;
                }

                if (owners.Count != 1)
                {
                    continue;
                }

                CapabilityOwner only = owners.First();
                if (record.Group != only)
                {
                    throw new InvalidOperationException(
                        "担当群が台帳の担当と合わない: " + record.TypeName
                            + "(表: " + record.Group + " / 台帳: " + only + ")");
                }
            }
        }

        private static string Shown(string path)
        {
            return path.Length == 0 ? "無し" : path;
        }

        /// <summary>
        /// ハンドルを返しうるシグネチャの集合が列挙と一対一で、発行するとしたものの種別が
        /// レシーバーから導いた種別と一致することを求める。
        /// </summary>
        private static void RequireIssuancesMatchTheEvidence(
            IList<HandleIssuanceRecord> records,
            IDictionary<string, HandleIssuanceKind> candidates)
        {
            HashSet<string> listed = new HashSet<string>(StringComparer.Ordinal);
            foreach (HandleIssuanceRecord record in records)
            {
                if (!listed.Add(record.SignatureKey))
                {
                    throw new InvalidOperationException(
                        "表に同じ行キーが二度在る: " + record.SignatureKey);
                }

                HandleIssuanceKind derived;
                if (!candidates.TryGetValue(record.SignatureKey, out derived))
                {
                    throw new InvalidOperationException(
                        "ハンドルを返さないシグネチャが表に在る: " + record.SignatureKey);
                }

                if (record.Issues && record.Kind != derived)
                {
                    throw new InvalidOperationException(
                        "発行の種別がレシーバーと合わない: " + record.SignatureKey
                            + "(表: " + record.Kind + " / 列挙: " + derived + ")");
                }
            }

            string missing = candidates.Keys.Except(listed, StringComparer.Ordinal)
                .OrderBy(k => k, StringComparer.Ordinal).FirstOrDefault();
            if (missing != null)
            {
                throw new InvalidOperationException("表に無いシグネチャが在る: " + missing);
            }
        }

        /// <summary>
        /// 要素を並べるリストの集合が列挙と一対一で、要素の型ごとに所有するリストが1つを超えず、
        /// 指すだけのリストの要素に所有するリストが在ることを求める。
        /// </summary>
        private static void RequireCollectionsMatchTheEvidence(
            IList<ElementCollectionRecord> records, IDictionary<string, string> candidates)
        {
            HashSet<string> listed = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, string> ownerOf = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (ElementCollectionRecord record in records)
            {
                if (!listed.Add(record.SignatureKey))
                {
                    throw new InvalidOperationException(
                        "表に同じ行キーが二度在る: " + record.SignatureKey);
                }

                string element;
                if (!candidates.TryGetValue(record.SignatureKey, out element))
                {
                    throw new InvalidOperationException(
                        "要素を並べるリストでないシグネチャが表に在る: " + record.SignatureKey);
                }

                if (!record.Owns)
                {
                    continue;
                }

                string other;
                if (ownerOf.TryGetValue(element, out other))
                {
                    throw new InvalidOperationException(
                        "同じ要素の型を所有するリストが二つ在る: " + other + " と " + record.SignatureKey
                            + "(" + element + ")");
                }

                ownerOf.Add(element, record.SignatureKey);
            }

            string missing = candidates.Keys.Except(listed, StringComparer.Ordinal)
                .OrderBy(k => k, StringComparer.Ordinal).FirstOrDefault();
            if (missing != null)
            {
                throw new InvalidOperationException("表に無いリストが在る: " + missing);
            }

            foreach (ElementCollectionRecord record in records
                .Where(r => !r.Owns && !ownerOf.ContainsKey(candidates[r.SignatureKey])))
            {
                throw new InvalidOperationException(
                    "所有するリストの無い要素を指すリストが在る: " + record.SignatureKey
                        + "(" + candidates[record.SignatureKey] + ")");
            }
        }
    }
}
