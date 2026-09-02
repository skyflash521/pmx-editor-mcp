using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 型ごとの役割が、規則どおりに割り当てられているかを検査する。役割の意味そのものは測れないので、
    /// 機械で確かめられる範囲——型の過不足、接続の根、イベント引数型の必要十分、コネクタ型の到達性
    /// ——に限る。
    /// </summary>
    public static class TypeRoleGate
    {
        /// <summary>
        /// 規則に反していれば <see cref="InvalidOperationException"/>。<paramref name="roleTypes"/> は
        /// 表が覆うべき型の集合で、接続の根とその経路上の型を含めて渡すこと。
        /// </summary>
        public static void Require(
            TypeRoleTable table,
            ISet<string> roleTypes,
            ISet<string> eventArgumentTypes,
            ICollection<string> reachableTypes)
        {
            if (table == null)
            {
                throw new ArgumentNullException(nameof(table));
            }

            if (roleTypes == null)
            {
                throw new ArgumentNullException(nameof(roleTypes));
            }

            if (eventArgumentTypes == null)
            {
                throw new ArgumentNullException(nameof(eventArgumentTypes));
            }

            if (reachableTypes == null)
            {
                throw new ArgumentNullException(nameof(reachableTypes));
            }

            RequireSameTypes(table.Types, roleTypes);
            RequireRootsAreConnectors(table);
            RequireEventArgumentsMatchTheEvidence(table.Types, eventArgumentTypes);
            RequireConnectorsAreReachable(table.Types, reachableTypes);
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
        /// 接続の根は、ホストが常駐保持するものそのものなので、表がコネクタ型として持つことを課す。
        /// </summary>
        private static void RequireRootsAreConnectors(TypeRoleTable table)
        {
            IDictionary<string, TypeRole> roles = table.Types
                .ToDictionary(r => r.TypeName, r => r.Role, StringComparer.Ordinal);
            foreach (string root in table.ConnectionRoots)
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

        private static void RequireConnectorsAreReachable(
            IList<TypeRoleRecord> records, ICollection<string> reachableTypes)
        {
            foreach (TypeRoleRecord record in records
                .Where(r => r.Role == TypeRole.Connector && !reachableTypes.Contains(r.TypeName)))
            {
                throw new InvalidOperationException(
                    "接続の根から辿り着けない型をコネクタ型にしている: " + record.TypeName);
            }
        }
    }
}
