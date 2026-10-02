using System;
using System.Collections.Generic;
using Vec = PmxEditorMcp.SurfaceGeometry.Vec;

namespace PmxEditorMcp
{
    internal sealed class LateralScaling
    {
        private const double Tiny = 1e-9;

        private readonly Vec _from;

        private readonly Vec _axis;

        private readonly double _axisSquared;

        private readonly Vec _across;

        private readonly IList<KeyValuePair<double, double>> _knots;

        private LateralScaling(
            Vec from,
            Vec axis,
            Vec across,
            IList<KeyValuePair<double, double>> knots)
        {
            _from = from;
            _axis = axis;
            _axisSquared = axis.Dot(axis);
            _across = across;
            _knots = knots;
        }

        /// <summary>
        /// 軸の両端と横方向から作る。両端が同じ点のとき、または横方向が軸と平行(零ベクトルを含む)のときは作れない。
        /// 横方向は軸に直交する成分を取って正規化する。
        /// </summary>
        public static bool TryCreate(
            Vec from,
            Vec to,
            Vec across,
            IList<KeyValuePair<double, double>> knots,
            out LateralScaling made,
            out string refused)
        {
            made = null;
            refused = null;
            Vec axis = to - from;
            double length = axis.Length;
            if (!(length > 0d))
            {
                refused = "from と to は、異なる点でなければならない。";

                return false;
            }

            Vec unit = axis * (1d / length);
            Vec perpendicular = across - (unit * across.Dot(unit));
            double width = perpendicular.Length;
            if (!(width > Tiny * across.Length))
            {
                refused = "across は、from から to への軸と平行でない向きでなければならない。";

                return false;
            }

            made = new LateralScaling(from, axis, perpendicular * (1d / width), knots);

            return true;
        }

        /// <summary>
        /// 軸の線から <c>across</c> の成分だけに倍率を掛けた位置。倍率は軸への射影の比で決まる。
        /// </summary>
        public Vec Scaled(Vec at)
        {
            Vec gap = at - _from;
            double along = gap.Dot(_axis) / _axisSquared;
            Vec off = gap - (_axis * along);
            double wide = off.Dot(_across);

            return at + (_across * (wide * (ScaleAt(along) - 1d)));
        }

        private double ScaleAt(double along)
        {
            if (along <= _knots[0].Key)
            {
                return _knots[0].Value;
            }

            for (int next = 1; next < _knots.Count; next++)
            {
                if (along <= _knots[next].Key)
                {
                    double share = (along - _knots[next - 1].Key) / (_knots[next].Key - _knots[next - 1].Key);
                    double eased = share * share * (3d - (2d * share));

                    return _knots[next - 1].Value + ((_knots[next].Value - _knots[next - 1].Value) * eased);
                }
            }

            return _knots[_knots.Count - 1].Value;
        }
    }
}
