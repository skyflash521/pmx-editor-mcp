// SDKの座標型を値として扱う計算。どの計算も元を変えずに新しい値を返し、長さ・正規化・垂直な向きは
// 倍精度で求めてから単精度へ戻す。

using System;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    public static class Vectors
    {
        public static V3 Add(V3 left, V3 right)
        {
            return new V3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
        }

        public static V3 Scale(V3 given, float by)
        {
            return new V3(given.X * by, given.Y * by, given.Z * by);
        }

        public static float Dot(V3 left, V3 right)
        {
            return (left.X * right.X) + (left.Y * right.Y) + (left.Z * right.Z);
        }

        /// <summary>長さを持たない向きはそのまま返す。</summary>
        public static V3 Normalized(V3 given)
        {
            return Shortened(given.X, given.Y, given.Z, new V3(given.X, given.Y, given.Z));
        }

        /// <summary>
        /// 2つの向きに垂直で、長さを1にそろえた向き。2つが平行なときは長さを持たない向きを返す。
        /// </summary>
        public static V3 Perpendicular(V3 left, V3 right)
        {
            return Shortened(
                ((double)left.Y * right.Z) - ((double)left.Z * right.Y),
                ((double)left.Z * right.X) - ((double)left.X * right.Z),
                ((double)left.X * right.Y) - ((double)left.Y * right.X),
                new V3(0f, 0f, 0f));
        }

        /// <summary>
        /// 3つの点が張る面に垂直で、長さを1にそろえた向き。3つが一直線に並ぶときは長さを持たない
        /// 向きを返す。点の隔たりも倍精度で求めるので、単精度で持てるどの3点でも向きが得られる。
        /// </summary>
        public static V3 PerpendicularTo(V3 first, V3 second, V3 third)
        {
            double leftX = (double)second.X - first.X;
            double leftY = (double)second.Y - first.Y;
            double leftZ = (double)second.Z - first.Z;
            double rightX = (double)third.X - first.X;
            double rightY = (double)third.Y - first.Y;
            double rightZ = (double)third.Z - first.Z;

            return Shortened(
                (leftY * rightZ) - (leftZ * rightY),
                (leftZ * rightX) - (leftX * rightZ),
                (leftX * rightY) - (leftY * rightX),
                new V3(0f, 0f, 0f));
        }

        public static bool HasLength(V3 given)
        {
            return given.X != 0f || given.Y != 0f || given.Z != 0f;
        }

        /// <summary>2つの点の隔たり。点の差も倍精度で求める。</summary>
        public static float Distance(V3 left, V3 right)
        {
            return (float)Spread(
                (double)left.X - right.X,
                (double)left.Y - right.Y,
                (double)left.Z - right.Z);
        }

        private static V3 Shortened(double x, double y, double z, V3 whenFlat)
        {
            double length = Spread(x, y, z);

            return length == 0d
                ? whenFlat
                : new V3((float)(x / length), (float)(y / length), (float)(z / length));
        }

        private static double Spread(double x, double y, double z)
        {
            return Math.Sqrt((x * x) + (y * y) + (z * z));
        }
    }
}
