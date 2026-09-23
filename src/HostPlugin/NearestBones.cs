using System;
using System.Collections.Generic;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    /// <summary>
    /// 点から最も近いボーンを引く表。ボーンごとの形(根元の点か、根元から表示先までの線分)を
    /// 囲む箱の木で持ち、箱までの距離が今の最短より遠い枝を辿らない。距離が等しいボーンが
    /// 並ぶときは、並びの中で先に来たものを返す。
    /// </summary>
    public sealed class NearestBones
    {
        private const int LeafSize = 4;

        /// <summary>箱までの距離と実際の距離の丸めの差を吸収する幅。この幅までは枝を辿る。</summary>
        private const double Slack = 1e-4;

        private readonly IList<IPXBone> _bones;

        private readonly Func<V3, int, float> _distance;

        private readonly double[] _low;

        private readonly double[] _high;

        private readonly Node _root;

        private NearestBones(
            IList<IPXBone> bones, Func<IPXBone, V3[]> shape, Func<V3, int, float> distance)
        {
            _bones = bones;
            _distance = distance;
            _low = new double[bones.Count * 3];
            _high = new double[bones.Count * 3];
            int[] order = new int[bones.Count];
            for (int at = 0; at < bones.Count; at++)
            {
                order[at] = at;
                V3[] ends = shape(bones[at]);
                for (int axis = 0; axis < 3; axis++)
                {
                    double first = Coordinate(ends[0], axis);
                    double second = Coordinate(ends[ends.Length - 1], axis);
                    _low[(at * 3) + axis] = Math.Min(first, second);
                    _high[(at * 3) + axis] = Math.Max(first, second);
                }
            }

            _root = Build(order, 0, order.Length);
        }

        /// <summary>ボーンの根元の点を比べる表。</summary>
        public static NearestBones ByPosition(IList<IPXBone> bones)
        {
            if (bones == null)
            {
                throw new ArgumentNullException(nameof(bones));
            }

            return new NearestBones(
                bones,
                bone => new[] { bone.Position },
                null);
        }

        /// <summary>ボーンの根元から表示先までの線分を比べる表。</summary>
        public static NearestBones BySegment(IList<IPXBone> bones, Func<IPXBone, V3> tip)
        {
            if (bones == null)
            {
                throw new ArgumentNullException(nameof(bones));
            }

            if (tip == null)
            {
                throw new ArgumentNullException(nameof(tip));
            }

            V3[] tips = new V3[bones.Count];
            for (int at = 0; at < bones.Count; at++)
            {
                tips[at] = tip(bones[at]);
            }

            return new NearestBones(
                bones,
                bone => new[] { bone.Position, tip(bone) },
                (point, at) => Vectors.DistanceToSegment(point, bones[at].Position, tips[at]));
        }

        /// <summary>点に最も近いボーン。ボーンが無ければ null。</summary>
        public IPXBone Closest(V3 point)
        {
            if (_root == null)
            {
                return null;
            }

            int best = -1;
            float shortest = 0f;
            Stack<Node> pending = new Stack<Node>();
            pending.Push(_root);
            while (pending.Count != 0)
            {
                Node node = pending.Pop();
                if (best >= 0 && Bound(point, node.Low, node.High) - Slack * (1d + shortest) > shortest)
                {
                    continue;
                }

                if (node.Items != null)
                {
                    foreach (int at in node.Items)
                    {
                        float apart = Distance(point, at);
                        if (best < 0 || apart < shortest || (apart == shortest && at < best))
                        {
                            best = at;
                            shortest = apart;
                        }
                    }

                    continue;
                }

                double left = Bound(point, node.Left.Low, node.Left.High);
                double right = Bound(point, node.Right.Low, node.Right.High);
                pending.Push(left <= right ? node.Right : node.Left);
                pending.Push(left <= right ? node.Left : node.Right);
            }

            return _bones[best];
        }

        private float Distance(V3 point, int at)
        {
            return _distance == null
                ? Vectors.Distance(point, _bones[at].Position)
                : _distance(point, at);
        }

        private Node Build(int[] order, int start, int end)
        {
            if (end <= start)
            {
                return null;
            }

            double[] low = { double.MaxValue, double.MaxValue, double.MaxValue };
            double[] high = { double.MinValue, double.MinValue, double.MinValue };
            for (int at = start; at < end; at++)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    low[axis] = Math.Min(low[axis], _low[(order[at] * 3) + axis]);
                    high[axis] = Math.Max(high[axis], _high[(order[at] * 3) + axis]);
                }
            }

            if (end - start <= LeafSize)
            {
                int[] items = new int[end - start];
                Array.Copy(order, start, items, 0, items.Length);

                return new Node(low, high, items, null, null);
            }

            int widest = 0;
            for (int axis = 1; axis < 3; axis++)
            {
                if (high[axis] - low[axis] > high[widest] - low[widest])
                {
                    widest = axis;
                }
            }

            Array.Sort(
                order,
                start,
                end - start,
                Comparer<int>.Create((a, b) => Middle(a, widest).CompareTo(Middle(b, widest))));
            int half = start + ((end - start) / 2);

            return new Node(low, high, null, Build(order, start, half), Build(order, half, end));
        }

        private double Middle(int at, int axis)
        {
            return (_low[(at * 3) + axis] + _high[(at * 3) + axis]) / 2d;
        }

        /// <summary>点から箱までの距離。箱の中なら0。</summary>
        private static double Bound(V3 point, double[] low, double[] high)
        {
            double sum = 0d;
            for (int axis = 0; axis < 3; axis++)
            {
                double at = Coordinate(point, axis);
                double gap = at < low[axis] ? low[axis] - at : (at > high[axis] ? at - high[axis] : 0d);
                sum += gap * gap;
            }

            return Math.Sqrt(sum);
        }

        private static double Coordinate(V3 point, int axis)
        {
            return axis == 0 ? point.X : (axis == 1 ? point.Y : point.Z);
        }

        private sealed class Node
        {
            public Node(double[] low, double[] high, int[] items, Node left, Node right)
            {
                Low = low;
                High = high;
                Items = items;
                Left = left;
                Right = right;
            }

            public double[] Low { get; }

            public double[] High { get; }

            /// <summary>葉が持つボーンの位置。枝では null。</summary>
            public int[] Items { get; }

            public Node Left { get; }

            public Node Right { get; }
        }
    }
}
