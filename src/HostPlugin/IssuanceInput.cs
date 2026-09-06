using System;
using System.Collections.Generic;
using System.Globalization;

namespace PmxEditorMcp
{
    /// <summary>ハンドルを発行する種別。型役割表のハンドル発行の判定が持つ。</summary>
    public enum IssuanceKind
    {
        /// <summary>公開のコンストラクタ。</summary>
        Constructor = 0,

        /// <summary>コネクタのメソッド。</summary>
        Factory = 1,

        /// <summary>受け手に紐づくメソッド。</summary>
        ReceiverBound = 2,
    }

    /// <summary>解いた発行の要求。</summary>
    public sealed class ResolvedIssuance
    {
        internal ResolvedIssuance(int count, ResolvedPerTargetInput args)
        {
            Count = count;
            Args = args;
        }

        /// <summary>発行する数。</summary>
        public int Count { get; }

        /// <summary>引数の配り方。渡す引数が無いメソッドでは受け取らない取り方になる。</summary>
        public ResolvedPerTargetInput Args { get; }
    }

    /// <summary>
    /// ハンドルを発行する要求の入力を解く。1回の呼び出しで複数を発行できるので、発行する数と、
    /// 引数の配り方を決める。受け手に紐づくメソッドは受け手1件につき1個を発行するので、発行する数を
    /// 別に受け取らない。
    /// </summary>
    public static class IssuanceInput
    {
        /// <summary>発行する数を受け取る項目の名前。</summary>
        public const string CountName = "count";

        /// <summary>
        /// 発行の要求を解く。持たない項目には null を渡す。<paramref name="receiverCount"/> は
        /// 受け手に紐づくメソッドで解決した受け手の件数、<paramref name="takesArgs"/> は呼び出し側が
        /// 渡す引数を持つか、<paramref name="countLimit"/> はこの分岐の発行数の上限である。
        /// </summary>
        public static bool TryResolve(
            IssuanceKind kind,
            int? count,
            object args,
            IList<object> argsList,
            int receiverCount,
            bool takesArgs,
            int countLimit,
            out ResolvedIssuance resolved,
            out string code,
            out string message)
        {
            if (receiverCount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(receiverCount), receiverCount, "0以上でなければならない。");
            }

            if (countLimit < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(countLimit), countLimit, "1以上でなければならない。");
            }

            resolved = null;
            code = null;
            message = null;
            int generated;
            if (kind == IssuanceKind.ReceiverBound)
            {
                if (count.HasValue)
                {
                    return Invalid(
                        CountName + " は受け手に紐づくメソッドでは持てない。受け手1件につき1個を発行する。",
                        out code,
                        out message);
                }

                generated = receiverCount;
            }
            else if (!TryTakeCount(count, args, argsList, takesArgs, out generated, out code, out message))
            {
                return false;
            }

            if (generated > countLimit)
            {
                return Invalid(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "発行する数が上限を超えている: {0} 件(上限は {1} 件)",
                        generated,
                        countLimit),
                    out code,
                    out message);
            }

            if (!PerTargetInput.TryResolve(
                args,
                argsList,
                generated,
                PerTargetInput.Args,
                takesArgs,
                out ResolvedPerTargetInput taken,
                out code,
                out message))
            {
                return false;
            }

            resolved = new ResolvedIssuance(generated, taken);

            return true;
        }

        /// <summary>
        /// コンストラクタとコネクタのメソッドが発行する数を決める。要素ごとの引数の並びは、その長さが
        /// 発行する数になるので、発行する数を別に受け取らない。
        /// </summary>
        private static bool TryTakeCount(
            int? count,
            object args,
            IList<object> argsList,
            bool takesArgs,
            out int generated,
            out string code,
            out string message)
        {
            generated = 0;
            code = null;
            message = null;
            if (takesArgs && argsList != null)
            {
                if (count.HasValue)
                {
                    return Invalid(
                        CountName + " は " + PerTargetInput.Args.PerTarget
                            + " と同時に持てない。並びの長さが発行する数になる。",
                        out code,
                        out message);
                }

                generated = argsList.Count;
                if (generated < 1)
                {
                    return Invalid(
                        PerTargetInput.Args.PerTarget + " が空である。空だと発行する数が決まらない。",
                        out code,
                        out message);
                }

                return true;
            }

            if (!count.HasValue)
            {
                return Invalid(CountName + " が無い。発行する数が決まらない。", out code, out message);
            }

            if (count.Value < 1)
            {
                return Invalid(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} が1を下回っている: {1}",
                        CountName,
                        count.Value),
                    out code,
                    out message);
            }

            generated = count.Value;

            return true;
        }

        private static bool Invalid(string text, out string code, out string message)
        {
            code = ToolEnvelope.InvalidArgument;
            message = text;

            return false;
        }
    }
}
