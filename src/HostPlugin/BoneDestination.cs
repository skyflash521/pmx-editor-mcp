using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    public sealed class BoneDestination
    {
        private const string ToOffsetRow = "PEPlugin.Pmx.IPXBone.ToOffset()";

        private const string ToBoneRow = "PEPlugin.Pmx.IPXBone.ToBone()";

        private readonly List<string> _stuck = new List<string>();

        public void Note(
            IEnumerable<string> written, IEnumerable<string> deferredRows, object item, string label)
        {
            if (written == null)
            {
                throw new ArgumentNullException(nameof(written));
            }

            if (deferredRows == null)
            {
                throw new ArgumentNullException(nameof(deferredRows));
            }

            IPXBone bone = item as IPXBone;
            if (bone == null
                || !written.Any(row => string.Equals(row, ToOffsetRow, StringComparison.Ordinal)
                    || string.Equals(row, ToBoneRow, StringComparison.Ordinal))
                || (bone.ToBone == null && !deferredRows.Contains(ToBoneRow, StringComparer.Ordinal))
                || IsZero(bone.ToOffset))
            {
                return;
            }

            _stuck.Add(label + "「" + bone.Name + "」");
        }

        public IList<string> Notices()
        {
            return _stuck.Count == 0
                ? new string[0]
                : new[]
                {
                    "次のボーンは表示先の座標(toOffset)とボーン(toBone)の両方が有効で、効くのは片方だけである。"
                        + "座標で指すなら toBone に null を、ボーンで指すなら toOffset に [0,0,0] を渡す: "
                        + string.Join("、", _stuck),
                };
        }

        private static bool IsZero(V3 offset)
        {
            return (object)offset == null || (offset.X == 0f && offset.Y == 0f && offset.Z == 0f);
        }
    }
}
