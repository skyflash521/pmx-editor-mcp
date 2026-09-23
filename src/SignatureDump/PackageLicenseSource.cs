using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 1つのパッケージが持つ、ライセンス表示の転記元。名乗るライセンス式と、パッケージが同梱する
    /// ライセンス・通知の類を指す。
    /// </summary>
    public sealed class PackageLicenseSource
    {
        /// <summary>本文か通知として転記する物の名前。大文字と小文字は区別しない。</summary>
        private static readonly string[] TranscribedPrefixes =
        {
            "LICENSE", "NOTICE", "THIRD-PARTY", "COPYING",
        };

        /// <summary>ライセンス本文そのものを持つ物の名前。残りは通知として扱う。</summary>
        private static readonly string[] BodyPrefixes = { "LICENSE", "COPYING" };

        private PackageLicenseSource(
            ShippedPackage package, string expression, IReadOnlyList<string> files, bool carriesBody)
        {
            Package = package;
            Expression = expression;
            Files = files;
            CarriesBody = carriesBody;
        }

        public ShippedPackage Package { get; }

        /// <summary>パッケージがメタデータで名乗るSPDXのライセンス式。</summary>
        public string Expression { get; }

        /// <summary>同梱する転記元。パッケージの置き場からの相対で、名前の昇順に並ぶ。</summary>
        public IReadOnlyList<string> Files { get; }

        /// <summary>同梱する物がライセンス本文を含むか。含まないなら標準の本文を併記する。</summary>
        public bool CarriesBody { get; }

        /// <summary>ライセンス式が指すライセンスの綴り。併記や例外つきの式もここで分解する。</summary>
        public IReadOnlyList<string> LicenseIds
        {
            get { return Ids(Expression); }
        }

        /// <summary>読めなければ <see cref="FormatException"/>。</summary>
        public static PackageLicenseSource Read(string packageRoot, ShippedPackage package)
        {
            if (packageRoot == null)
            {
                throw new ArgumentNullException(nameof(packageRoot));
            }

            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            string directory = PathOf(packageRoot, package);
            if (!Directory.Exists(directory))
            {
                throw new FormatException(package + " の置き場が無い: " + directory);
            }

            string expression = Declared(directory, package);
            List<string> files = Transcribed(directory);
            bool carriesBody = files.Any(file => Named(Path.GetFileName(file), BodyPrefixes));
            return new PackageLicenseSource(package, expression, files, carriesBody);
        }

        /// <summary>パッケージを展開してある場所。綴りもバージョンも小文字へそろえた名前になる。</summary>
        public static string PathOf(string packageRoot, ShippedPackage package)
        {
            if (packageRoot == null)
            {
                throw new ArgumentNullException(nameof(packageRoot));
            }

            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            return Path.Combine(
                packageRoot,
                package.Id.ToLowerInvariant(),
                package.Version.ToLowerInvariant());
        }

        private static string Declared(string directory, ShippedPackage package)
        {
            string[] specs = Directory.GetFiles(directory, "*.nuspec");
            if (specs.Length != 1)
            {
                throw new FormatException(
                    package + " のメタデータが1つに定まらない: " + specs.Length + "件");
            }

            XDocument read;
            try
            {
                read = XDocument.Load(specs[0]);
            }
            catch (System.Xml.XmlException exception)
            {
                throw new FormatException(package + " のメタデータを読めない。", exception);
            }

            XElement license = read.Descendants()
                .FirstOrDefault(element => string.Equals(
                    element.Name.LocalName, "license", StringComparison.Ordinal));
            if (license == null)
            {
                throw new FormatException(package + " がライセンスを名乗っていない。");
            }

            XAttribute kind = license.Attribute("type");
            if (kind == null || !string.Equals(kind.Value, "expression", StringComparison.Ordinal))
            {
                throw new FormatException(
                    package + " のライセンスがSPDXの式でない: "
                        + (kind == null ? "型なし" : kind.Value));
            }

            string expression = license.Value.Trim();
            if (expression.Length == 0)
            {
                throw new FormatException(package + " のライセンス式が空である。");
            }

            return expression;
        }

        private static List<string> Transcribed(string directory)
        {
            return Directory
                .GetFiles(directory, "*", SearchOption.AllDirectories)
                .Where(path => Named(Path.GetFileName(path), TranscribedPrefixes))
                .Select(path => Relative(directory, path))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
        }

        private static string Relative(string directory, string path)
        {
            return path.Substring(directory.Length).TrimStart('\\', '/').Replace('\\', '/');
        }

        private static bool Named(string name, IEnumerable<string> prefixes)
        {
            return prefixes.Any(
                prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        private static IReadOnlyList<string> Ids(string expression)
        {
            var ids = new List<string>();
            string[] words = expression.Split(
                new[] { ' ', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string word in words)
            {
                if (string.Equals(word, "AND", StringComparison.Ordinal)
                    || string.Equals(word, "OR", StringComparison.Ordinal)
                    || string.Equals(word, "WITH", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!ids.Contains(word, StringComparer.Ordinal))
                {
                    ids.Add(word);
                }
            }

            return ids;
        }
    }
}
