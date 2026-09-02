using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 日本語名の正本を規則と照合する配線。ファイルは書き出さず、合否だけを終了コードで返す。
    /// </summary>
    public static class PropertyNameRunner
    {
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
                    "引数は4つ: <PMXエディタ導入ディレクトリ> <能力台帳のパス> <除外一覧のパス>"
                        + " <日本語名の正本のパス>");
                return ExitCodes.InvalidArguments;
            }

            string editorDirectory = args[0];
            string assemblyPath = SdkAssemblyLocator.GetAssemblyPath(editorDirectory);
            if (!File.Exists(assemblyPath))
            {
                error.WriteLine("対象のアセンブリが無い: " + assemblyPath);
                return ExitCodes.InputUnavailable;
            }

            string documentPath = SdkAssemblyLocator.GetDocumentPath(editorDirectory);
            IList<CapabilityRecord> ledger;
            IList<ExcludedSignatureRecord> excluded;
            IDictionary<string, string> notes;
            IList<PropertyNameRecord> names;
            try
            {
                ledger = LedgerParser.Parse(Read(args[1], "能力台帳"));
                excluded = ExcludedSignatureJsonReader.Read(Read(args[2], "除外一覧"));
                names = PropertyNameJsonReader.ReadPropertyNames(Read(args[3], "日本語名の正本"));
                notes = DocumentNoteReader.Read(Read(documentPath, "ドキュメントXML"));
            }
            catch (Exception exception)
            {
                error.WriteLine(exception.Message);
                return ExitCodes.InputUnavailable;
            }

            IList<PropertyRecord> properties;
            try
            {
                properties = SdkInventory.Read(
                    editorDirectory,
                    assemblyPath,
                    assembly => Enumerate(assembly, ledger, excluded));
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine("母集合を決められない。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }
            catch (Exception exception)
            {
                error.WriteLine("対象のアセンブリを読めない: " + assemblyPath);
                error.WriteLine(exception.Message);
                return ExitCodes.InputUnavailable;
            }

            try
            {
                PropertyNameGate.Require(names, properties, notes, path => LineCount(editorDirectory, path));
            }
            catch (InvalidOperationException exception)
            {
                error.WriteLine("日本語名が規則に合わない。");
                error.WriteLine(exception.Message);
                return ExitCodes.Unresolved;
            }

            output.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "照合した: 項目 {0} 件(記載を採る {1}・名前を起こす {2})",
                names.Count,
                names.Count(n => n.Decision == NameDecision.Quoted),
                names.Count(n => n.Decision == NameDecision.Authored)));

            return ExitCodes.Success;
        }

        private static IList<PropertyRecord> Enumerate(
            Assembly assembly,
            IList<CapabilityRecord> ledger,
            IList<ExcludedSignatureRecord> excluded)
        {
            TypeRolePopulation population = TypeRolePopulation.Resolve(
                ledger, AssemblyEnumerator.Enumerate(assembly), excluded);
            IList<Type> candidates = Candidates(assembly);

            HashSet<string> found = new HashSet<string>(
                candidates
                    .Select(t => TypeDefinitionName.Of(TypeNameFormatter.Format(t)))
                    .Where(population.RoleTypes.Contains),
                StringComparer.Ordinal);
            string unresolved = population.RoleTypes.Except(found, StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal).FirstOrDefault();
            if (unresolved != null)
            {
                throw new InvalidOperationException("役割対象の型を読み込めない: " + unresolved);
            }

            return RoleTypeProperties.Enumerate(population.RoleTypes, candidates);
        }

        /// <summary>対象アセンブリと、読み込み済みのアセンブリの公開型を候補にする。</summary>
        private static IList<Type> Candidates(Assembly assembly)
        {
            List<Type> candidates = new List<Type>(Visible(assembly));
            foreach (Assembly loaded in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (loaded != assembly)
                {
                    candidates.AddRange(Visible(loaded));
                }
            }

            return candidates;
        }

        private static IEnumerable<Type> Visible(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes().Where(t => t.IsVisible);
            }
            catch (ReflectionTypeLoadException)
            {
                return new Type[0];
            }
        }

        private static int LineCount(string editorDirectory, string relativePath)
        {
            string path = Path.Combine(editorDirectory, relativePath);
            return File.Exists(path) ? File.ReadAllLines(path).Length : -1;
        }

        private static string Read(string path, string name)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(name + "が無い: " + path, path);
            }

            return File.ReadAllText(path);
        }
    }
}
