using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>出荷台帳の読み取りと、そこから組み立てる第三者ライセンス表示。</summary>
    public sealed class ThirdPartyNoticeTests
    {
        private const string Root = @"C:\packages";

        [Fact]
        public void TheLedgerKeepsOnlyAssetsThatCameFromSomewhereElse()
        {
            ShippingLedger read = ShippingLedger.Read(new[]
            {
                Ledger("asset=Bridge.exe||", "asset=lib.dll|Some.Package|1.0.0"),
            });

            Assert.Equal(Root, read.PackageRoot);
            Assert.Equal(new[] { "Some.Package 1.0.0" }, Said(read));
        }

        [Fact]
        public void TheLedgerCountsAPackageOnceHoweverManyAssetsItShipped()
        {
            ShippingLedger read = ShippingLedger.Read(new[]
            {
                Ledger(
                    "asset=a.dll|Some.Package|1.0.0",
                    "asset=b.dll|Some.Package|1.0.0"),
            });

            Assert.Equal(new[] { "Some.Package 1.0.0" }, Said(read));
        }

        [Fact]
        public void TheLedgerKeepsBothWhenOnePackageResolvedToTwoVersions()
        {
            ShippingLedger read = ShippingLedger.Read(new[]
            {
                Ledger("asset=a.dll|Some.Package|2.0.0"),
                Ledger("asset=b.dll|Some.Package|1.0.0"),
            });

            Assert.Equal(new[] { "Some.Package 1.0.0", "Some.Package 2.0.0" }, Said(read));
        }

        [Fact]
        public void ALedgerThatNamesNoPackageRootIsRefused()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ShippingLedger.Read(new[] { "asset=a.dll|Some.Package|1.0.0" }));

            Assert.Contains("置き場", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void LedgersThatNameDifferentPackageRootsAreRefused()
        {
            FormatException error = Assert.Throws<FormatException>(() => ShippingLedger.Read(new[]
            {
                Ledger("asset=a.dll|Some.Package|1.0.0"),
                "root=D:\\elsewhere\nasset=b.dll|Other.Package|1.0.0",
            }));

            Assert.Contains("違う", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void AnAssetThatNamesAPackageWithoutAVersionIsRefused()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => ShippingLedger.Read(new[] { Ledger("asset=a.dll|Some.Package|") }));

            Assert.Contains("バージョンが無い", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void TranscribedTextLosesTheByteOrderMarkAndTheCarriageReturnsAndTheTrailingBlanks()
        {
            Assert.Equal("a\nb", ThirdPartyNoticeBuilder.Normalize("\ufeffa\r\nb\r\n\r\n"));
        }

        [Fact]
        public void APackageThatShipsNoLicenseBodyGetsTheStandardTextOfWhatItDeclares()
        {
            string built = Build(
                new[] { Package("Some.Package", "1.0.0", "MIT", null) });

            Assert.Contains("source: spdx MIT", built, StringComparison.Ordinal);
            Assert.Contains("--- spdx MIT ---\nMITの標準の本文", built, StringComparison.Ordinal);
        }

        [Fact]
        public void APackageThatShipsItsLicenseBodyGetsNoStandardTextBesideIt()
        {
            string built = Build(
                new[] { Package("Some.Package", "1.0.0", "MIT", "LICENSE.TXT") });

            Assert.DoesNotContain("source: spdx", built, StringComparison.Ordinal);
            Assert.Contains("source: files LICENSE.TXT", built, StringComparison.Ordinal);
        }

        [Fact]
        public void APackageThatShipsOnlyANoticeGetsBothTheStandardTextAndTheNotice()
        {
            string built = Build(
                new[] { Package("Some.Package", "1.0.0", "MIT", "THIRD-PARTY-NOTICES.TXT") });

            Assert.Contains("source: spdx MIT", built, StringComparison.Ordinal);
            Assert.Contains("source: files THIRD-PARTY-NOTICES.TXT", built, StringComparison.Ordinal);
        }

        [Fact]
        public void PackagesWhoseTranscriptionIsTheSameShareOneBodyAndKeepBothNames()
        {
            string built = Build(new[]
            {
                Package("One.Package", "1.0.0", "MIT", null),
                Package("Two.Package", "2.0.0", "MIT", null),
            });

            Assert.Contains("## One.Package 1.0.0\n## Two.Package 2.0.0\nlicense: MIT",
                built, StringComparison.Ordinal);
            Assert.Equal(1, Times(built, ThirdPartyNoticeBuilder.BodyBegin));
        }

        [Fact]
        public void PackagesWhoseTranscriptionDiffersKeepTheirOwnBodies()
        {
            string built = Build(new[]
            {
                Package("One.Package", "1.0.0", "MIT", null),
                Package("Two.Package", "2.0.0", "Apache-2.0", null),
            });

            Assert.Equal(2, Times(built, ThirdPartyNoticeBuilder.BodyBegin));
        }

        [Fact]
        public void APackageThatDeclaresALicenseWithNoStandardTextAtHandIsRefused()
        {
            FormatException error = Assert.Throws<FormatException>(
                () => Build(new[] { Package("Some.Package", "1.0.0", "BSD-3-Clause", null) }));

            Assert.Contains("標準の本文", error.Message, StringComparison.Ordinal);
        }

        private static string Ledger(params string[] assets)
        {
            return "root=" + Root + "\n" + string.Join("\n", assets);
        }

        private static IEnumerable<string> Said(ShippingLedger read)
        {
            return read.Packages.Select(package => package.ToString());
        }

        private static int Times(string text, string part)
        {
            int count = 0;
            int at = text.IndexOf(part, StringComparison.Ordinal);
            while (at >= 0)
            {
                count++;
                at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal);
            }

            return count;
        }

        /// <summary>何を名乗り何を同梱するかを決めた、試しのパッケージ。</summary>
        private sealed class Declared
        {
            public Declared(string id, string version, string expression, string bundled)
            {
                Id = id;
                Version = version;
                Expression = expression;
                Bundled = bundled;
            }

            public string Id { get; }

            public string Version { get; }

            public string Expression { get; }

            /// <summary>同梱する転記元の名前。無いなら null。</summary>
            public string Bundled { get; }
        }

        private static Declared Package(string id, string version, string expression, string bundled)
        {
            return new Declared(id, version, expression, bundled);
        }

        /// <summary>試しのパッケージを置き場へ並べ、そこから表示を組み立てる。</summary>
        private static string Build(IEnumerable<Declared> declared)
        {
            string work = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string packages = Path.Combine(work, "packages");
            string licenses = Path.Combine(work, "licenses");
            Directory.CreateDirectory(licenses);
            Write(Path.Combine(licenses, "MIT.txt"), "MITの標準の本文");
            Write(Path.Combine(licenses, "Apache-2.0.txt"), "Apache-2.0の標準の本文");

            try
            {
                var assets = new List<string>();
                foreach (Declared one in declared)
                {
                    string directory = Path.Combine(
                        packages, one.Id.ToLowerInvariant(), one.Version.ToLowerInvariant());
                    Directory.CreateDirectory(directory);
                    Write(
                        Path.Combine(directory, one.Id.ToLowerInvariant() + ".nuspec"),
                        "<package><metadata><license type=\"expression\">"
                            + one.Expression + "</license></metadata></package>");
                    if (one.Bundled != null)
                    {
                        Write(Path.Combine(directory, one.Bundled), "同梱の本文");
                    }

                    assets.Add("asset=" + one.Id + ".dll|" + one.Id + "|" + one.Version);
                }

                ShippingLedger ledger = ShippingLedger.Read(new[]
                {
                    "root=" + packages + "\n" + string.Join("\n", assets),
                });
                IEnumerable<PackageLicenseSource> sources = ledger.Packages
                    .Select(package => PackageLicenseSource.Read(ledger.PackageRoot, package))
                    .ToList();
                return ThirdPartyNoticeBuilder.Build(ledger, licenses, sources);
            }
            finally
            {
                Directory.Delete(work, true);
            }
        }

        private static void Write(string path, string text)
        {
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
    }
}
