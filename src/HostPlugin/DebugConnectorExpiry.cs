using System;
using System.Collections.Generic;

namespace PmxEditorMcp
{
    /// <summary>
    /// 検査が常駐コネクタを失効させるための入口。失効は待てば起こるとは限らず、起こる時機も
    /// こちらで決められないので、取り直しの経路は人為的に失効させないと通らない。
    /// </summary>
    public static class DebugConnectorExpiry
    {
        /// <summary>この入口のメソッド名。MCPのツールとしては公開しない。</summary>
        public const string MethodName = "debug_expire_connector";

        /// <summary>
        /// 入口が開いているときだけ表へ足す。閉じているときは足さないので、要求は未知のメソッド
        /// として返る。
        /// </summary>
        public static void AddTo(McpMethodTable methods, bool enabled, ResidentConnection resident)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (resident == null)
            {
                throw new ArgumentNullException(nameof(resident));
            }

            if (enabled)
            {
                methods.Add(MethodName, context => Expire(resident));
            }
        }

        /// <summary>
        /// 保持しているコネクタを失効させ、その場で求め直す。求め直しは常駐の側が持つ経路を
        /// そのまま通すので、記録には失効と取得が順に並ぶ。
        /// </summary>
        public static object Expire(ResidentConnection resident)
        {
            if (resident == null)
            {
                throw new ArgumentNullException(nameof(resident));
            }

            resident.Expire();
            resident.Use();

            return new Dictionary<string, object>(StringComparer.Ordinal) { { "renewed", true } };
        }
    }
}
