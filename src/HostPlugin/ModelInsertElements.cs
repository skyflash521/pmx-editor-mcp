using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp
{
    /// <summary>
    /// 新しい要素か、指した要素の複製を、並びの指した位置へ入れるツール。位置を省くと末尾へ足す。
    /// 複製は指した要素を写したもので、写した先が指す参照は元と同じ要素を指す。
    /// </summary>
    public static class ModelInsertElements
    {
        /// <summary>このツールの名前。</summary>
        public const string ToolName = "model_insert_elements";

        /// <summary>どちらを入れるかを受け取る入力の名前。</summary>
        public const string OperationName = "operation";

        /// <summary>新しい要素を入れる。</summary>
        public const string New = "new";

        /// <summary>指した要素の複製を入れる。</summary>
        public const string Clone = "clone";

        /// <summary>入れる位置を受け取る入力の名前。</summary>
        public const string AtName = "at";

        /// <summary>新しい要素をいくつ入れるかを受け取る入力の名前。</summary>
        public const string CountName = "count";

        /// <summary>入った位置を返す項目の名前。</summary>
        public const string IndicesName = "indices";

        /// <summary>受け取れる操作。スキーマが並べる順。</summary>
        public static IList<string> Operations
        {
            get { return new[] { New, Clone }; }
        }

        /// <summary>ツールを表へ足す。<paramref name="builder"/> は新しい要素を作る相手を返す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedEdit edit, Func<object> builder)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (edit == null)
            {
                throw new ArgumentNullException(nameof(edit));
            }

            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            List<string> known = new List<string>(ElementScope.Names)
            {
                TargetNames.Element.Indices,
                TargetNames.Element.Range,
                TargetNames.Element.All,
                OperationName,
                AtName,
                CountName,
            };
            methods.Add(
                ToolName, edit.Method(known, (context, pmx) => Run(context, pmx, builder)));
        }

        private static ComposedEditResult Run(
            McpMethodContext context, object pmx, Func<object> builder)
        {
            ElementKind kind;
            IList<object> owners;
            string code;
            string message;
            string operation;
            if (!ElementScope.TryTake(context, pmx, out kind, out owners, out code, out message)
                || !TryOperation(context, out operation, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            bool copying = string.Equals(operation, Clone, StringComparison.Ordinal);
            int count;
            int? at;
            if (!TryCount(context, copying, out count, out code, out message)
                || !TryAt(context, out at, out code, out message))
            {
                return ComposedEditResult.Refuse(code, message);
            }

            if (!copying && kind.Owner != null)
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidArgument,
                    kind.Name + " はほかの要素を指して初めて意味を持つ種類なので、"
                        + New + " では作れない。" + Clone + " で写して入れる。");
            }

            if (!copying && context.Params.Keys.Any(Points))
            {
                return ComposedEditResult.Refuse(
                    ToolEnvelope.InvalidArgument,
                    OperationName + " が " + New + " のときは、写す元を指さない。");
            }

            List<object> landed = new List<object>();
            foreach (object owner in owners)
            {
                IList<object> made;
                if (!TryMade(context, kind, owner, builder, copying, count, out made, out code, out message))
                {
                    return ComposedEditResult.Refuse(code, message);
                }

                IList<object> items = kind.Items(owner);
                int put = at ?? items.Count;
                if (put < 0 || put > items.Count)
                {
                    return ComposedEditResult.Refuse(
                        ToolEnvelope.IndexOutOfRange, AtName + " が並びの外を指している: " + put);
                }

                List<object> written = new List<object>(items);
                written.InsertRange(put, made);
                kind.Replace(owner, written);
                for (int step = 0; step < made.Count; step++)
                {
                    landed.Add(put + step);
                }
            }

            return ComposedEditResult.Complete(
                new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    { IndicesName, landed.ToArray() },
                });
        }

        private static bool TryMade(
            McpMethodContext context,
            ElementKind kind,
            object owner,
            Func<object> builder,
            bool copying,
            int count,
            out IList<object> made,
            out string code,
            out string message)
        {
            made = null;
            code = null;
            message = null;
            if (!copying)
            {
                List<object> built = new List<object>();
                for (int step = 0; step < count; step++)
                {
                    object item = kind.Create(builder(), owner);
                    if (item == null)
                    {
                        code = ToolEnvelope.NotApplicable;
                        message = kind.Name + " の新しい要素をこの相手のもとでは作れない。";

                        return false;
                    }

                    built.Add(item);
                }

                made = built;

                return true;
            }

            IList<int> chosen;
            if (!ElementScope.TryPositions(context, kind, owner, out chosen, out code, out message))
            {
                return false;
            }

            IList<object> items = kind.Items(owner);
            made = chosen.Select(at => kind.CloneOf(items[at])).ToList();

            return true;
        }

        private static bool Points(string name)
        {
            return string.Equals(name, TargetNames.Element.Indices, StringComparison.Ordinal)
                || string.Equals(name, TargetNames.Element.Range, StringComparison.Ordinal)
                || string.Equals(name, TargetNames.Element.All, StringComparison.Ordinal);
        }

        private static bool TryOperation(
            McpMethodContext context, out string operation, out string code, out string message)
        {
            code = ToolEnvelope.InvalidArgument;
            message = null;
            object given;
            context.Params.TryGetValue(OperationName, out given);
            operation = given as string;
            if (operation == null || !Operations.Contains(operation, StringComparer.Ordinal))
            {
                message = OperationName + " は次のどれかでなければならない: "
                    + string.Join("・", Operations.ToArray());

                return false;
            }

            code = null;

            return true;
        }

        private static bool TryCount(
            McpMethodContext context, bool copying, out int count, out string code, out string message)
        {
            count = 1;
            code = ToolEnvelope.InvalidArgument;
            message = null;
            object given;
            if (!context.Params.TryGetValue(CountName, out given))
            {
                code = null;

                return true;
            }

            if (copying)
            {
                message = CountName + " を渡せるのは " + OperationName + " が " + New
                    + " のときだけである。";

                return false;
            }

            if (!ValueInput.TryIndex(given, out count) || count < 1)
            {
                message = CountName + " は1以上の整数でなければならない。";

                return false;
            }

            code = null;

            return true;
        }

        private static bool TryAt(
            McpMethodContext context, out int? at, out string code, out string message)
        {
            at = null;
            code = null;
            message = null;
            object given;
            if (!context.Params.TryGetValue(AtName, out given))
            {
                return true;
            }

            int taken;
            if (!ValueInput.TryIndex(given, out taken))
            {
                code = ToolEnvelope.InvalidArgument;
                message = AtName + " は整数でなければならない。";

                return false;
            }

            at = taken;

            return true;
        }
    }
}
