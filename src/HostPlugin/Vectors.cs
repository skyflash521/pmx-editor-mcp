// SDKの座標型を値として扱う計算。どの計算も元を変えずに新しい値を返し、長さ・正規化・垂直な向きは
// 倍精度で求めてから単精度へ戻す。

using System;
using System.Collections.Generic;
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

        /// <summary>
        /// 点と線分の隔たり。線分が長さを持たないときは、その一点との隔たりを返す。
        /// </summary>
        public static float DistanceToSegment(V3 point, V3 from, V3 to)
        {
            double alongX = (double)to.X - from.X;
            double alongY = (double)to.Y - from.Y;
            double alongZ = (double)to.Z - from.Z;
            double awayX = (double)point.X - from.X;
            double awayY = (double)point.Y - from.Y;
            double awayZ = (double)point.Z - from.Z;
            double square = (alongX * alongX) + (alongY * alongY) + (alongZ * alongZ);
            double at = square == 0d
                ? 0d
                : ((awayX * alongX) + (awayY * alongY) + (awayZ * alongZ)) / square;
            at = at < 0d ? 0d : (at > 1d ? 1d : at);

            return (float)Spread(
                awayX - (alongX * at), awayY - (alongY * at), awayZ - (alongZ * at));
        }

        /// <summary>
        /// いくつかの向きの和を、長さを1にそろえて返す。和が長さを持たないときは長さを持たない向きを
        /// 返す。足し合わせも倍精度で行うので、単精度で持てるどの向きを何本足しても潰れない。
        /// </summary>
        public static V3 NormalizedSum(IEnumerable<V3> given)
        {
            if (given == null)
            {
                throw new ArgumentNullException(nameof(given));
            }

            double x = 0d;
            double y = 0d;
            double z = 0d;
            foreach (V3 held in given)
            {
                x += held.X;
                y += held.Y;
                z += held.Z;
            }

            return Shortened(x, y, z, new V3(0f, 0f, 0f));
        }

        /// <summary>
        /// 2つの点の中ほど。足し合わせを倍精度で行うので、単精度で持てるどの2点でも潰れない。
        /// </summary>
        public static V3 Between(V3 first, V3 second)
        {
            return new V3(
                (float)(((double)first.X + second.X) / 2d),
                (float)(((double)first.Y + second.Y) / 2d),
                (float)(((double)first.Z + second.Z) / 2d));
        }

        /// <summary>
        /// いくつかの点の重心。足し合わせを倍精度で行うので、単精度で持てるどの点を何個足しても
        /// 潰れない。1つも無ければ原点を返す。
        /// </summary>
        public static V3 Middle(IEnumerable<V3> given)
        {
            if (given == null)
            {
                throw new ArgumentNullException(nameof(given));
            }

            double x = 0d;
            double y = 0d;
            double z = 0d;
            int count = 0;
            foreach (V3 held in given)
            {
                x += held.X;
                y += held.Y;
                z += held.Z;
                count++;
            }

            return count == 0
                ? new V3(0f, 0f, 0f)
                : new V3((float)(x / count), (float)(y / count), (float)(z / count));
        }

        /// <summary>
        /// ある点から別の点への隔たりを、倍率を掛けて返す。引き算も掛け算も倍精度で行うので、
        /// 単精度で持てるどの2点でも、結果が単精度に収まるかぎり潰れない。
        /// </summary>
        public static V3 Apart(V3 to, V3 from, float by)
        {
            return new V3(
                (float)(((double)to.X - from.X) * by),
                (float)(((double)to.Y - from.Y) * by),
                (float)(((double)to.Z - from.Z) * by));
        }

        /// <summary>
        /// ある点から別の点への向きを、長さを1にそろえて返す。引き算も正規化も倍精度で行うので、
        /// 単精度で持てるどの2点でも潰れない。2点が同じ点なら長さを持たない向きを返す。
        /// </summary>
        public static V3 Toward(V3 to, V3 from)
        {
            return Shortened(
                (double)to.X - from.X,
                (double)to.Y - from.Y,
                (double)to.Z - from.Z,
                new V3(0f, 0f, 0f));
        }

        /// <summary>同じ成分を持つ、別の向き。</summary>
        public static V3 Copied(V3 given)
        {
            return new V3(given.X, given.Y, given.Z);
        }

        /// <summary>同じ3つの成分を持つ向きか。</summary>
        public static bool Same(V3 left, V3 right)
        {
            return left.X == right.X && left.Y == right.Y && left.Z == right.Z;
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
