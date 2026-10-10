using Vec = PmxEditorMcp.SurfaceGeometry.Vec;

namespace PmxEditorMcp
{
    internal sealed class AxisFrame
    {
        private const double Tiny = 1e-9;

        private readonly double _axisSquared;

        private AxisFrame(Vec from, Vec axis, Vec across, Vec up)
        {
            From = from;
            Axis = axis;
            _axisSquared = axis.Dot(axis);
            Across = across;
            Up = up;
        }

        public Vec From { get; }

        public Vec Axis { get; }

        public Vec Across { get; }

        public Vec Up { get; }

        public static bool TryCreate(
            Vec from, Vec to, Vec across, out AxisFrame made, out string refused)
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

            Vec side = perpendicular * (1d / width);
            made = new AxisFrame(from, axis, side, side.Cross(unit));

            return true;
        }

        public double Along(Vec at)
        {
            return (at - From).Dot(Axis) / _axisSquared;
        }
    }
}
