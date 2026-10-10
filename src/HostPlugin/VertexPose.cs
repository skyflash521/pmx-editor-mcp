using System;
using System.Collections.Generic;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    internal static class VertexPose
    {
        private const double NearlySame = 0.999999;

        internal struct Vec
        {
            public readonly double X;

            public readonly double Y;

            public readonly double Z;

            public Vec(double x, double y, double z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public double Length
            {
                get { return Math.Sqrt(Dot(this)); }
            }

            public static Vec Of(V3 given)
            {
                return new Vec(given.X, given.Y, given.Z);
            }

            public static Vec operator +(Vec left, Vec right)
            {
                return new Vec(left.X + right.X, left.Y + right.Y, left.Z + right.Z);
            }

            public static Vec operator -(Vec left, Vec right)
            {
                return new Vec(left.X - right.X, left.Y - right.Y, left.Z - right.Z);
            }

            public static Vec operator *(Vec left, double by)
            {
                return new Vec(left.X * by, left.Y * by, left.Z * by);
            }

            public double Dot(Vec other)
            {
                return (X * other.X) + (Y * other.Y) + (Z * other.Z);
            }

            public V3 ToV3()
            {
                return new V3((float)X, (float)Y, (float)Z);
            }
        }

        internal struct Quat
        {
            public readonly double X;

            public readonly double Y;

            public readonly double Z;

            public readonly double W;

            public Quat(double x, double y, double z, double w)
            {
                X = x;
                Y = y;
                Z = z;
                W = w;
            }

            public static Quat Identity
            {
                get { return new Quat(0d, 0d, 0d, 1d); }
            }

            public double Length
            {
                get { return Math.Sqrt(Dot(this)); }
            }

            public static Quat operator +(Quat left, Quat right)
            {
                return new Quat(left.X + right.X, left.Y + right.Y, left.Z + right.Z, left.W + right.W);
            }

            public static Quat operator -(Quat left, Quat right)
            {
                return new Quat(left.X - right.X, left.Y - right.Y, left.Z - right.Z, left.W - right.W);
            }

            public static Quat operator *(Quat left, double by)
            {
                return new Quat(left.X * by, left.Y * by, left.Z * by, left.W * by);
            }

            public static Quat Multiply(Quat a, Quat b)
            {
                return new Quat(
                    (a.W * b.X) + (a.X * b.W) + (a.Y * b.Z) - (a.Z * b.Y),
                    (a.W * b.Y) - (a.X * b.Z) + (a.Y * b.W) + (a.Z * b.X),
                    (a.W * b.Z) + (a.X * b.Y) - (a.Y * b.X) + (a.Z * b.W),
                    (a.W * b.W) - (a.X * b.X) - (a.Y * b.Y) - (a.Z * b.Z));
            }

            public Quat Conjugate()
            {
                return new Quat(-X, -Y, -Z, W);
            }

            public double Dot(Quat other)
            {
                return (X * other.X) + (Y * other.Y) + (Z * other.Z) + (W * other.W);
            }

            public Quat Normalized()
            {
                return this * (1d / Length);
            }
        }

        internal sealed class Mat
        {
            private readonly double[] _e;

            private Mat(double[] elements)
            {
                _e = elements;
            }

            public static Mat Identity
            {
                get { return new Mat(new double[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 }); }
            }

            public static Mat Of(Quat q)
            {
                double x = q.X;
                double y = q.Y;
                double z = q.Z;
                double w = q.W;

                return new Mat(new[]
                {
                    1 - (2 * ((y * y) + (z * z))), 2 * ((x * y) - (z * w)), 2 * ((x * z) + (y * w)),
                    2 * ((x * y) + (z * w)), 1 - (2 * ((x * x) + (z * z))), 2 * ((y * z) - (x * w)),
                    2 * ((x * z) - (y * w)), 2 * ((y * z) + (x * w)), 1 - (2 * ((x * x) + (y * y))),
                });
            }

            public static Mat operator *(Mat left, Mat right)
            {
                double[] made = new double[9];
                for (int row = 0; row < 3; row++)
                {
                    for (int column = 0; column < 3; column++)
                    {
                        made[(row * 3) + column] = (left._e[row * 3] * right._e[column])
                            + (left._e[(row * 3) + 1] * right._e[3 + column])
                            + (left._e[(row * 3) + 2] * right._e[6 + column]);
                    }
                }

                return new Mat(made);
            }

            public Vec Apply(Vec v)
            {
                return new Vec(
                    (_e[0] * v.X) + (_e[1] * v.Y) + (_e[2] * v.Z),
                    (_e[3] * v.X) + (_e[4] * v.Y) + (_e[5] * v.Z),
                    (_e[6] * v.X) + (_e[7] * v.Y) + (_e[8] * v.Z));
            }

            public Quat ToQuat()
            {
                double trace = _e[0] + _e[4] + _e[8];
                double[] q = new double[4];
                if (trace > 0d)
                {
                    double s = Math.Sqrt(trace + 1d) * 2d;
                    q[0] = (_e[7] - _e[5]) / s;
                    q[1] = (_e[2] - _e[6]) / s;
                    q[2] = (_e[3] - _e[1]) / s;
                    q[3] = s / 4d;
                }
                else
                {
                    int i = 0;
                    if (_e[4] > _e[0])
                    {
                        i = 1;
                    }

                    if (_e[8] > _e[(i * 3) + i])
                    {
                        i = 2;
                    }

                    int j = (i + 1) % 3;
                    int k = (i + 2) % 3;
                    double s = Math.Sqrt(1d + _e[(i * 3) + i] - _e[(j * 3) + j] - _e[(k * 3) + k]) * 2d;
                    q[3] = (_e[(k * 3) + j] - _e[(j * 3) + k]) / s;
                    q[i] = s / 4d;
                    q[j] = (_e[(j * 3) + i] + _e[(i * 3) + j]) / s;
                    q[k] = (_e[(k * 3) + i] + _e[(i * 3) + k]) / s;
                }

                return new Quat(q[0], q[1], q[2], q[3]).Normalized();
            }
        }

        internal sealed class BonePose
        {
            public BonePose(Quat local, Vec move, Mat rotation, Vec world, Vec shift)
            {
                Local = local;
                Move = move;
                Rotation = rotation;
                World = world;
                Shift = shift;
            }

            public Quat Local { get; }

            public Vec Move { get; }

            public Mat Rotation { get; }

            public Vec World { get; }

            public Vec Shift { get; }
        }

        internal static Quat Slerp(Quat a, Quat b, double t)
        {
            double d = a.Dot(b);
            bool flip = d < 0d;
            d = Math.Abs(d);
            double first;
            double second;
            if (d > NearlySame)
            {
                first = 1d - t;
                second = flip ? -t : t;
            }
            else
            {
                double angle = Math.Acos(d);
                double scale = 1d / Math.Sin(angle);
                first = Math.Sin((1d - t) * angle) * scale;
                second = Math.Sin(t * angle) * scale * (flip ? -1d : 1d);
            }

            return ((a * first) + (b * second)).Normalized();
        }

        internal static bool TrySkin(
            IPXVertex vertex,
            Func<IPXBone, BonePose> poseOf,
            out Vec position,
            out Vec normal)
        {
            Vec x = Vec.Of(vertex.Position);
            Vec n = Vec.Of(vertex.Normal);
            position = x;
            normal = n;
            if (vertex.SDEF && vertex.Bone1 != null && vertex.Bone2 != null)
            {
                Sdef(vertex, poseOf, x, n, out position, out normal);

                return true;
            }

            if (vertex.QDEF)
            {
                return Qdef(vertex, poseOf, x, n, out position, out normal);
            }

            return Linear(vertex, poseOf, x, n, out position, out normal);
        }

        internal static IEnumerable<IPXBone> Used(IPXVertex vertex)
        {
            if (vertex.SDEF && vertex.Bone1 != null && vertex.Bone2 != null)
            {
                yield return vertex.Bone1;
                yield return vertex.Bone2;

                yield break;
            }

            foreach (KeyValuePair<IPXBone, double> slot in Slots(vertex))
            {
                yield return slot.Key;
            }
        }

        private static bool Linear(
            IPXVertex vertex,
            Func<IPXBone, BonePose> poseOf,
            Vec x,
            Vec n,
            out Vec position,
            out Vec normal)
        {
            position = x;
            normal = n;
            double sum = 0d;
            Vec moved = new Vec(0d, 0d, 0d);
            Vec turned = new Vec(0d, 0d, 0d);
            foreach (KeyValuePair<IPXBone, double> slot in Slots(vertex))
            {
                BonePose pose = poseOf(slot.Key);
                sum += slot.Value;
                moved = moved + ((pose.Rotation.Apply(x) + pose.Shift) * slot.Value);
                turned = turned + (pose.Rotation.Apply(n) * slot.Value);
            }

            if (sum == 0d)
            {
                return false;
            }

            position = moved * (1d / sum);
            normal = Unit(turned * (1d / sum), n);

            return true;
        }

        private static void Sdef(
            IPXVertex vertex,
            Func<IPXBone, BonePose> poseOf,
            Vec x,
            Vec n,
            out Vec position,
            out Vec normal)
        {
            BonePose pose0 = poseOf(vertex.Bone1);
            BonePose pose1 = poseOf(vertex.Bone2);
            double w0 = vertex.Weight1;
            double w1 = 1d - w0;
            Vec p0 = Vec.Of(vertex.Bone1.Position);
            Vec axis = Vec.Of(vertex.Bone2.Position) - p0;
            double length = axis.Length;
            axis = length > 0d ? axis * (1d / length) : new Vec(0d, 0d, 0d);
            Vec c0 = (axis * axis.Dot(x - p0)) + p0;
            Vec rw = (Vec.Of(vertex.SDEF_R0) - Vec.Of(vertex.SDEF_R1)) * (0.5d * w0 * w1);
            Mat turn = Mat.Of(Slerp(pose0.Rotation.ToQuat(), pose1.Rotation.ToQuat(), w1));
            position = turn.Apply(x - c0)
                + (pose0.Rotation.Apply(c0) * w0) + (pose1.Rotation.Apply(c0) * w1)
                + (pose0.Shift * w0) + (pose1.Shift * w1)
                + pose0.Rotation.Apply(rw) - pose1.Rotation.Apply(rw);
            normal = Unit(turn.Apply(n), n);
        }

        private static bool Qdef(
            IPXVertex vertex,
            Func<IPXBone, BonePose> poseOf,
            Vec x,
            Vec n,
            out Vec position,
            out Vec normal)
        {
            position = x;
            normal = n;
            IPXBone[] bones = { vertex.Bone1, vertex.Bone2, vertex.Bone3, vertex.Bone4 };
            float[] weights = { vertex.Weight1, vertex.Weight2, vertex.Weight3, vertex.Weight4 };
            Quat first = new Quat(0d, 0d, 0d, 0d);
            Quat real = new Quat(0d, 0d, 0d, 0d);
            Quat dual = new Quat(0d, 0d, 0d, 0d);
            for (int at = 0; at < bones.Length; at++)
            {
                if (bones[at] == null || weights[at] == 0f)
                {
                    continue;
                }

                BonePose pose = poseOf(bones[at]);
                Quat q = pose.Rotation.ToQuat();
                Quat d = Quat.Multiply(new Quat(pose.Shift.X, pose.Shift.Y, pose.Shift.Z, 0d), q) * 0.5d;
                if (at > 0 && first.Dot(q) < 0d)
                {
                    q = q * -1d;
                    d = d * -1d;
                }

                if (at == 0)
                {
                    first = q;
                }

                real = real + (q * weights[at]);
                dual = dual + (d * weights[at]);
            }

            double size = real.Length;
            if (!(size > 0d))
            {
                return false;
            }

            real = real * (1d / size);
            dual = dual * (1d / size);
            dual = dual - (real * real.Dot(dual));
            Quat shift = Quat.Multiply(dual, real.Conjugate()) * 2d;
            Mat turn = Mat.Of(real);
            position = turn.Apply(x) + new Vec(shift.X, shift.Y, shift.Z);
            normal = Unit(turn.Apply(n), n);

            return true;
        }

        private static IEnumerable<KeyValuePair<IPXBone, double>> Slots(IPXVertex vertex)
        {
            IPXBone[] bones = { vertex.Bone1, vertex.Bone2, vertex.Bone3, vertex.Bone4 };
            float[] weights = { vertex.Weight1, vertex.Weight2, vertex.Weight3, vertex.Weight4 };
            for (int at = 0; at < bones.Length; at++)
            {
                if (bones[at] != null && weights[at] != 0f)
                {
                    yield return new KeyValuePair<IPXBone, double>(bones[at], weights[at]);
                }
            }
        }

        private static Vec Unit(Vec turned, Vec otherwise)
        {
            double length = turned.Length;

            return length > 0d ? turned * (1d / length) : otherwise;
        }
    }
}
