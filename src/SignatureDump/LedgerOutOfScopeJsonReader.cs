using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>明示的な対象外一覧の正本をJSONから読み取る。</summary>
    public static class LedgerOutOfScopeJsonReader
    {
        private const string TypesName = "types";

        private const string SignaturesName = "signatures";

        private const string NameName = "name";

        private const string KeyName = "key";

        private static readonly JsonForm Form = JsonForm.Object(
            JsonForm.Member(
                TypesName,
                JsonForm.Array(
                    JsonForm.Object(JsonForm.Member(NameName, JsonForm.Text())), allowEmpty: true)),
            JsonForm.Member(
                SignaturesName,
                JsonForm.Array(
                    JsonForm.Object(JsonForm.Member(KeyName, JsonForm.Text())), allowEmpty: true)));

        /// <summary>形が違えば <see cref="FormatException"/>。</summary>
        public static LedgerOutOfScopeRecord Read(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            IDictionary<string, object> root = (IDictionary<string, object>)Form.Read(json);
            try
            {
                return new LedgerOutOfScopeRecord(
                    Named(root[TypesName], NameName)
                        .Select(name => new OutOfScopeTypeEntry(name))
                        .ToList(),
                    Named(root[SignaturesName], KeyName)
                        .Select(key => new OutOfScopeSignatureEntry(key))
                        .ToList());
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(exception.Message, exception);
            }
        }

        private static IEnumerable<string> Named(object items, string name)
        {
            return ((object[])items)
                .Cast<IDictionary<string, object>>()
                .Select(item => (string)item[name]);
        }
    }
}
