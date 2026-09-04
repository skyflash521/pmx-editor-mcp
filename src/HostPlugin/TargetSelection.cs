using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PmxEditorMcp
{
    /// <summary>要素の集合の指し方。</summary>
    [Flags]
    public enum TargetForm
    {
        /// <summary>どれも指していない。</summary>
        None = 0,

        /// <summary>位置の整数の配列。</summary>
        Indices = 1,

        /// <summary>始まりの位置と件数の組。</summary>
        Range = 2,

        /// <summary>対象のリストの全要素。</summary>
        All = 4,

        /// <summary>まだリストへ加えていない生成物のハンドルの配列。</summary>
        Handles = 8,
    }

    /// <summary>
    /// 1つの集合を指す項目の名前。指す対象ごとに名前を分けるので、解く集合に合わせてこれを渡す。
    /// </summary>
    public sealed class TargetNames
    {
        public TargetNames(string indices, string range, string all, string handles)
        {
            if (string.IsNullOrWhiteSpace(indices))
            {
                throw new ArgumentException("空にできない。", nameof(indices));
            }

            if (string.IsNullOrWhiteSpace(range))
            {
                throw new ArgumentException("空にできない。", nameof(range));
            }

            if (string.IsNullOrWhiteSpace(all))
            {
                throw new ArgumentException("空にできない。", nameof(all));
            }

            if (string.IsNullOrWhiteSpace(handles))
            {
                throw new ArgumentException("空にできない。", nameof(handles));
            }

            Indices = indices;
            Range = range;
            All = all;
            Handles = handles;
        }

        /// <summary>対象そのものの集合を指す名前。</summary>
        public static TargetNames Element { get; } =
            new TargetNames("indices", "range", "all", "handles");

        /// <summary>親の集合を指す名前。</summary>
        public static TargetNames Parent { get; } =
            new TargetNames("parentIndices", "parentRange", "parentAll", "parentHandles");

        public string Indices { get; }

        public string Range { get; }

        public string All { get; }

        public string Handles { get; }
    }

    /// <summary>要求が持ってきた集合の指定。持たない形は null を渡す。</summary>
    public sealed class TargetRequest
    {
        public TargetRequest(
            IList<int> indices = null,
            int? rangeStart = null,
            int? rangeCount = null,
            bool? all = null,
            IList<long> handles = null)
        {
            Indices = indices;
            RangeStart = rangeStart;
            RangeCount = rangeCount;
            All = all;
            Handles = handles;
        }

        /// <summary>位置の配列。指定が無ければ null。</summary>
        public IList<int> Indices { get; }

        /// <summary>範囲の始まり。指定が無ければ null。</summary>
        public int? RangeStart { get; }

        /// <summary>範囲の件数。指定が無ければ null。</summary>
        public int? RangeCount { get; }

        /// <summary>全要素の指定。指定が無ければ null。</summary>
        public bool? All { get; }

        /// <summary>ハンドルの配列。指定が無ければ null。</summary>
        public IList<long> Handles { get; }
    }

    /// <summary>解決した集合。</summary>
    public sealed class ResolvedTargets
    {
        public ResolvedTargets(TargetForm form, IList<int> indices, IList<long> handles)
        {
            Form = form;
            Indices = indices;
            Handles = handles;
        }

        /// <summary>どの指し方で解決したか。</summary>
        public TargetForm Form { get; }

        /// <summary>位置で解決した対象。ハンドルで解決したときは null。</summary>
        public IList<int> Indices { get; }

        /// <summary>ハンドルで解決した対象。位置で解決したときは null。</summary>
        public IList<long> Handles { get; }

        /// <summary>対象の件数。</summary>
        public int Count
        {
            get { return Indices != null ? Indices.Count : Handles.Count; }
        }
    }

    /// <summary>
    /// 要素の集合の指定を、順序の付いた対象へ解く。解けなかったときは、返すエラーコードと説明を
    /// 添えて断る。解決をここに集めるのは、どのツールでも同じ順序と同じ断り方にするためである。
    /// </summary>
    public static class TargetSelection
    {
        /// <summary>
        /// <paramref name="request"/> を解く。<paramref name="allowed"/> は、そのツールが
        /// 受け付ける指し方。<paramref name="listCount"/> は対象のリストの件数で、
        /// <paramref name="isUsableHandle"/> はハンドルが使えるかを答えるもの。
        /// <paramref name="names"/> は解く集合を指す項目の名前で、説明はこの名前で書く。
        /// </summary>
        public static bool TryResolve(
            TargetRequest request,
            TargetForm allowed,
            int listCount,
            Func<long, bool> isUsableHandle,
            out ResolvedTargets resolved,
            out string code,
            out string message,
            TargetNames names)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (isUsableHandle == null)
            {
                throw new ArgumentNullException(nameof(isUsableHandle));
            }

            if (listCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(listCount), listCount, "0以上でなければならない。");
            }

            if (names == null)
            {
                throw new ArgumentNullException(nameof(names));
            }

            resolved = null;
            code = null;
            message = null;

            TargetForm given = Given(request);
            if (!TryTakeSoleForm(given, allowed, names, out TargetForm form, out code, out message))
            {
                return false;
            }

            switch (form)
            {
                case TargetForm.Indices:
                    return TryResolveIndices(
                        request.Indices, listCount, names, out resolved, out code, out message);

                case TargetForm.Range:
                    return TryResolveRange(
                        request.RangeStart, request.RangeCount, listCount, names,
                        out resolved, out code, out message);

                case TargetForm.All:
                    return TryResolveAll(
                        request.All.Value, listCount, names, out resolved, out code, out message);

                default:
                    return TryResolveHandles(
                        request.Handles, isUsableHandle, names, out resolved, out code, out message);
            }
        }

        /// <summary>要求が持ってきた指し方。2つ以上あればその全部が立つ。</summary>
        private static TargetForm Given(TargetRequest request)
        {
            TargetForm given = TargetForm.None;
            if (request.Indices != null)
            {
                given |= TargetForm.Indices;
            }

            if (request.RangeStart.HasValue || request.RangeCount.HasValue)
            {
                given |= TargetForm.Range;
            }

            if (request.All.HasValue)
            {
                given |= TargetForm.All;
            }

            if (request.Handles != null)
            {
                given |= TargetForm.Handles;
            }

            return given;
        }

        /// <summary>指し方がちょうど1つで、そのツールが受け付けるものかを見る。</summary>
        private static bool TryTakeSoleForm(
            TargetForm given,
            TargetForm allowed,
            TargetNames names,
            out TargetForm form,
            out string code,
            out string message)
        {
            form = TargetForm.None;
            code = ToolEnvelope.InvalidArgument;
            message = null;

            TargetForm[] present = All().Where(f => (given & f) == f).ToArray();
            if (present.Length == 0)
            {
                message = "対象の指定が無い。" + AllowedText(allowed, names) + "のどれか1つを指定する。";
                return false;
            }

            if (present.Length > 1)
            {
                message = "対象の指定が重なっている: "
                    + string.Join("・", present.Select(f => Name(f, names)).ToArray())
                    + "。どれか1つだけを指定する。";
                return false;
            }

            if ((allowed & present[0]) != present[0])
            {
                message = Name(present[0], names) + " はこのツールでは指定できない。"
                    + AllowedText(allowed, names) + "のどれか1つを指定する。";
                return false;
            }

            form = present[0];
            code = null;

            return true;
        }

        private static bool TryResolveIndices(
            IList<int> indices,
            int listCount,
            TargetNames names,
            out ResolvedTargets resolved,
            out string code,
            out string message)
        {
            resolved = null;
            code = null;
            message = null;

            if (indices.Count == 0)
            {
                code = ToolEnvelope.InvalidArgument;
                message = names.Indices + " が空である。空だと対象が決まらない。";
                return false;
            }

            HashSet<int> seen = new HashSet<int>();
            foreach (int index in indices)
            {
                if (!seen.Add(index))
                {
                    code = ToolEnvelope.InvalidArgument;
                    message = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} が同じ位置を二度指している: {1}",
                        names.Indices,
                        index);
                    return false;
                }
            }

            foreach (int index in indices)
            {
                if (index < 0 || index >= listCount)
                {
                    code = ToolEnvelope.IndexOutOfRange;
                    message = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} の位置が範囲の外にある: {1}(リストの件数は {2})",
                        names.Indices,
                        index,
                        listCount);
                    return false;
                }
            }

            resolved = new ResolvedTargets(TargetForm.Indices, indices.ToArray(), null);

            return true;
        }

        private static bool TryResolveRange(
            int? start,
            int? count,
            int listCount,
            TargetNames names,
            out ResolvedTargets resolved,
            out string code,
            out string message)
        {
            resolved = null;
            code = null;
            message = null;

            if (!start.HasValue || !count.HasValue)
            {
                code = ToolEnvelope.InvalidArgument;
                message = names.Range + " は start と count の両方を持たなければならない。";
                return false;
            }

            if (start.Value < 0)
            {
                code = ToolEnvelope.InvalidArgument;
                message = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} の start が0を下回っている: {1}",
                    names.Range,
                    start.Value);
                return false;
            }

            if (count.Value < 1)
            {
                code = ToolEnvelope.InvalidArgument;
                message = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} の count が1を下回っている: {1}",
                    names.Range,
                    count.Value);
                return false;
            }

            long last = (long)start.Value + count.Value - 1;
            if (last >= listCount)
            {
                code = ToolEnvelope.IndexOutOfRange;
                message = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} が範囲の外まで及んでいる: {1} から {2} 件(リストの件数は {3})",
                    names.Range,
                    start.Value,
                    count.Value,
                    listCount);
                return false;
            }

            resolved = new ResolvedTargets(
                TargetForm.Range,
                Enumerable.Range(start.Value, count.Value).ToArray(),
                null);

            return true;
        }

        private static bool TryResolveAll(
            bool all,
            int listCount,
            TargetNames names,
            out ResolvedTargets resolved,
            out string code,
            out string message)
        {
            resolved = null;
            code = null;
            message = null;

            if (!all)
            {
                code = ToolEnvelope.InvalidArgument;
                message = names.All + " は真でなければならない。全要素を指さないなら、この指定を持たせない。";
                return false;
            }

            resolved = new ResolvedTargets(TargetForm.All, Enumerable.Range(0, listCount).ToArray(), null);

            return true;
        }

        private static bool TryResolveHandles(
            IList<long> handles,
            Func<long, bool> isUsableHandle,
            TargetNames names,
            out ResolvedTargets resolved,
            out string code,
            out string message)
        {
            resolved = null;
            code = null;
            message = null;

            if (handles.Count == 0)
            {
                code = ToolEnvelope.InvalidArgument;
                message = names.Handles + " が空である。空だと対象が決まらない。";
                return false;
            }

            HashSet<long> seen = new HashSet<long>();
            foreach (long handle in handles)
            {
                if (!seen.Add(handle))
                {
                    code = ToolEnvelope.InvalidArgument;
                    message = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} が同じハンドルを二度指している: {1}",
                        names.Handles,
                        handle);
                    return false;
                }
            }

            foreach (long handle in handles)
            {
                if (!isUsableHandle(handle))
                {
                    code = ToolEnvelope.InvalidHandle;
                    message = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} に使えないハンドルがある: {1}",
                        names.Handles,
                        handle);
                    return false;
                }
            }

            resolved = new ResolvedTargets(TargetForm.Handles, null, handles.ToArray());

            return true;
        }

        private static IEnumerable<TargetForm> All()
        {
            yield return TargetForm.Indices;
            yield return TargetForm.Range;
            yield return TargetForm.All;
            yield return TargetForm.Handles;
        }

        private static string AllowedText(TargetForm allowed, TargetNames names)
        {
            TargetForm[] forms = All().Where(f => (allowed & f) == f).ToArray();

            return forms.Length == 0
                ? "(受け付ける指定が無い)"
                : string.Join("・", forms.Select(f => Name(f, names)).ToArray());
        }

        private static string Name(TargetForm form, TargetNames names)
        {
            switch (form)
            {
                case TargetForm.Indices:
                    return names.Indices;

                case TargetForm.Range:
                    return names.Range;

                case TargetForm.All:
                    return names.All;

                case TargetForm.Handles:
                    return names.Handles;

                default:
                    throw new ArgumentOutOfRangeException(nameof(form), form, "知らない指し方。");
            }
        }
    }
}
