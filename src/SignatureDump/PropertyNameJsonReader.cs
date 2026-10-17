using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>日本語名の正本をJSONから読み取る。</summary>
    public static class PropertyNameJsonReader
    {
        private const string PropertyNamesName = "propertyNames";

        private const string DeclaringTypeName = "declaringType";

        private const string MemberNameName = "memberName";

        private const string JapaneseNameName = "japaneseName";

        private const string BasisName = "basis";

        private const string OriginName = "origin";

        private const string KindName = "kind";

        private const string PathName = "path";

        private const string FirstLineName = "firstLine";

        private const string LastLineName = "lastLine";

        private const string DocumentSectionText = "documentSection";

        private const string MemberShapeText = "memberShape";

        private static readonly JsonForm Form = JsonForm.Object(
            JsonForm.Member(
                PropertyNamesName,
                JsonForm.Array(
                    JsonForm.Object(
                        JsonForm.Member(DeclaringTypeName, JsonForm.Text()),
                        JsonForm.Member(MemberNameName, JsonForm.Text()),
                        JsonForm.Member(JapaneseNameName, JsonForm.Text()),
                        JsonForm.Member(
                            BasisName,
                            JsonForm.Object(
                                JsonForm.Member(KindName, JsonForm.Text()),
                                JsonForm.Optional(PathName, JsonForm.Text()),
                                JsonForm.Optional(FirstLineName, JsonForm.Count()),
                                JsonForm.Optional(LastLineName, JsonForm.Count()))),
                        JsonForm.Member(OriginName, JsonForm.Text())),
                    allowEmpty: true)));

        /// <summary>
        /// 名前を起こした項目を書かれた順に返す。並びは宣言型・メンバー名の序数の昇順で、重複が
        /// 無いことを求める(<see cref="RoleTypeProperties"/> の並びと同じ定義)。形が違えば
        /// <see cref="FormatException"/>。
        /// </summary>
        public static IList<PropertyNameRecord> ReadPropertyNames(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            IDictionary<string, object> root = (IDictionary<string, object>)Form.Read(json);
            List<PropertyNameRecord> records = new List<PropertyNameRecord>();
            PropertyNameRecord previous = null;

            foreach (IDictionary<string, object> item in
                ((object[])root[PropertyNamesName]).Cast<IDictionary<string, object>>())
            {
                PropertyNameRecord record = ReadRecord(item);
                if (previous != null)
                {
                    int order = Order(previous, record);
                    if (order == 0)
                    {
                        throw new FormatException("同じ項目が二度現れる: " + record.Key);
                    }

                    if (order > 0)
                    {
                        throw new FormatException("序数の昇順で並んでいない: " + record.Key);
                    }
                }

                previous = record;
                records.Add(record);
            }

            return new ReadOnlyCollection<PropertyNameRecord>(records);
        }

        /// <summary>宣言型を先に比べ、同じときだけメンバー名を比べる。列挙側の並びと同じ定義。</summary>
        private static int Order(PropertyNameRecord left, PropertyNameRecord right)
        {
            int order = string.CompareOrdinal(left.DeclaringType, right.DeclaringType);

            return order != 0 ? order : string.CompareOrdinal(left.MemberName, right.MemberName);
        }

        private static PropertyNameRecord ReadRecord(IDictionary<string, object> members)
        {
            try
            {
                return new PropertyNameRecord(
                    (string)members[DeclaringTypeName],
                    (string)members[MemberNameName],
                    (string)members[JapaneseNameName],
                    Basis((IDictionary<string, object>)members[BasisName]),
                    (string)members[OriginName]);
            }
            catch (ArgumentException exception)
            {
                throw new FormatException(exception.Message, exception);
            }
        }

        private static NameBasis Basis(IDictionary<string, object> members)
        {
            string kind = (string)members[KindName];
            string[] located = { PathName, FirstLineName, LastLineName };
            if (string.Equals(kind, DocumentSectionText, StringComparison.Ordinal))
            {
                foreach (string name in located.Where(n => !members.ContainsKey(n)))
                {
                    throw new FormatException("項目が無い: " + name);
                }

                return NameBasis.FromDocumentSection(
                    (string)members[PathName], (int)members[FirstLineName], (int)members[LastLineName]);
            }

            if (string.Equals(kind, MemberShapeText, StringComparison.Ordinal))
            {
                foreach (string name in located.Where(members.ContainsKey))
                {
                    throw new FormatException("知らない項目がある: " + name);
                }

                return NameBasis.FromMemberShape();
            }

            throw new FormatException("知らない根拠の種別: " + kind);
        }
    }
}
