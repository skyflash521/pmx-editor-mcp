using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 出荷台帳と転記元から、配布物へ同梱する第三者ライセンス表示を組み立てる。
    /// 表示は組み立てた物と1バイトも違わないことを条件にするので、並びも改行も一意に決める。
    /// </summary>
    public static class ThirdPartyNoticeBuilder
    {
        /// <summary>転記の始まりと終わり。どこまでが転記した本文かを、読む側が切り出せるようにする。</summary>
        public const string BodyBegin = "-----BEGIN LICENSE-----";

        public const string BodyEnd = "-----END LICENSE-----";

        private static readonly string[] Header =
        {
            "pmx-editor-mcp が同梱する第三者のソフトウェアと、その表示。",
            string.Empty,
            "この表示は配布物を組み立てる元から機械で作る。手で書き換えない——書き換えても、",
            "次に組み立てたときに元へ戻る。",
            string.Empty,
            "名乗るライセンスと転記元が同じものは、同じ転記を何度も並べずにまとめてある。",
        };

        /// <summary>転記元が読めなければ <see cref="FormatException"/>。</summary>
        public static string Build(
            ShippingLedger ledger,
            string licenseDirectory,
            IEnumerable<PackageLicenseSource> sources)
        {
            if (ledger == null)
            {
                throw new ArgumentNullException(nameof(ledger));
            }

            if (licenseDirectory == null)
            {
                throw new ArgumentNullException(nameof(licenseDirectory));
            }

            if (sources == null)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            var entries = new List<NoticeEntry>();
            foreach (PackageLicenseSource source in sources)
            {
                Gather(entries, ledger, licenseDirectory, source);
            }

            var text = new StringBuilder();
            foreach (string line in Header)
            {
                text.Append(line).Append('\n');
            }

            foreach (NoticeEntry entry in entries)
            {
                text.Append('\n');
                entry.Write(text);
            }

            return text.ToString();
        }

        /// <summary>
        /// 転記元の本文を一意な形へそろえる。バイト順の印を外し、改行をLFへそろえ、末尾の空行を
        /// 落とす——同じ中身が、書き手の環境の違いで別物に見えないようにするためである。
        /// </summary>
        public static string Normalize(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            return text.TrimStart('﻿').Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd('\n');
        }

        private static void Gather(
            List<NoticeEntry> entries,
            ShippingLedger ledger,
            string licenseDirectory,
            PackageLicenseSource source)
        {
            string name = source.Package.Id + " " + source.Package.Version;

            // 同梱がライセンス本文を持たないときだけ、式が指す標準の本文を併記する。通知だけを
            // 同梱するパッケージは、本文をここからしか得られない。
            IReadOnlyList<string> standards = source.CarriesBody
                ? Array.Empty<string>()
                : source.LicenseIds;
            if (standards.Count == 0 && source.Files.Count == 0)
            {
                throw new FormatException(name + " の転記元が1つも無い。");
            }

            var origins = new List<string>();
            if (standards.Count != 0)
            {
                origins.Add("source: spdx " + string.Join(";", standards));
            }

            if (source.Files.Count != 0)
            {
                origins.Add("source: files " + string.Join(";", source.Files));
            }

            var body = new StringBuilder();
            foreach (string id in standards)
            {
                body.Append("--- spdx ").Append(id).Append(" ---\n");
                body.Append(Read(Path.Combine(licenseDirectory, id + ".txt"), "標準の本文"))
                    .Append('\n');
            }

            string directory = PackageLicenseSource.PathOf(ledger.PackageRoot, source.Package);
            foreach (string file in source.Files)
            {
                body.Append("--- ").Append(file).Append(" ---\n");
                body.Append(Read(Path.Combine(directory, file), "同梱の転記元")).Append('\n');
            }

            NoticeEntry same = entries.FirstOrDefault(
                entry => entry.Same(source.Expression, origins, body.ToString()));
            if (same == null)
            {
                entries.Add(new NoticeEntry(name, source.Expression, origins, body.ToString()));
                return;
            }

            same.Add(name);
        }

        private static string Read(string path, string what)
        {
            try
            {
                return Normalize(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (IOException exception)
            {
                throw new FormatException(what + "を読めない: " + path, exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new FormatException(what + "を読めない: " + path, exception);
            }
        }

        /// <summary>同じ転記になるパッケージをまとめた、表示の1区画。</summary>
        private sealed class NoticeEntry
        {
            private readonly List<string> _names;
            private readonly string _expression;
            private readonly IReadOnlyList<string> _origins;
            private readonly string _body;

            public NoticeEntry(
                string name, string expression, IReadOnlyList<string> origins, string body)
            {
                _names = new List<string> { name };
                _expression = expression;
                _origins = origins;
                _body = body;
            }

            public bool Same(string expression, IReadOnlyList<string> origins, string body)
            {
                return string.Equals(_expression, expression, StringComparison.Ordinal)
                    && _origins.SequenceEqual(origins, StringComparer.Ordinal)
                    && string.Equals(_body, body, StringComparison.Ordinal);
            }

            public void Add(string name)
            {
                _names.Add(name);
            }

            public void Write(StringBuilder text)
            {
                foreach (string name in _names)
                {
                    text.Append("## ").Append(name).Append('\n');
                }

                text.Append("license: ").Append(_expression).Append('\n');
                foreach (string origin in _origins)
                {
                    text.Append(origin).Append('\n');
                }

                text.Append(BodyBegin).Append('\n');
                text.Append(_body);
                text.Append(BodyEnd).Append('\n');
            }
        }
    }
}
