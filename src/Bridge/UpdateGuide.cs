using System;
using System.IO;
using System.Text.Json.Nodes;

namespace PmxEditorMcp.Bridge
{
    public static class UpdateGuide
    {
        public const string RepositoryUrl = "https://github.com/skyflash521/pmx-editor-mcp";

        public const string LatestReleaseUrl =
            "https://api.github.com/repos/skyflash521/pmx-editor-mcp/releases/latest";

        public const string InstallGuideFileName = "INSTALL.md";

        public static string InstalledVersion()
        {
            return typeof(UpdateGuide).Assembly.GetName().Version.ToString(3);
        }

        public static JsonObject Describe(string installDirectory)
        {
            if (installDirectory == null)
            {
                throw new ArgumentNullException(nameof(installDirectory));
            }

            string directory = Path.GetFullPath(installDirectory);
            string guide = Path.Combine(directory, InstallGuideFileName);
            bool found = File.Exists(guide);

            return new JsonObject
            {
                ["installedVersion"] = InstalledVersion(),
                ["installDirectory"] = directory,
                ["installGuide"] = found ? guide : null,
                ["repository"] = RepositoryUrl,
                ["latestRelease"] = LatestReleaseUrl,
                ["steps"] = found
                    ? "installGuide の「更新する」に従う。アンインストールは同じ文書の「アンインストールする」に従う。"
                    : "installDirectory に " + InstallGuideFileName + " が無い。配布パッケージから導入した"
                        + "ブリッジではない。latestRelease が返す最新のリリースのzipを落とし、そのアセットの"
                        + " digest の sha256: に続く値とハッシュが一致することを確かめてから展開する。"
                        + "一致しないとき、digest が無いときは展開しない。展開した "
                        + InstallGuideFileName + " の「導入する」に従う。",
            };
        }
    }
}
