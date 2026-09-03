using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 危険操作に当たるシグネチャについて、決め方が導くものと台帳が記すものを突き合わせる。片方に
    /// しか無いものが残ると、危険操作かどうかが2つの資料で食い違う。
    /// </summary>
    public static class DangerousOperationGate
    {
        /// <summary>合わなければ <see cref="InvalidOperationException"/> を投げる。</summary>
        public static void Require(
            IDictionary<string, DangerKind> derived, IDictionary<string, DangerKind> noted)
        {
            if (derived == null)
            {
                throw new ArgumentNullException(nameof(derived));
            }

            if (noted == null)
            {
                throw new ArgumentNullException(nameof(noted));
            }

            string missing = derived.Keys.Where(k => !noted.ContainsKey(k))
                .OrderBy(k => k, StringComparer.Ordinal).FirstOrDefault();
            if (missing != null)
            {
                throw new InvalidOperationException("台帳が危険操作として記していない: " + missing);
            }

            string extra = noted.Keys.Where(k => !derived.ContainsKey(k))
                .OrderBy(k => k, StringComparer.Ordinal).FirstOrDefault();
            if (extra != null)
            {
                throw new InvalidOperationException(
                    "決め方が危険操作としないものを台帳が記している: " + extra);
            }

            string disagreed = derived.Keys.Where(k => derived[k] != noted[k])
                .OrderBy(k => k, StringComparer.Ordinal).FirstOrDefault();
            if (disagreed != null)
            {
                throw new InvalidOperationException(string.Format(
                    CultureInfo.InvariantCulture,
                    "種別が食い違う: {0} は {1} だが台帳は {2}",
                    disagreed,
                    derived[disagreed],
                    noted[disagreed]));
            }
        }
    }
}
