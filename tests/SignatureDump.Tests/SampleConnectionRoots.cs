namespace PEPlugin
{
    // 題材のアセンブリはSDKの代役なので、接続初期化が辿り始める型をSDKと同じ名前で持つ。
    public interface IPERunArgs
    {
        string Version { get; }
    }
}

namespace PXCPlugin
{
    public interface IPXCPluginRunArgs
    {
        string CPluginVersion { get; }
    }

    public static class PXCBridge
    {
        public static string BridgeVersion
        {
            get { return string.Empty; }
        }
    }
}
