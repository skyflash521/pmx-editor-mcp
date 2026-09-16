using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 能力対応表の行キーからSDKへの中継を組み立てて書き出す配線。ホストのビルドがこれを呼び、
    /// 出来た本文だけを配布物へ入れる。
    /// </summary>
    public static class RelaySourceRunner
    {
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false);

        public static int Run(string[] args, TextWriter output, TextWriter error)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (error == null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            if (args.Length != 4)
            {
                error.WriteLine(
                    "引数は4つ: <PMXエディタ導入ディレクトリ> <能力対応表の正本のパス>"
                        + " <能力台帳のパス> <書き出し先パス>");
                return ExitCodes.InvalidArguments;
            }

            string editorDirectory = args[0];
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(editorDirectory);
            if (!File.Exists(assemblyPath))
            {
                error.WriteLine("対象のアセンブリが無い: " + assemblyPath);
                return ExitCodes.InputUnavailable;
            }

            ToolMap toolMap;
            string toolMapDigest;
            try
            {
                if (!File.Exists(args[1]))
                {
                    throw new FileNotFoundException("能力対応表が無い: " + args[1], args[1]);
                }

                string toolMapText = File.ReadAllText(args[1]);
                toolMap = ToolMapJsonReader.Read(toolMapText);
                toolMapDigest = ToolMapDigest.Of(toolMapText);
            }
            catch (Exception exception)
            {
                error.WriteLine(exception.Message);
                return ExitCodes.InputUnavailable;
            }

            SdkFacts facts;
            try
            {
                facts = SdkInventory.Read(editorDirectory, assemblyPath, SdkFacts.Of);
            }
            catch (Exception exception)
            {
                error.WriteLine("対象のアセンブリを読めない: " + assemblyPath);
                error.WriteLine(exception.Message);
                return ExitCodes.InputUnavailable;
            }

            InventoryRecord inventory = facts.Inventory;
            IList<CapabilityRecord> ledger;
            try
            {
                if (!File.Exists(args[2]))
                {
                    throw new FileNotFoundException("能力台帳が無い: " + args[2], args[2]);
                }

                ledger = LedgerJsonReader.Read(File.ReadAllText(args[2]));
            }
            catch (Exception exception)
            {
                error.WriteLine("能力台帳を読めない: " + args[2]);
                error.WriteLine(exception.Message);
                return ExitCodes.InputUnavailable;
            }

            IDictionary<string, ReceiverPath> receivers;
            try
            {
                receivers = ReceiverEvidence.Resolve(
                    inventory,
                    Receiving(toolMap, inventory),
                    ReceiverRouteEvidence.Candidates(inventory, ledger));
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine("受け手の道を辿れない。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            RelaySource source = RelaySourceBuilder.Build(
                toolMap.Rows.Select(r => r.SignatureKey),
                inventory,
                inventory.AssemblyVersion,
                facts.CombinableEnums,
                toolMapDigest,
                receivers);

            try
            {
                WriteIfChanged(args[3], source.Text);
            }
            catch (Exception exception)
            {
                error.WriteLine("書き出せない: " + args[3]);
                error.WriteLine(exception.Message);
                return ExitCodes.WriteFailed;
            }

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "中継を組み立てた: 行 {0} 件・解決 {1} 件・未解決 {2} 件・知らせ {3} 件"
                    + "・受け手 {4} 型・SDK {5}",
                source.Resolved.Count + source.Unresolved.Count + source.Notified.Count,
                source.Resolved.Count,
                source.Unresolved.Count,
                source.Notified.Count,
                receivers.Count,
                inventory.AssemblyVersion));

            return ExitCodes.Success;
        }

        /// <summary>
        /// 受け手の要る宣言型。静的なメンバーだけを持たせた行は呼ぶ相手が無いので、道も要らない。
        /// </summary>
        private static IEnumerable<string> Receiving(ToolMap toolMap, InventoryRecord inventory)
        {
            HashSet<string> rows = new HashSet<string>(
                toolMap.Rows.Select(r => r.SignatureKey), StringComparer.Ordinal);

            return inventory.Signatures
                .Where(s => !s.IsStatic && rows.Contains(s.Key))
                .Select(s => TypeDefinitionName.Of(s.DeclaringType));
        }

        /// <summary>
        /// 中身が変わっていなければ書かない。書き直すと更新時刻が動いて、ホストのビルドが毎回
        /// やり直しになる。
        /// </summary>
        private static void WriteIfChanged(string path, string text)
        {
            if (File.Exists(path)
                && string.Equals(File.ReadAllText(path, Utf8WithoutBom), text, StringComparison.Ordinal))
            {
                return;
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, text, Utf8WithoutBom);
        }
    }
}
