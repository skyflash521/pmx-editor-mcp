using PEPlugin.SDX;

namespace PmxEditorMcp
{
    public sealed class UvRegion
    {
        public UvRegion(float minU, float maxU, float minV, float maxV)
        {
            MinU = minU;
            MaxU = maxU;
            MinV = minV;
            MaxV = maxV;
        }

        public float MinU { get; }

        public float MaxU { get; }

        public float MinV { get; }

        public float MaxV { get; }

        /// <summary>そのUVがこの範囲に入るか。端の値は入るものとし、UVが無いものは入らない。</summary>
        public bool Holds(V2 uv)
        {
            return uv != null
                && uv.X >= MinU && uv.X <= MaxU
                && uv.Y >= MinV && uv.Y <= MaxV;
        }
    }
}
