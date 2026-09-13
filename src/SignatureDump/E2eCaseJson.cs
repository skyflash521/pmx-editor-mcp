using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>組み立てた検査を、実行器が読むJSONとして綴る。</summary>
    public static class E2eCaseJson
    {
        public static string Compose(IEnumerable<E2eCase> cases)
        {
            if (cases == null)
            {
                throw new ArgumentNullException(nameof(cases));
            }

            StringBuilder text = new StringBuilder("{\n  \"cases\": [\n");
            bool first = true;
            foreach (E2eCase one in cases)
            {
                if (!first)
                {
                    text.Append(",\n");
                }

                text.Append("    ").Append(Compose(one));
                first = false;
            }

            return text.Append("\n  ]\n}\n").ToString();
        }

        private static string Compose(E2eCase one)
        {
            JsonObjectText written = new JsonObjectText()
                .AddText("rowKey", one.RowKey)
                .AddText("editKind", one.EditKind)
                .AddText("connectionPath", one.ConnectionPath)
                .AddText("tool", one.Tool)
                .AddText("purpose", one.Purpose)
                .Add("arguments", Arguments(one.Arguments))
                .AddText("expect", Spelling(one.Expectation));

            if (one.Code != null)
            {
                written.AddText("code", one.Code);
            }

            if (one.View != null)
            {
                written.AddText("view", one.View);
            }

            if (one.Produces != null)
            {
                written.AddText("produces", one.Produces);
            }

            if (one.Borrowed != null)
            {
                written.Add("borrowed", Borrowed(one.Borrowed));
            }

            return written.Text;
        }

        private static string Spelling(E2eExpectation expectation)
        {
            switch (expectation)
            {
                case E2eExpectation.Success:
                    return "success";

                case E2eExpectation.Refusal:
                    return "refusal";

                case E2eExpectation.Dispatched:
                    return "dispatched";

                case E2eExpectation.ViewImage:
                    return "viewImage";

                default:
                    throw new ArgumentOutOfRangeException(nameof(expectation));
            }
        }

        private static string Borrowed(IDictionary<string, string> borrowed)
        {
            JsonObjectText written = new JsonObjectText();
            foreach (KeyValuePair<string, string> one in borrowed
                .OrderBy(b => b.Key, StringComparer.Ordinal))
            {
                written.AddText(one.Key, one.Value);
            }

            return written.Text;
        }

        private static string Arguments(IDictionary<string, object> arguments)
        {
            JsonObjectText written = new JsonObjectText();
            foreach (KeyValuePair<string, object> argument in arguments
                .OrderBy(a => a.Key, StringComparer.Ordinal))
            {
                written.Add(argument.Key, Value(argument.Value));
            }

            return written.Text;
        }

        private static string Value(object value)
        {
            if (value == null)
            {
                return "null";
            }

            if (value is bool)
            {
                return (bool)value ? "true" : "false";
            }

            if (value is string)
            {
                return JsonText.Quote((string)value);
            }

            object[] items = value as object[];
            if (items != null)
            {
                return JsonWriter.Array(items.Select(Value));
            }

            IDictionary<string, object> members = value as IDictionary<string, object>;
            if (members != null)
            {
                return Arguments(members);
            }

            return Convert.ToDouble(value, CultureInfo.InvariantCulture)
                .ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
