using System;
using PEPlugin.Vme;

namespace PmxEditorMcp
{
    /// <summary>
    /// VMEのパスへ点を置く・足す呼び出しと、経路の種類の書き込みを、書いた後の点の数が経路の
    /// 種類の下限を割るときに呼ぶ前に断る。SDKはスプラインを3点未満で組むと配列の境界外で落ち、
    /// 点を置いたまま経路を壊して残す。直線は2点未満だと経路を組まずに戻り、読むと置いた点と
    /// 違う点を返す。0点は経路を空にするだけなので断らない。点が0個のパスは、SDKの RangeMin と
    /// 間隔だけを渡す GetPathPoints が空の距離の配列の先頭を読んで落ちるので、RangeMin には
    /// 一次資料が固定と書く0を返し、点列の読み取りは呼ぶ前に断る。
    /// </summary>
    public static class VmePathPoints
    {
        public const string SetPointKey = "PEPlugin.Vme.IPEVmePath.SetPoint(SlimDX.Vector3[])";

        public const string AddPointKey = "PEPlugin.Vme.IPEVmePath.AddPoint(SlimDX.Vector3)";

        public const string PathTypeKey = "PEPlugin.Vme.IPEVmePath.PathType()";

        public const string GetPathPointsKey =
            "PEPlugin.Vme.IPEVmePath.GetPathPoints(System.Double)";

        public const string RangeMinKey = "PEPlugin.Vme.IPEVmePath.RangeMin()";

        private const int SplineLeast = 3;

        private const int LinearLeast = 2;

        public static bool TryAccept(PEVmePathType type, int points, out string message)
        {
            message = null;
            int least = type == PEVmePathType.Spline ? SplineLeast : LinearLeast;
            if (points == 0 || points >= least)
            {
                return true;
            }

            message = "経路の種類が " + type + " のパスは、点を " + least + " 個以上置かないと"
                + "経路を組めないので呼べない。書いた後の点は " + points + " 個になる。"
                + "点をまとめて置くか、経路の種類を変えてから置く。";

            return false;
        }

        /// <summary>
        /// 置かれている点の数。点の位置に当たらない番号へは -1 を返す GetDistanceAtPoint で、
        /// 数の上限を倍々に広げてから二分で探す。
        /// </summary>
        public static int Count(IPEVmePath path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (path.GetDistanceAtPoint(0) < 0)
            {
                return 0;
            }

            int inside = 0;
            int outside = 1;
            while (path.GetDistanceAtPoint(outside) >= 0)
            {
                inside = outside;
                outside = outside > int.MaxValue / 2 ? int.MaxValue : outside * 2;
            }

            while (outside - inside > 1)
            {
                int middle = inside + ((outside - inside) / 2);
                if (path.GetDistanceAtPoint(middle) >= 0)
                {
                    inside = middle;
                }
                else
                {
                    outside = middle;
                }
            }

            return inside + 1;
        }

        /// <summary>パスへ点を置く・足す呼び出しと、点列の読み取りでなければ確かめずに通す。</summary>
        public static bool TryCall(
            string rowKey, object item, object[] args, out string code, out string message)
        {
            code = ToolEnvelope.InvalidArgument;
            message = null;
            IPEVmePath path = item as IPEVmePath;
            if (path == null || args == null)
            {
                return true;
            }

            if (string.Equals(rowKey, SetPointKey, StringComparison.Ordinal))
            {
                Array placed = args.Length == 0 ? null : args[0] as Array;

                return TryAccept(path.PathType, placed == null ? 0 : placed.Length, out message);
            }

            if (string.Equals(rowKey, AddPointKey, StringComparison.Ordinal))
            {
                return TryAccept(path.PathType, Count(path) + 1, out message);
            }

            if (string.Equals(rowKey, GetPathPointsKey, StringComparison.Ordinal)
                && Count(path) == 0)
            {
                code = ToolEnvelope.NotApplicable;
                message = "点が置かれていないパスからは点列を読めない。点を置いてから読む。";

                return false;
            }

            return true;
        }

        /// <summary>
        /// SDKを呼ばずに答える読み取りなら真で、その値を渡す。点が0個のパスの RangeMin だけが当たる。
        /// </summary>
        public static bool TryRead(string rowKey, object item, out object value)
        {
            value = null;
            IPEVmePath path = item as IPEVmePath;
            if (path == null
                || !string.Equals(rowKey, RangeMinKey, StringComparison.Ordinal)
                || Count(path) != 0)
            {
                return false;
            }

            value = 0.0;

            return true;
        }

        /// <summary>パスの経路の種類の書き込みでなければ確かめずに通す。</summary>
        public static bool TryWrite(string rowKey, object item, object value, out string message)
        {
            message = null;
            IPEVmePath path = item as IPEVmePath;
            if (path == null
                || !(value is PEVmePathType)
                || !string.Equals(rowKey, PathTypeKey, StringComparison.Ordinal))
            {
                return true;
            }

            return TryAccept((PEVmePathType)value, Count(path), out message);
        }
    }
}
