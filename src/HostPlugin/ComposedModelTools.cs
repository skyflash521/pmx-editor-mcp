using System;

namespace PmxEditorMcp
{
    /// <summary>
    /// SDKの1メンバーへ写らない、組み立てのモデル編集ツールを表へ足す口。ここを通したツールは
    /// 生成したツールと同じ複製編集の経路に乗り、1回の呼び出しが1回のUndoで戻る。
    /// </summary>
    public static class ComposedModelTools
    {
        /// <summary>組み立てのモデル編集ツールを表へ足す。</summary>
        public static void AddTo(McpMethodTable methods, ComposedEdit edit, Func<object> builder)
        {
            if (methods == null)
            {
                throw new ArgumentNullException(nameof(methods));
            }

            if (edit == null)
            {
                throw new ArgumentNullException(nameof(edit));
            }

            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            ModelReorderElements.AddTo(methods, edit);
            ModelCleanFaces.AddTo(methods, edit);
            ModelEditFaces.AddTo(methods, edit);
            ModelEditMaterials.AddTo(methods, edit);
            ModelEditVertices.AddTo(methods, edit);
            ModelEditUv.AddTo(methods, edit);
            ModelDrawUvLayout.AddTo(methods, edit);
            ModelEditNormals.AddTo(methods, edit);
            ModelEditWeights.AddTo(methods, edit);
            ModelSetDeformType.AddTo(methods, edit);
            ModelEditBones.AddTo(methods, edit, builder);
            ModelCreatePhysics.AddTo(methods, edit, builder);
            ModelEditMorphs.AddTo(methods, edit, builder);
            ModelMorphFromMoved.AddTo(methods, edit, builder);
            ModelEditNodes.AddTo(methods, edit, builder);
            ModelCopyFromReference.AddTo(methods, edit);
            ModelPlaceElements.AddTo(methods, edit);
            ModelMirrorElements.AddTo(methods, edit);
            ModelValidatePmx.AddTo(methods, edit);
            ModelFindReferrers.AddTo(methods, edit);
            ModelFindMaterialVertices.AddTo(methods, edit);
            ModelFindVertexBounds.AddTo(methods, edit);
            ModelInsertElements.AddTo(methods, edit, builder);
            ModelDeleteElements.AddTo(methods, edit);
        }
    }
}
