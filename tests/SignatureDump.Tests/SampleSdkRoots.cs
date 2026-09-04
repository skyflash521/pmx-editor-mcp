using System.Collections.Generic;

namespace PEPlugin
{
    // 題材のアセンブリはSDKの代役なので、接続初期化が辿り始める型をSDKと同じ名前で持つ。
    public interface IPERunArgs
    {
        string Version { get; }
    }
}

namespace PEPlugin.Pmx
{
    // 所有の経路が始まる型も、同じ理由でSDKと同じ名前で持つ。
    public interface IPXPmx
    {
        IList<IPXVertex> Vertex { get; }
    }

    public interface IPXVertex
    {
        int Index { get; }
    }

    // 反映を一部に限るときの指定が引く列挙型も、同じ理由でSDKと同じ名前で持つ。
    public enum PmxUpdateObject
    {
        Vertex,
        Bone,
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
