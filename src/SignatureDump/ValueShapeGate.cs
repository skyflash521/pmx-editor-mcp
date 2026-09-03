using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 値の表現の表を、提供対象から導いた、値として写せる型の集合と規則へ照合する。表に載らない型が残ると、
    /// その型の写し方は誰も決めていないことになる。
    /// </summary>
    public static class ValueShapeGate
    {
        /// <summary>合わなければ <see cref="InvalidOperationException"/> を投げる。</summary>
        public static void Require(
            IList<ValueShapeRow> rows, ISet<string> valueMapped, ValueRepresentationRule rule)
        {
            if (rows == null)
            {
                throw new ArgumentNullException(nameof(rows));
            }

            if (valueMapped == null)
            {
                throw new ArgumentNullException(nameof(valueMapped));
            }

            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            RequireEachTypeOnce(rows);
            RequireTheSameTypes(rows, valueMapped);
            RequireTheShapesFollowTheRule(rows, rule);
        }

        private static void RequireEachTypeOnce(IList<ValueShapeRow> rows)
        {
            string repeated = rows.GroupBy(r => r.TypeName, StringComparer.Ordinal)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .OrderBy(n => n, StringComparer.Ordinal)
                .FirstOrDefault();
            if (repeated != null)
            {
                throw new InvalidOperationException("同じ型が二度現れる: " + repeated);
            }
        }

        private static void RequireTheSameTypes(IList<ValueShapeRow> rows, ISet<string> valueMapped)
        {
            HashSet<string> listed = new HashSet<string>(
                rows.Select(r => r.TypeName), StringComparer.Ordinal);

            string missing = valueMapped.Where(t => !listed.Contains(t))
                .OrderBy(n => n, StringComparer.Ordinal).FirstOrDefault();
            if (missing != null)
            {
                throw new InvalidOperationException("表に無い型が在る: " + missing);
            }

            string extra = listed.Where(t => !valueMapped.Contains(t))
                .OrderBy(n => n, StringComparer.Ordinal).FirstOrDefault();
            if (extra != null)
            {
                throw new InvalidOperationException("値として写せない型が表に在る: " + extra);
            }
        }

        private static void RequireTheShapesFollowTheRule(
            IList<ValueShapeRow> rows, ValueRepresentationRule rule)
        {
            foreach (ValueShapeRow row in rows)
            {
                ValueRepresentation representation;
                if (rule.TryClassify(row.TypeName, out representation))
                {
                    if (!string.Equals(row.Shape, representation.Identifier, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "表現が規則と違う: " + row.TypeName + " は " + representation.Identifier);
                    }

                    continue;
                }

                if (row.Shape != null)
                {
                    throw new InvalidOperationException(
                        "綴りが1つに決まらない型に綴りが書いてある: " + row.TypeName);
                }
            }
        }
    }
}
