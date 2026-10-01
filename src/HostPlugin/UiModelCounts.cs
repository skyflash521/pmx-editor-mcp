using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PEPlugin.Pmx;

namespace PmxEditorMcp
{
    public static class UiModelCounts
    {
        public const string RegisterKey = "PXCPlugin.PXCBridge.RegisterUIModel";

        private static readonly ConditionalWeakTable<object, Counts> Known = new ConditionalWeakTable<object, Counts>();

        public static void Remember(string rowKey, IList<string> names, object[] arguments, object registered)
        {
            int open = rowKey.IndexOf('(');
            string declared = open < 0 ? rowKey : rowKey.Substring(0, open);
            int pmx = names.IndexOf("pmx");
            IPXPmx model = pmx < 0 ? null : arguments[pmx] as IPXPmx;
            if (declared != RegisterKey || registered == null || model == null)
            {
                return;
            }

            Known.Remove(registered);
            Known.Add(registered, new Counts(model.Bone.Count, model.Vertex.Count, model.Morph.Count, model.Material.Count));
        }

        public static bool TryGet(object uiModel, out Counts counts)
        {
            counts = null;

            return uiModel != null && Known.TryGetValue(uiModel, out counts);
        }

        public sealed class Counts
        {
            public Counts(int bone, int vertex, int morph, int material)
            {
                Bone = bone;
                Vertex = vertex;
                Morph = morph;
                Material = material;
            }

            public int Bone { get; }

            public int Vertex { get; }

            public int Morph { get; }

            public int Material { get; }
        }
    }
}
