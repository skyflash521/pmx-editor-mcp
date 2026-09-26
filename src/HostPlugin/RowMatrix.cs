// エディタが座標を回すときと同じ、行ベクトルへ右から掛ける取り決めの3行3列の行列。成分は倍精度で持つ。

using System;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    public sealed class RowMatrix
    {
        private static readonly double LockedCosine = Math.Sqrt(Math.Pow(2d, -50d));

        private readonly double[,] _cells;

        private RowMatrix(double[,] cells)
        {
            _cells = cells;
        }

        public static RowMatrix Diagonal(double x, double y, double z)
        {
            return new RowMatrix(new double[,] { { x, 0d, 0d }, { 0d, y, 0d }, { 0d, 0d, z } });
        }

        /// <summary>
        /// SlimDX の Matrix.RotationYawPitchRoll と同じ行列。Z軸・X軸・Y軸の順に回す。角はラジアン。
        /// </summary>
        public static RowMatrix YawPitchRoll(double yaw, double pitch, double roll)
        {
            double cy = Math.Cos(yaw);
            double sy = Math.Sin(yaw);
            double cp = Math.Cos(pitch);
            double sp = Math.Sin(pitch);
            double cr = Math.Cos(roll);
            double sr = Math.Sin(roll);
            RowMatrix aroundZ = new RowMatrix(
                new double[,] { { cr, sr, 0d }, { -sr, cr, 0d }, { 0d, 0d, 1d } });
            RowMatrix aroundX = new RowMatrix(
                new double[,] { { 1d, 0d, 0d }, { 0d, cp, sp }, { 0d, -sp, cp } });
            RowMatrix aroundY = new RowMatrix(
                new double[,] { { cy, 0d, -sy }, { 0d, 1d, 0d }, { sy, 0d, cy } });

            return aroundZ.Times(aroundX).Times(aroundY);
        }

        /// <summary>単位の軸 (x, y, z) のまわりに回す行列。正の角で、軸 X なら +Y を +Z へ回す。角はラジアン。</summary>
        public static RowMatrix AroundAxis(double x, double y, double z, double angle)
        {
            double c = Math.Cos(angle);
            double s = Math.Sin(angle);
            double t = 1d - c;

            return new RowMatrix(
                new double[,]
                {
                    { (t * x * x) + c, (t * x * y) + (s * z), (t * x * z) - (s * y) },
                    { (t * x * y) - (s * z), (t * y * y) + c, (t * y * z) + (s * x) },
                    { (t * x * z) + (s * y), (t * y * z) - (s * x), (t * z * z) + c },
                });
        }

        public RowMatrix Times(RowMatrix right)
        {
            if (right == null)
            {
                throw new ArgumentNullException(nameof(right));
            }

            double[,] made = new double[3, 3];
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    for (int at = 0; at < 3; at++)
                    {
                        made[row, column] += _cells[row, at] * right._cells[at, column];
                    }
                }
            }

            return new RowMatrix(made);
        }

        public V3 Transform(V3 given)
        {
            if (given == null)
            {
                throw new ArgumentNullException(nameof(given));
            }

            return new V3(
                (float)((given.X * _cells[0, 0]) + (given.Y * _cells[1, 0]) + (given.Z * _cells[2, 0])),
                (float)((given.X * _cells[0, 1]) + (given.Y * _cells[1, 1]) + (given.Z * _cells[2, 1])),
                (float)((given.X * _cells[0, 2]) + (given.Y * _cells[1, 2]) + (given.Z * _cells[2, 2])));
        }

        public V3 TransformAbout(V3 given, V3 center)
        {
            if (given == null)
            {
                throw new ArgumentNullException(nameof(given));
            }

            if (center == null)
            {
                throw new ArgumentNullException(nameof(center));
            }

            double x = (double)given.X - center.X;
            double y = (double)given.Y - center.Y;
            double z = (double)given.Z - center.Z;

            return new V3(
                (float)((x * _cells[0, 0]) + (y * _cells[1, 0]) + (z * _cells[2, 0]) + center.X),
                (float)((x * _cells[0, 1]) + (y * _cells[1, 1]) + (z * _cells[2, 1]) + center.Y),
                (float)((x * _cells[0, 2]) + (y * _cells[1, 2]) + (z * _cells[2, 2]) + center.Z));
        }

        /// <summary>
        /// PMXエディタの CMath.MatrixToEuler_ZXY と同じく、単精度の成分と単精度の途中の値で、剛体と
        /// ジョイントが持つ回転の角へ直す。エディタが NaN を得る asin の定義域の外だけは端へ寄せる。
        /// </summary>
        public V3 ToEulerZxy()
        {
            float m11 = (float)_cells[0, 0];
            float m12 = (float)_cells[0, 1];
            float m13 = (float)_cells[0, 2];
            float m22 = (float)_cells[1, 1];
            float m31 = (float)_cells[2, 0];
            float m32 = (float)_cells[2, 1];
            float m33 = (float)_cells[2, 2];
            float x = 0f - (float)Math.Asin(Clamped(m32));
            if (x == (float)Math.PI / 2f || x == -(float)Math.PI / 2f)
            {
                return new V3(x, (float)Math.Atan2(0f - m13, m11), 0f);
            }

            float y = (float)Math.Atan2(m31, m33);
            float z = (float)Math.Asin(Clamped(m12 / (float)Math.Cos(x)));
            if (m22 < 0f)
            {
                z = (float)Math.PI - z;
            }

            return new V3(x, y, z);
        }

        /// <summary>
        /// <see cref="YawPitchRoll"/> へ Y・X・Z の順に渡すと同じ行列になる、X・Y・Z の角(ラジアン)。
        /// X の余弦が0に近いときは Z を0にする。
        /// </summary>
        public V3 ToYawPitchRollAngles()
        {
            double x = Math.Asin(Math.Max(-1d, Math.Min(1d, -_cells[2, 1])));
            if (Math.Cos(x) < LockedCosine)
            {
                return new V3((float)x, (float)Math.Atan2(-_cells[0, 2], _cells[0, 0]), 0f);
            }

            return new V3(
                (float)x, (float)Math.Atan2(_cells[2, 0], _cells[2, 2]), (float)Math.Atan2(_cells[0, 1], _cells[1, 1]));
        }

        private static float Clamped(float value)
        {
            return Math.Max(-1f, Math.Min(1f, value));
        }
    }
}
