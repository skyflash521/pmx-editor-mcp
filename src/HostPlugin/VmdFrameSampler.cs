using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp
{
    /// <summary>それぞれ0から127。</summary>
    internal struct IplCurve
    {
        internal IplCurve(int x1, int y1, int x2, int y2)
        {
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
        }

        internal int X1 { get; }

        internal int Y1 { get; }

        internal int X2 { get; }

        internal int Y2 { get; }

        internal static IplCurve Linear
        {
            get { return new IplCurve(20, 20, 107, 107); }
        }
    }

    /// <summary>回転は x・y・z・w の順。</summary>
    internal sealed class BoneKeySample
    {
        internal BoneKeySample(
            int frame, float[] translation, float[] rotation, IplCurve x, IplCurve y, IplCurve z, IplCurve r)
        {
            Frame = frame;
            Translation = translation;
            Rotation = rotation;
            CurveX = x;
            CurveY = y;
            CurveZ = z;
            CurveR = r;
        }

        internal int Frame { get; }

        internal float[] Translation { get; }

        internal float[] Rotation { get; }

        internal IplCurve CurveX { get; }

        internal IplCurve CurveY { get; }

        internal IplCurve CurveZ { get; }

        internal IplCurve CurveR { get; }
    }

    internal sealed class MorphKeySample
    {
        internal MorphKeySample(int frame, float value)
        {
            Frame = frame;
            Value = value;
        }

        internal int Frame { get; }

        internal float Value { get; }
    }

    /// <summary>回転は x・y・z・w の順。</summary>
    internal sealed class BonePoseValue
    {
        internal BonePoseValue(string name, float[] translation, float[] rotation)
        {
            Name = name;
            Translation = translation;
            Rotation = rotation;
        }

        internal string Name { get; }

        internal float[] Translation { get; }

        internal float[] Rotation { get; }
    }

    internal sealed class MorphValue
    {
        internal MorphValue(string name, float value)
        {
            Name = name;
            Value = value;
        }

        internal string Name { get; }

        internal float Value { get; }
    }

    internal sealed class Placed<T>
    {
        internal Placed(T key, int frame, int before)
        {
            Key = key;
            Frame = frame;
            Before = before;
        }

        internal T Key { get; }

        internal int Frame { get; }

        internal int Before { get; set; }
    }

    internal static class VmdFrameSampler
    {
        private const int FrameScale = 2;

        private const int TableSize = 32;

        /// <summary>キーが1つも無いときは null。キーはエディタが VMD を読み込むときの並べ方で並べる。</summary>
        internal static BonePoseValue Bone(string name, IEnumerable<BoneKeySample> keys, int frame)
        {
            if (!keys.Any())
            {
                return null;
            }

            BoneKeySample initial = new BoneKeySample(
                0, new float[3], new[] { 0f, 0f, 0f, 1f }, IplCurve.Linear, IplCurve.Linear, IplCurve.Linear, IplCurve.Linear);
            List<Placed<BoneKeySample>> placed = Load(initial, keys, key => key.Frame);
            int found = Find(placed, frame);
            if (found >= 0)
            {
                return Pose(name, placed[found].Key);
            }

            int next = ~found;
            if (next >= placed.Count)
            {
                return Pose(name, placed[placed.Count - 1].Key);
            }

            if (next <= 0 || placed[next].Before < 0)
            {
                return Pose(name, placed[next].Key);
            }

            BoneKeySample before = placed[next - 1].Key;
            BoneKeySample after = placed[next].Key;
            float at = Progress(frame, before.Frame, after.Frame);
            float[] translation =
            {
                Mix(before.Translation[0], after.Translation[0], Curve(after.CurveX, at)),
                Mix(before.Translation[1], after.Translation[1], Curve(after.CurveY, at)),
                Mix(before.Translation[2], after.Translation[2], Curve(after.CurveZ, at)),
            };

            return new BonePoseValue(
                name, translation, Slerp(before.Rotation, after.Rotation, Curve(after.CurveR, at)));
        }

        /// <summary>キーが1つも無いときは null。キーはエディタが VMD を読み込むときの並べ方で並べる。</summary>
        internal static MorphValue Morph(string name, IEnumerable<MorphKeySample> keys, int frame)
        {
            if (!keys.Any())
            {
                return null;
            }

            List<Placed<MorphKeySample>> placed = Load(new MorphKeySample(0, 0f), keys, key => key.Frame);
            int found = Find(placed, frame);
            if (found >= 0)
            {
                return new MorphValue(name, placed[found].Key.Value);
            }

            int next = ~found;
            if (next >= placed.Count)
            {
                return new MorphValue(name, placed[placed.Count - 1].Key.Value);
            }

            if (next <= 0 || placed[next].Before == -1)
            {
                return new MorphValue(name, placed[next].Key.Value);
            }

            MorphKeySample before = placed[next - 1].Key;
            MorphKeySample after = placed[next].Key;
            float at = Progress(frame, before.Frame, after.Frame);

            return new MorphValue(name, Mix(before.Value, after.Value, at));
        }

        private static List<Placed<T>> Load<T>(T initial, IEnumerable<T> keys, Func<T, int> frameOf)
        {
            List<Placed<T>> placed = new List<Placed<T>>();
            Append(placed, initial, frameOf(initial));
            foreach (T key in keys)
            {
                int at = Find(placed, frameOf(key));
                if (at >= 0)
                {
                    placed.RemoveAt(at);
                    if (placed.Count > at)
                    {
                        placed[at].Before = at > 0 ? placed[at - 1].Frame * FrameScale : -1;
                    }
                }

                Append(placed, key, frameOf(key));
            }

            placed.Sort((x, y) => x.Frame - y.Frame);

            return placed;
        }

        private static void Append<T>(List<Placed<T>> placed, T key, int frame)
        {
            placed.Add(new Placed<T>(key, frame, placed.Count > 0 ? placed[placed.Count - 1].Frame * FrameScale : -1));
        }

        private static int Find<T>(List<Placed<T>> placed, int frame)
        {
            return placed.BinarySearch(new Placed<T>(default(T), frame, -1), Comparer<Placed<T>>.Create((x, y) => x.Frame - y.Frame));
        }

        private static BonePoseValue Pose(string name, BoneKeySample key)
        {
            return new BonePoseValue(name, (float[])key.Translation.Clone(), (float[])key.Rotation.Clone());
        }

        private static float Mix(float from, float to, float at)
        {
            return ((to - from) * at) + from;
        }

        private static float Progress(int frame, int from, int to)
        {
            return (float)(frame - from) / (float)(to - from);
        }

        internal static float Curve(IplCurve curve, float at)
        {
            if (curve.X1 == curve.Y1 && curve.X2 == curve.Y2)
            {
                return at;
            }

            double unit = 1.0 / 127.0;
            double[] x = Coefficients(curve.X1 * unit, curve.X2 * unit);
            double[] y = Coefficients(curve.Y1 * unit, curve.Y2 * unit);
            double target = at;
            if (target == 0.0)
            {
                return 0f;
            }

            double[] table = new double[TableSize];
            for (int i = 0; i < TableSize; i++)
            {
                table[i] = Position(x, (double)i / TableSize);
            }

            int low = 0;
            int high = TableSize;
            int middle = 0;
            while (low <= high)
            {
                middle = (low + high) >> 1;
                if (middle == TableSize - 1 || (table[middle] < target && target <= table[middle + 1]))
                {
                    break;
                }

                if (table[middle] < target)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            if (middle == 0)
            {
                middle = 1;
            }

            double t = table[middle];
            double next = 0.0;
            for (int i = 0; i < 1000; i++)
            {
                next = t - ((Position(x, t) - target) / Slope(x, t));
                if (Math.Abs((next - t) / t) < 1E-05)
                {
                    break;
                }

                t = next;
            }

            return (float)Position(y, next);
        }

        private static double[] Coefficients(double p1, double p2)
        {
            return new[] { 1.0 - (3.0 * (p2 - p1)), 3.0 * (p2 - (2.0 * p1)), 3.0 * p1 };
        }

        private static double Position(double[] c, double t)
        {
            return t * ((c[0] * t * t) + (c[1] * t) + c[2]);
        }

        private static double Slope(double[] c, double t)
        {
            return (t * ((3.0 * c[0] * t) + (2.0 * c[1]))) + c[2];
        }

        private static float[] Slerp(float[] from, float[] to, float amount)
        {
            float dot = (float)(((double)from[1] * to[1]) + ((double)from[0] * to[0]) + ((double)from[2] * to[2]) + ((double)from[3] * to[3]));
            bool flipped = false;
            if (dot < 0f)
            {
                flipped = true;
                dot = -dot;
            }

            float fromWeight;
            float toWeight;
            if (dot > 0.999999f)
            {
                fromWeight = (float)(1.0 - amount);
                toWeight = flipped ? -amount : amount;
            }
            else
            {
                float angle = (float)Math.Acos(dot);
                float inverse = (float)(1.0 / Math.Sin(angle));
                fromWeight = (float)((float)Math.Sin((1.0 - amount) * angle) * (double)inverse);
                toWeight = flipped
                    ? (float)((float)(0.0 - Math.Sin(angle * (double)amount)) * (double)inverse)
                    : (float)((float)Math.Sin(angle * (double)amount) * (double)inverse);
            }

            float[] mixed = new float[4];
            for (int i = 0; i < 4; i++)
            {
                mixed[i] = (float)(((double)to[i] * toWeight) + ((double)from[i] * fromWeight));
            }

            return mixed;
        }
    }
}
