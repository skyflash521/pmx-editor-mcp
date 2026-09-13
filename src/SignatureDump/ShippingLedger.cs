using System;
using System.Collections.Generic;
using System.Linq;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>出荷する物の出所になるパッケージ。綴りも版も、発行が解決した値そのままである。</summary>
    public sealed class ShippedPackage : IEquatable<ShippedPackage>
    {
        public ShippedPackage(string id, string version)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("パッケージの綴りが空である。", nameof(id));
            }

            if (string.IsNullOrEmpty(version))
            {
                throw new ArgumentException("パッケージの版が空である。", nameof(version));
            }

            Id = id;
            Version = version;
        }

        public string Id { get; }

        public string Version { get; }

        public bool Equals(ShippedPackage other)
        {
            return other != null
                && string.Equals(Id, other.Id, StringComparison.Ordinal)
                && string.Equals(Version, other.Version, StringComparison.Ordinal);
        }

        public override bool Equals(object other)
        {
            return Equals(other as ShippedPackage);
        }

        public override int GetHashCode()
        {
            return (Id + "/" + Version).GetHashCode();
        }

        public override string ToString()
        {
            return Id + " " + Version;
        }
    }

    /// <summary>
    /// 発行が解決した資産の一覧。第三者ライセンス表示の母集団は、パッケージの一覧ではなくこれが
    /// 決める——自己完結の発行が取り込むランタイムパックの構成物は、パッケージの一覧に出ない。
    /// </summary>
    public sealed class ShippingLedger
    {
        private const string RootPrefix = "root=";
        private const string AssetPrefix = "asset=";

        private ShippingLedger(string packageRoot, IReadOnlyList<ShippedPackage> packages)
        {
            PackageRoot = packageRoot;
            Packages = packages;
        }

        /// <summary>パッケージを展開してある置き場。転記元はここから読む。</summary>
        public string PackageRoot { get; }

        /// <summary>出所のあるものだけ。自分たちが作った物は出所を持たないので入らない。</summary>
        public IReadOnlyList<ShippedPackage> Packages { get; }

        /// <summary>形が違えば <see cref="FormatException"/>。</summary>
        public static ShippingLedger Read(IEnumerable<string> ledgers)
        {
            if (ledgers == null)
            {
                throw new ArgumentNullException(nameof(ledgers));
            }

            string root = null;
            var packages = new List<ShippedPackage>();
            foreach (string ledger in ledgers)
            {
                string found = ReadOne(ledger, packages);
                if (root != null && !string.Equals(root, found, StringComparison.OrdinalIgnoreCase))
                {
                    throw new FormatException(
                        "台帳ごとにパッケージの置き場が違う: " + root + " と " + found);
                }

                root = found;
            }

            if (root == null)
            {
                throw new FormatException("出荷台帳が1つも無い。");
            }

            return new ShippingLedger(root, Ordered(packages));
        }

        private static string ReadOne(string ledger, List<ShippedPackage> packages)
        {
            if (ledger == null)
            {
                throw new FormatException("出荷台帳の中身が無い。");
            }

            string root = null;
            foreach (string line in ledger.Replace("\r\n", "\n").Split('\n'))
            {
                if (line.Length == 0)
                {
                    continue;
                }

                if (line.StartsWith(RootPrefix, StringComparison.Ordinal))
                {
                    root = line.Substring(RootPrefix.Length).TrimEnd('\\', '/');
                    continue;
                }

                if (!line.StartsWith(AssetPrefix, StringComparison.Ordinal))
                {
                    throw new FormatException("出荷台帳に解せない行がある: " + line);
                }

                string[] parts = line.Substring(AssetPrefix.Length).Split('|');
                if (parts.Length != 3)
                {
                    throw new FormatException("出荷台帳の資産の行が3つ組でない: " + line);
                }

                // 出所を持たない資産は自分たちが作った物である。第三者の表示には関わらない。
                if (parts[1].Length == 0)
                {
                    continue;
                }

                if (parts[2].Length == 0)
                {
                    throw new FormatException("出所はあるのに版が無い資産がある: " + line);
                }

                packages.Add(new ShippedPackage(parts[1], parts[2]));
            }

            if (root == null)
            {
                throw new FormatException("出荷台帳がパッケージの置き場を持たない。");
            }

            return root;
        }

        private static IReadOnlyList<ShippedPackage> Ordered(IEnumerable<ShippedPackage> packages)
        {
            return packages
                .Distinct()
                .OrderBy(package => package.Id, StringComparer.Ordinal)
                .ThenBy(package => package.Version, StringComparer.Ordinal)
                .ToList();
        }
    }
}
