using System;
using System.Collections.Generic;
using Vec = PmxEditorMcp.SurfaceGeometry.Vec;

namespace PmxEditorMcp
{
    internal sealed class LateralScaling
    {
        private readonly Vec _from;

        private readonly Vec _axis;

        private readonly double _axisSquared;

        private readonly Vec _across;

        private readonly IList<Knot> _knots;

        private readonly int _side;

        private LateralScaling(
            Vec from,
            Vec axis,
            Vec across,
            IList<Knot> knots,
            int side)
        {
            _from = from;
            _axis = axis;
            _axisSquared = axis.Dot(axis);
            _across = across;
            _knots = knots;
            _side = side;
        }

        internal struct Knot
        {
            public Knot(double position, double scale, double center)
            {
                Position = position;
                Scale = scale;
                Center = center;
            }

            public double Position { get; private set; }

            public double Scale { get; private set; }

            public double Center { get; private set; }
        }

        /// <summary>
        /// 軸の両端と横方向から作る。両端が同じ点のとき、または横方向が軸と平行(零ベクトルを含む)のときは作れない。
        /// 横方向は軸に直交する成分を取って正規化する。
        /// </summary>
        public static bool TryCreate(
            Vec from,
            Vec to,
            Vec across,
            IList<Knot> knots,
            int side,
            out LateralScaling made,
            out string refused)
        {
            made = null;
            AxisFrame frame;
            if (!AxisFrame.TryCreate(from, to, across, out frame, out refused))
            {
                return false;
            }

            made = new LateralScaling(frame.From, frame.Axis, frame.Across, knots, side);

            return true;
        }

        public Vec Scaled(Vec at)
        {
            Vec gap = at - _from;
            double along = gap.Dot(_axis) / _axisSquared;
            Vec off = gap - (_axis * along);
            double wide = off.Dot(_across);
            double scale;
            double center;
            Interpolate(along, out scale, out center);
            double reach = wide - center;
            if ((_side > 0 && !(reach > 0d)) || (_side < 0 && !(reach < 0d)))
            {
                return at;
            }

            return at + (_across * (reach * (scale - 1d)));
        }

        private void Interpolate(double along, out double scale, out double center)
        {
            if (along <= _knots[0].Position)
            {
                scale = _knots[0].Scale;
                center = _knots[0].Center;

                return;
            }

            for (int next = 1; next < _knots.Count; next++)
            {
                if (along <= _knots[next].Position)
                {
                    Knot before = _knots[next - 1];
                    Knot after = _knots[next];
                    double share = (along - before.Position) / (after.Position - before.Position);
                    double eased = share * share * (3d - (2d * share));
                    scale = before.Scale + ((after.Scale - before.Scale) * eased);
                    center = before.Center + ((after.Center - before.Center) * eased);

                    return;
                }
            }

            scale = _knots[_knots.Count - 1].Scale;
            center = _knots[_knots.Count - 1].Center;
        }
    }
}
