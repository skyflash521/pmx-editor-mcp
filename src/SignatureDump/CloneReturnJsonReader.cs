using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 戻り値の綴りが object の行について、エディタがその呼び出しで実際に返すものの型を記した表を
    /// JSONから読み取る。
    /// </summary>
    public static class CloneReturnJsonReader
    {
        private const string RowsName = "rows";

        private const string KeyName = "signatureKey";

        private const string ReturnsName = "returns";

        private const string BasisName = "basis";

        private const string FileName = "clone-returns.json";

        private static readonly JsonForm Form = JsonForm.Object(
            JsonForm.Member(
                RowsName,
                JsonForm.Array(
                    JsonForm.Object(
                        JsonForm.Member(KeyName, JsonForm.Text()),
                        JsonForm.Member(ReturnsName, JsonForm.Text()),
                        JsonForm.Member(BasisName, JsonForm.Text())),
                    allowEmpty: true)));

        /// <summary>
        /// 表のパス。表は、<paramref name="ledgerPath"/> の能力台帳と同じ観測データの置き場に在る。
        /// </summary>
        public static string Beside(string ledgerPath)
        {
            if (ledgerPath == null)
            {
                throw new ArgumentNullException(nameof(ledgerPath));
            }

            return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(ledgerPath)), FileName);
        }

        /// <summary>行キーから、返すものの型へ。形が違えば <see cref="FormatException"/>。</summary>
        public static IDictionary<string, string> Read(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            IDictionary<string, object> root;
            try
            {
                root = (IDictionary<string, object>)Form.Read(json);
            }
            catch (FormatException exception)
            {
                throw new FormatException("エディタが返す型の表: " + exception.Message, exception);
            }

            Dictionary<string, string> returns = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (IDictionary<string, object> row in
                ((object[])root[RowsName]).Cast<IDictionary<string, object>>())
            {
                string key = (string)row[KeyName];
                if (returns.ContainsKey(key))
                {
                    throw new FormatException("エディタが返す型の表に同じ行が二度在る: " + key);
                }

                returns.Add(key, (string)row[ReturnsName]);
            }

            return returns;
        }
    }
}
