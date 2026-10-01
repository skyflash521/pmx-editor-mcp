using System.Collections.Generic;
using System.Globalization;

namespace PmxEditorMcp
{
    public static class PositionInput
    {
        public const int Unbounded = int.MaxValue;

        public const string ListCountName = "リストの件数";

        public static bool TryRead(
            object given, string shape, out int at, out string code, out string message)
        {
            if (ValueInput.TryIndex(given, out at))
            {
                code = null;
                message = null;

                return true;
            }

            at = 0;
            code = ToolEnvelope.InvalidArgument;
            message = shape;

            return false;
        }

        public static bool TryWithin(
            int at,
            int ceiling,
            string name,
            out string code,
            out string message,
            string countName = null)
        {
            code = null;
            message = null;
            if (at >= 0 && at < ceiling)
            {
                return true;
            }

            code = ToolEnvelope.IndexOutOfRange;
            message = countName == null
                ? name + " が並びの外を指している: " + Spelled(at)
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} の位置が範囲の外にある: {1}({2}は {3})",
                    name,
                    at,
                    countName,
                    ceiling);

            return false;
        }

        public static bool TryOne(
            object given,
            string name,
            string shape,
            int ceiling,
            out int at,
            out string code,
            out string message)
        {
            return TryRead(given, shape, out at, out code, out message)
                && TryWithin(at, ceiling, name, out code, out message);
        }

        public static bool TryMany(
            object given,
            string name,
            string shape,
            int ceiling,
            bool allowEmpty,
            out List<int> positions,
            out string code,
            out string message)
        {
            positions = new List<int>();
            object[] items = given as object[];
            if (items == null || (!allowEmpty && items.Length == 0))
            {
                code = ToolEnvelope.InvalidArgument;
                message = shape;

                return false;
            }

            foreach (object item in items)
            {
                int at;
                if (!TryOne(item, name, shape, ceiling, out at, out code, out message))
                {
                    positions = new List<int>();

                    return false;
                }

                positions.Add(at);
            }

            code = null;
            message = null;

            return true;
        }

        public static bool TryRange(
            int start,
            int count,
            int ceiling,
            string name,
            out string code,
            out string message)
        {
            code = null;
            message = null;
            if (start < 0)
            {
                code = ToolEnvelope.IndexOutOfRange;
                message = string.Format(
                    CultureInfo.InvariantCulture, "{0} の start が0を下回っている: {1}", name, start);

                return false;
            }

            if (count < 1)
            {
                code = ToolEnvelope.InvalidArgument;
                message = string.Format(
                    CultureInfo.InvariantCulture, "{0} の count が1を下回っている: {1}", name, count);

                return false;
            }

            if ((long)start + count - 1 >= ceiling)
            {
                code = ToolEnvelope.IndexOutOfRange;
                message = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} が範囲の外まで及んでいる: {1} から {2} 件({3}は {4})",
                    name,
                    start,
                    count,
                    ListCountName,
                    ceiling);

                return false;
            }

            return true;
        }

        private static string Spelled(int number)
        {
            return number.ToString(CultureInfo.InvariantCulture);
        }
    }
}
