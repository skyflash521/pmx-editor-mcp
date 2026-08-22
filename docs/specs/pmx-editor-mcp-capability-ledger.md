# PEPlugin SDK 能力台帳

PMXエディタ配布物(PmxEditor_0273)の PEPlugin.dll(v0.0.8.9、.NET Framework 4.0)の
公開APIをリフレクションで列挙し、能力単位に集約した台帳。pmx-editor-mcp がツール化する
対象の正本とする。ツール契約仕様書はこの台帳の「担当」列に従って契約を定める。

## 凡例

- **能力の単位**: コネクタの公開メソッド1つ=1能力。同名オーバーロードは1能力に
  まとめ、シグネチャによって可否が分かれる場合は備考に記す。プロパティはget/setで
  1能力。データ型は型ごとに1能力(全公開メンバーを含む)。ビルダ生成は生成対象型
  ごとに1能力。列挙型・引数専用型は数えない。コネクタへの経路となるだけの型
  (IPEConnector等)は行を作らず、能力は参照先コネクタで計上する。性質が同一で
  同じ理由により一括分類する型群(プラグイン機構・PMDレガシー等)は1行にまとめる。
- **分類**: 提供=ツール化する / 非対応=理由を備考に記載 / 要調査=実機確認(E2Eスパイク)で確定する
- **担当**: 分類が「提供」の能力を担当するツール契約仕様書。モデル / セッション / ビュー / 変形・モーション
- 実行時オブジェクト(デリゲート等)は直接は受け渡せないため、宣言的な記述を
  ホスト側で変換する設計を前提に提供する(設計しても意味を持たないもの——重複経路・
  プラグイン機構専用など——だけを非対応とする)。
- 危険な操作(エディタ終了・上書き保存等)も「技術的にツール化するか」で分類する。
  公開の可否・確認フローはアーキテクチャ仕様書の危険操作の公開方針が定める。

集計: 提供 298 / 非対応 4 / 要調査 66(計 368)

| ID | 大分類 | 対象 | 分類 | 担当 | 備考 |
|---|---|---|---|---|---|
| CAP-001 | PMXデータ | IPXPmxConnector.GetCurrentState | 提供 | モデル |  |
| CAP-002 | PMXデータ | IPXPmxConnector.CurrentPath | 提供 | セッション | 開いているファイルパスの取得・設定 |
| CAP-003 | PMXデータ | IPXPmxConnector.Update | 提供 | モデル | 全体更新と部分更新(単一/複数Index)のオーバーロードを含む |
| CAP-004 | PMXデータ | IPXPmxConnector.LockUndo | 提供 | セッション | Undo記録の制御 |
| CAP-005 | PMXデータ | IPXPmxConnector.UnlockUndo | 提供 | セッション | Undo記録の制御 |
| CAP-006 | 本体フォーム | IPEFormConnector.Close | 提供 | セッション | 危険操作。公開方針はアーキテクチャ仕様書に従う |
| CAP-007 | 本体フォーム | IPEFormConnector.InitializePMD | 提供 | セッション |  |
| CAP-008 | 本体フォーム | IPEFormConnector.OpenPMDFile | 提供 | セッション |  |
| CAP-009 | 本体フォーム | IPEFormConnector.ImportXFile | 提供 | セッション |  |
| CAP-010 | 本体フォーム | IPEFormConnector.AppendPMDFile | 提供 | セッション |  |
| CAP-011 | 本体フォーム | IPEFormConnector.AppendXFile | 提供 | セッション |  |
| CAP-012 | 本体フォーム | IPEFormConnector.SavePMDFile | 提供 | セッション |  |
| CAP-013 | 本体フォーム | IPEFormConnector.SaveMaterialName | 提供 | セッション |  |
| CAP-014 | 本体フォーム | IPEFormConnector.InitializePMX | 提供 | セッション |  |
| CAP-015 | 本体フォーム | IPEFormConnector.OpenPMXFile | 提供 | セッション |  |
| CAP-016 | 本体フォーム | IPEFormConnector.SavePMXFile | 提供 | セッション |  |
| CAP-017 | 本体フォーム | IPEFormConnector.UpdateList | 提供 | ビュー | リスト表示更新 |
| CAP-018 | 本体フォーム | IPEFormConnector.VertexItemsCount | 提供 | ビュー |  |
| CAP-019 | 本体フォーム | IPEFormConnector.FaceItemsCount | 提供 | ビュー |  |
| CAP-020 | 本体フォーム | IPEFormConnector.MaterialItemsCount | 提供 | ビュー |  |
| CAP-021 | 本体フォーム | IPEFormConnector.BoneItemsCount | 提供 | ビュー |  |
| CAP-022 | 本体フォーム | IPEFormConnector.IKItemsCount | 提供 | ビュー |  |
| CAP-023 | 本体フォーム | IPEFormConnector.IKLinkItemsCount | 提供 | ビュー |  |
| CAP-024 | 本体フォーム | IPEFormConnector.ExpressionItemsCount | 提供 | ビュー |  |
| CAP-025 | 本体フォーム | IPEFormConnector.ExpressionOffsetItemsCount | 提供 | ビュー |  |
| CAP-026 | 本体フォーム | IPEFormConnector.FrameExpressionItemsCount | 提供 | ビュー |  |
| CAP-027 | 本体フォーム | IPEFormConnector.FrameBoneItemsCount | 提供 | ビュー |  |
| CAP-028 | 本体フォーム | IPEFormConnector.FrameBone_BoneItemsCount | 提供 | ビュー |  |
| CAP-029 | 本体フォーム | IPEFormConnector.BodyItemsCount | 提供 | ビュー |  |
| CAP-030 | 本体フォーム | IPEFormConnector.JointItemsCount | 提供 | ビュー |  |
| CAP-031 | 本体フォーム | IPEFormConnector.MorphItemsCount | 提供 | ビュー |  |
| CAP-032 | 本体フォーム | IPEFormConnector.NodeItemsCount | 提供 | ビュー |  |
| CAP-033 | 本体フォーム | IPEFormConnector.NodeElementItemsCount | 提供 | ビュー |  |
| CAP-034 | 本体フォーム | IPEFormConnector.SelectedTabPage | 提供 | ビュー |  |
| CAP-035 | 本体フォーム | IPEFormConnector.SelectedVertexIndex | 提供 | ビュー |  |
| CAP-036 | 本体フォーム | IPEFormConnector.SelectedFaceIndex | 提供 | ビュー |  |
| CAP-037 | 本体フォーム | IPEFormConnector.SelectedMaterialIndex | 提供 | ビュー |  |
| CAP-038 | 本体フォーム | IPEFormConnector.GetSelectedMaterialIndices | 提供 | ビュー |  |
| CAP-039 | 本体フォーム | IPEFormConnector.SetSelectedMaterialIndices | 提供 | ビュー |  |
| CAP-040 | 本体フォーム | IPEFormConnector.SelectedBoneIndex | 提供 | ビュー |  |
| CAP-041 | 本体フォーム | IPEFormConnector.SelectedIKIndex | 提供 | ビュー |  |
| CAP-042 | 本体フォーム | IPEFormConnector.SelectedIKLinkIndex | 提供 | ビュー |  |
| CAP-043 | 本体フォーム | IPEFormConnector.SelectedExpressionIndex | 提供 | ビュー |  |
| CAP-044 | 本体フォーム | IPEFormConnector.SelectedExpressionOffsetIndex | 提供 | ビュー |  |
| CAP-045 | 本体フォーム | IPEFormConnector.SelectedFrameExpressionIndex | 提供 | ビュー |  |
| CAP-046 | 本体フォーム | IPEFormConnector.SelectedFrameBoneIndex | 提供 | ビュー |  |
| CAP-047 | 本体フォーム | IPEFormConnector.SelectedFrameBone_BoneIndex | 提供 | ビュー |  |
| CAP-048 | 本体フォーム | IPEFormConnector.SelectedBodyIndex | 提供 | ビュー |  |
| CAP-049 | 本体フォーム | IPEFormConnector.SelectedJointIndex | 提供 | ビュー |  |
| CAP-050 | 本体フォーム | IPEFormConnector.Undo | 提供 | セッション |  |
| CAP-051 | 本体フォーム | IPEFormConnector.UndoCount | 提供 | セッション |  |
| CAP-052 | 本体フォーム | IPEFormConnector.Redo | 提供 | セッション |  |
| CAP-053 | 本体フォーム | IPEFormConnector.RedoCount | 提供 | セッション |  |
| CAP-054 | 本体フォーム | IPEFormConnector.Location | 提供 | ビュー |  |
| CAP-055 | 本体フォーム | IPEFormConnector.TopMost | 提供 | ビュー |  |
| CAP-056 | 本体フォーム | IPEFormConnector.EnableFacePage | 提供 | セッション |  |
| CAP-057 | 本体フォーム | IPEFormConnector.GetBoneKindColors | 提供 | ビュー |  |
| CAP-058 | 本体フォーム | IPEFormConnector.GetExpressionCategoryColors | 提供 | ビュー |  |
| CAP-059 | 本体フォーム | IPEFormConnector.GetBodyModeColors | 提供 | ビュー |  |
| CAP-060 | 本体フォーム | IPEFormConnector.GetBodyGroupColors | 提供 | ビュー |  |
| CAP-061 | 本体フォーム | IPEFormConnector.EnableTexUpdateWatch | 提供 | セッション |  |
| CAP-062 | 本体フォーム | IPEFormConnector.PmxFormActivate | 提供 | セッション |  |
| CAP-063 | システム | IPESystemConnector.PEPluginAssemblyVersion | 提供 | セッション |  |
| CAP-064 | システム | IPESystemConnector.PEPluginAssemblyPath | 提供 | セッション |  |
| CAP-065 | システム | IPESystemConnector.DefaultPluginFolderPath | 提供 | セッション |  |
| CAP-066 | システム | IPESystemConnector.HostApplicationPath | 提供 | セッション |  |
| CAP-067 | システム | IPESystemConnector.SlimDXAssemblyPath | 提供 | セッション |  |
| CAP-068 | システム | IPESystemConnector.RegisteredPluginCount | 提供 | セッション |  |
| CAP-069 | システム | IPESystemConnector.FindRegisteredPluginsFromMenuText | 提供 | セッション |  |
| CAP-070 | システム | IPESystemConnector.GetPluginInfo | 提供 | セッション |  |
| CAP-071 | システム | IPESystemConnector.RunPlugin | 提供 | セッション |  |
| CAP-072 | システム | IPESystemConnector.SetShareObject | 提供 | セッション | JSONで表現できる値に限定して提供(任意の.NETオブジェクトは対象外) |
| CAP-073 | システム | IPESystemConnector.GetShareObject | 提供 | セッション | JSONで表現できる値に限定して提供(任意の.NETオブジェクトは対象外) |
| CAP-074 | システム | IPESystemConnector.RemoveShareObject | 提供 | セッション |  |
| CAP-075 | システム | IPESystemConnector.RegisteredCPluginCount | 提供 | セッション |  |
| CAP-076 | システム | IPESystemConnector.GetCPluginInfo | 提供 | セッション |  |
| CAP-077 | システム | IPESystemConnector.RunCPlugin | 提供 | セッション |  |
| CAP-078 | システム | IPESystemConnector.GetCPluginRunArgsClone | 要調査 |  | PXCPlugin型の扱いが不明。実機確認 |
| CAP-079 | PmxView | IPXPmxViewConnector.GetViewMatrix | 要調査 |  | シグネチャを解決できなかったため実機確認 |
| CAP-080 | PmxView | IPXPmxViewConnector.GetProjectionMatrix | 要調査 |  | シグネチャを解決できなかったため実機確認 |
| CAP-081 | PmxView | IPXPmxViewConnector.UpdateView | 提供 | ビュー |  |
| CAP-082 | PmxView | IPXPmxViewConnector.UpdateModel | 提供 | ビュー |  |
| CAP-083 | PmxView | IPXPmxViewConnector.UpdateModel_Vertex | 提供 | ビュー |  |
| CAP-084 | PmxView | IPXPmxViewConnector.UpdateModel_Bone | 提供 | ビュー |  |
| CAP-085 | PmxView | IPXPmxViewConnector.UpdateModel_Weight | 提供 | ビュー |  |
| CAP-086 | PmxView | IPXPmxViewConnector.UpdateModel_Body | 提供 | ビュー |  |
| CAP-087 | PmxView | IPXPmxViewConnector.UpdateModel_Joint | 提供 | ビュー |  |
| CAP-088 | PmxView | IPXPmxViewConnector.UpdateModel_Material | 提供 | ビュー |  |
| CAP-089 | PmxView | IPXPmxViewConnector.UpdateModelSize | 提供 | ビュー |  |
| CAP-090 | PmxView | IPXPmxViewConnector.GetVertexIndices | 提供 | ビュー |  |
| CAP-091 | PmxView | IPXPmxViewConnector.SetVertexIndices | 提供 | ビュー |  |
| CAP-092 | PmxView | IPXPmxViewConnector.GetSelectedVertexIndices | 提供 | ビュー |  |
| CAP-093 | PmxView | IPXPmxViewConnector.SetSelectedVertexIndices | 提供 | ビュー |  |
| CAP-094 | PmxView | IPXPmxViewConnector.GetSelectedFaceIndices | 提供 | ビュー |  |
| CAP-095 | PmxView | IPXPmxViewConnector.SetSelectedFaceIndices | 提供 | ビュー |  |
| CAP-096 | PmxView | IPXPmxViewConnector.GetSelectedBoneIndices | 提供 | ビュー |  |
| CAP-097 | PmxView | IPXPmxViewConnector.SetSelectedBoneIndices | 提供 | ビュー |  |
| CAP-098 | PmxView | IPXPmxViewConnector.GetSelectedBodyIndices | 提供 | ビュー |  |
| CAP-099 | PmxView | IPXPmxViewConnector.SetSelectedBodyIndices | 提供 | ビュー |  |
| CAP-100 | PmxView | IPXPmxViewConnector.GetSelectedJointIndices | 提供 | ビュー |  |
| CAP-101 | PmxView | IPXPmxViewConnector.SetSelectedJointIndices | 提供 | ビュー |  |
| CAP-102 | PmxView | IPXPmxViewConnector.BodyVisible | 提供 | ビュー |  |
| CAP-103 | PmxView | IPXPmxViewConnector.SelectedBodyIndex | 提供 | ビュー |  |
| CAP-104 | PmxView | IPXPmxViewConnector.SelectedJointIndex | 提供 | ビュー |  |
| CAP-105 | PmxView | IPXPmxViewConnector.CameraRotateCenter | 提供 | ビュー |  |
| CAP-106 | PmxView | IPXPmxViewConnector.CameraTarget | 提供 | ビュー |  |
| CAP-107 | PmxView | IPXPmxViewConnector.CameraPosition | 提供 | ビュー |  |
| CAP-108 | PmxView | IPXPmxViewConnector.CameraUpVector | 提供 | ビュー |  |
| CAP-109 | PmxView | IPXPmxViewConnector.SetCameraView | 提供 | ビュー |  |
| CAP-110 | PmxView | IPXPmxViewConnector.IsShaderMode | 提供 | ビュー |  |
| CAP-111 | PmxView | IPXPmxViewConnector.GetViewAxis | 提供 | ビュー |  |
| CAP-112 | PmxView | IPXPmxViewConnector.EnableHandleEdit | 提供 | ビュー |  |
| CAP-113 | PmxView | IPXPmxViewConnector.IsVmdViewBootup | 提供 | 変形・モーション |  |
| CAP-114 | PmxView | IPXPmxViewConnector.BootupVmdView | 提供 | 変形・モーション | PMX+VMD版と引数なし版を対象。PMDを引数に取る版はレガシーのため対象外 |
| CAP-115 | PmxView | IPXPmxViewConnector.PlayVmdView | 提供 | 変形・モーション |  |
| CAP-116 | PmxView | IPXPmxViewConnector.StopVmdView | 提供 | 変形・モーション |  |
| CAP-117 | PmxView | IPXPmxViewConnector.ShowBoneVmdView | 提供 | 変形・モーション |  |
| CAP-118 | PmxView | IPXPmxViewConnector.EnableCameraVmdView | 提供 | 変形・モーション |  |
| CAP-119 | PmxView | IPXPmxViewConnector.SetVmeEvent | 提供 | 変形・モーション | IPEVme版とIPEVmeResult版の2種。IPEVme版は読込不可型IPEVme(要調査)の調査結果に連動 |
| CAP-120 | PmxView | IPXPmxViewConnector.GetClientImage | 提供 | ビュー |  |
| CAP-121 | PmxView | IPXPmxViewConnector.Visible | 提供 | ビュー |  |
| CAP-122 | PmxView | IPXPmxViewConnector.Location | 提供 | ビュー |  |
| CAP-123 | PmxView | IPXPmxViewConnector.Focus | 提供 | ビュー |  |
| CAP-124 | PmxView | IPXPmxViewConnector.Size | 提供 | ビュー |  |
| CAP-125 | PmxView | IPXPmxViewConnector.WindowState | 提供 | ビュー |  |
| CAP-126 | PmxView | IPXPmxViewConnector.GetBodyVisibles | 提供 | ビュー |  |
| CAP-127 | PmxView | IPXPmxViewConnector.SetBodyVisibles | 提供 | ビュー |  |
| CAP-128 | PmxView | IPXPmxViewConnector.GetJointVisibles | 提供 | ビュー |  |
| CAP-129 | PmxView | IPXPmxViewConnector.SetJointVisibles | 提供 | ビュー |  |
| CAP-130 | ビュー設定 | IPEViewSettingConnector.SelectedTabPage | 提供 | ビュー |  |
| CAP-131 | ビュー設定 | IPEViewSettingConnector.BackColor | 提供 | ビュー |  |
| CAP-132 | ビュー設定 | IPEViewSettingConnector.AmbientColor | 提供 | ビュー |  |
| CAP-133 | ビュー設定 | IPEViewSettingConnector.LightColor | 提供 | ビュー |  |
| CAP-134 | ビュー設定 | IPEViewSettingConnector.LightDirection | 提供 | ビュー |  |
| CAP-135 | ビュー設定 | IPEViewSettingConnector.InitializeLight | 提供 | ビュー |  |
| CAP-136 | ビュー設定 | IPEViewSettingConnector.Visible_Bone | 提供 | ビュー |  |
| CAP-137 | ビュー設定 | IPEViewSettingConnector.Visible_Vertex | 提供 | ビュー |  |
| CAP-138 | ビュー設定 | IPEViewSettingConnector.Visible_SelectedVertex | 提供 | ビュー |  |
| CAP-139 | ビュー設定 | IPEViewSettingConnector.Visible_UnvisibleVertex | 提供 | ビュー |  |
| CAP-140 | ビュー設定 | IPEViewSettingConnector.Visible_SelectedFace | 提供 | ビュー |  |
| CAP-141 | ビュー設定 | IPEViewSettingConnector.Visible_Normal | 提供 | ビュー |  |
| CAP-142 | ビュー設定 | IPEViewSettingConnector.Visible_SelectedNormal | 提供 | ビュー |  |
| CAP-143 | ビュー設定 | IPEViewSettingConnector.Visible_Body | 提供 | ビュー |  |
| CAP-144 | ビュー設定 | IPEViewSettingConnector.Visible_SolidBody | 提供 | ビュー |  |
| CAP-145 | ビュー設定 | IPEViewSettingConnector.Visible_Joint | 提供 | ビュー |  |
| CAP-146 | ビュー設定 | IPEViewSettingConnector.Visible_WeightMap | 提供 | ビュー |  |
| CAP-147 | ビュー設定 | IPEViewSettingConnector.OnlyWeighting | 提供 | ビュー |  |
| CAP-148 | ビュー設定 | IPEViewSettingConnector.NonWeightingVertexColor | 提供 | ビュー |  |
| CAP-149 | ビュー設定 | IPEViewSettingConnector.ModelSize | 提供 | ビュー |  |
| CAP-150 | ビュー設定 | IPEViewSettingConnector.Perspective | 提供 | ビュー |  |
| CAP-151 | ビュー設定 | IPEViewSettingConnector.AAType | 提供 | ビュー |  |
| CAP-152 | ビュー設定 | IPEViewSettingConnector.ColorBlending | 提供 | ビュー |  |
| CAP-153 | ビュー設定 | IPEViewSettingConnector.FillMode | 提供 | ビュー |  |
| CAP-154 | ビュー設定 | IPEViewSettingConnector.VertexPointSize | 提供 | ビュー |  |
| CAP-155 | ビュー設定 | IPEViewSettingConnector.VertexPointColor | 提供 | ビュー |  |
| CAP-156 | ビュー設定 | IPEViewSettingConnector.SelectedVertexPointColor | 提供 | ビュー |  |
| CAP-157 | ビュー設定 | IPEViewSettingConnector.NormalLength | 提供 | ビュー |  |
| CAP-158 | ビュー設定 | IPEViewSettingConnector.NormalColor | 提供 | ビュー |  |
| CAP-159 | ビュー設定 | IPEViewSettingConnector.SelectedNormalColor | 提供 | ビュー |  |
| CAP-160 | ビュー設定 | IPEViewSettingConnector.JointPointSize | 提供 | ビュー |  |
| CAP-161 | ビュー設定 | IPEViewSettingConnector.ToonType | 提供 | ビュー |  |
| CAP-162 | ビュー設定 | IPEViewSettingConnector.Edge | 提供 | ビュー |  |
| CAP-163 | ビュー設定 | IPEViewSettingConnector.EdgeSize | 提供 | ビュー |  |
| CAP-164 | ビュー設定 | IPEViewSettingConnector.InitializeViewSetting | 提供 | ビュー |  |
| CAP-165 | ビュー設定 | IPEViewSettingConnector.LoadViewSetting | 提供 | ビュー |  |
| CAP-166 | ビュー設定 | IPEViewSettingConnector.SaveViewSetting | 提供 | ビュー |  |
| CAP-167 | TransformView | IPETransformViewConnector.UpdateView | 提供 | 変形・モーション |  |
| CAP-168 | TransformView | IPETransformViewConnector.GetClientImage | 提供 | 変形・モーション |  |
| CAP-169 | TransformView | IPETransformViewConnector.ResetTransform | 提供 | 変形・モーション |  |
| CAP-170 | TransformView | IPETransformViewConnector.SelectedBoneIndex | 提供 | 変形・モーション |  |
| CAP-171 | TransformView | IPETransformViewConnector.BoneRotate_XYZ | 提供 | 変形・モーション |  |
| CAP-172 | TransformView | IPETransformViewConnector.BoneTranslate_XYZ | 提供 | 変形・モーション |  |
| CAP-173 | TransformView | IPETransformViewConnector.BoneScale_XYZ | 提供 | 変形・モーション |  |
| CAP-174 | TransformView | IPETransformViewConnector.BoneRotate | 提供 | 変形・モーション |  |
| CAP-175 | TransformView | IPETransformViewConnector.BoneTranslate | 提供 | 変形・モーション |  |
| CAP-176 | TransformView | IPETransformViewConnector.BoneScaling | 提供 | 変形・モーション |  |
| CAP-177 | TransformView | IPETransformViewConnector.SelectedMorphIndex | 提供 | 変形・モーション |  |
| CAP-178 | TransformView | IPETransformViewConnector.MorphValue | 提供 | 変形・モーション |  |
| CAP-179 | TransformView | IPETransformViewConnector.MorphChecker | 提供 | 変形・モーション |  |
| CAP-180 | TransformView | IPETransformViewConnector.SetVpd | 提供 | 変形・モーション |  |
| CAP-181 | TransformView | IPETransformViewConnector.SetVpdFromText | 提供 | 変形・モーション |  |
| CAP-182 | 頂点編集補助 | IPEVertexEditConnector.EditObject | 提供 | モデル |  |
| CAP-183 | 頂点編集補助 | IPEVertexEditConnector.MoveOffset | 提供 | モデル |  |
| CAP-184 | 頂点編集補助 | IPEVertexEditConnector.RotateOffset | 提供 | モデル |  |
| CAP-185 | 頂点編集補助 | IPEVertexEditConnector.ScaleOffset | 提供 | モデル |  |
| CAP-186 | 頂点編集補助 | IPEVertexEditConnector.RotateNormalOffset | 提供 | モデル |  |
| CAP-187 | 頂点編集補助 | IPEVertexEditConnector.NormalAxisMoveOffset | 提供 | モデル |  |
| CAP-188 | 頂点編集補助 | IPEVertexEditConnector.ControlYDirection | 提供 | モデル |  |
| CAP-189 | 頂点編集補助 | IPEVertexEditConnector.NormalControlXDirection | 提供 | モデル |  |
| CAP-190 | 頂点編集補助 | IPEVertexEditConnector.CenterOrigin | 提供 | モデル |  |
| CAP-191 | 頂点編集補助 | IPEVertexEditConnector.MirrorMode | 提供 | モデル |  |
| CAP-192 | 頂点編集補助 | IPEVertexEditConnector.MoveValue | 提供 | モデル |  |
| CAP-193 | 頂点編集補助 | IPEVertexEditConnector.RotateValue | 提供 | モデル |  |
| CAP-194 | 頂点編集補助 | IPEVertexEditConnector.ScaleValue | 提供 | モデル |  |
| CAP-195 | 頂点編集補助 | IPEVertexEditConnector.CenterValue | 提供 | モデル |  |
| CAP-196 | 頂点編集補助 | IPEVertexEditConnector.Move | 提供 | モデル |  |
| CAP-197 | 頂点編集補助 | IPEVertexEditConnector.Rotate | 提供 | モデル |  |
| CAP-198 | 頂点編集補助 | IPEVertexEditConnector.Scaling | 提供 | モデル |  |
| CAP-199 | 頂点編集補助 | IPEVertexEditConnector.RotateNormalValue | 提供 | モデル |  |
| CAP-200 | 頂点編集補助 | IPEVertexEditConnector.RotateNormal | 提供 | モデル |  |
| CAP-201 | 頂点編集補助 | IPEVertexEditConnector.MoveNormalAxisValue | 提供 | モデル |  |
| CAP-202 | 頂点編集補助 | IPEVertexEditConnector.MoveNormalAxis | 提供 | モデル |  |
| CAP-203 | 頂点編集補助 | IPEVertexEditConnector.GetVertexMemory | 提供 | モデル |  |
| CAP-204 | 頂点編集補助 | IPEVertexEditConnector.SetVertexMemory | 提供 | モデル |  |
| CAP-205 | ウェイト編集補助 | IPEWeightEditConnector.Drawing | 提供 | モデル |  |
| CAP-206 | ウェイト編集補助 | IPEWeightEditConnector.BeginDraw | 提供 | モデル |  |
| CAP-207 | ウェイト編集補助 | IPEWeightEditConnector.EndDraw | 提供 | モデル |  |
| CAP-208 | ウェイト編集補助 | IPEWeightEditConnector.DrawSize | 提供 | モデル |  |
| CAP-209 | ウェイト編集補助 | IPEWeightEditConnector.IsGradation | 提供 | モデル |  |
| CAP-210 | ウェイト編集補助 | IPEWeightEditConnector.WeightValue | 提供 | モデル |  |
| CAP-211 | ウェイト編集補助 | IPEWeightEditConnector.Spray | 提供 | モデル |  |
| CAP-212 | ウェイト編集補助 | IPEWeightEditConnector.SprayOn | 提供 | モデル |  |
| CAP-213 | ウェイト編集補助 | IPEWeightEditConnector.SprayOff | 提供 | モデル |  |
| CAP-214 | ウェイト編集補助 | IPEWeightEditConnector.SprayPower | 提供 | モデル |  |
| CAP-215 | ウェイト編集補助 | IPEWeightEditConnector.SprayInterval | 提供 | モデル |  |
| CAP-216 | ウェイト編集補助 | IPEWeightEditConnector.SprayOffset | 提供 | モデル |  |
| CAP-217 | 絞り込み表示 | IPEPartsSelectConnector.SelectObject | 提供 | ビュー |  |
| CAP-218 | 絞り込み表示 | IPEPartsSelectConnector.RangeBegin | 提供 | ビュー |  |
| CAP-219 | 絞り込み表示 | IPEPartsSelectConnector.RangeEnd | 提供 | ビュー |  |
| CAP-220 | 絞り込み表示 | IPEPartsSelectConnector.MaterialItemsCount | 提供 | ビュー |  |
| CAP-221 | 絞り込み表示 | IPEPartsSelectConnector.BoneItemsCount | 提供 | ビュー |  |
| CAP-222 | 絞り込み表示 | IPEPartsSelectConnector.ExpressionItemsCount | 提供 | ビュー |  |
| CAP-223 | 絞り込み表示 | IPEPartsSelectConnector.SelectedMaterialIndex | 提供 | ビュー |  |
| CAP-224 | 絞り込み表示 | IPEPartsSelectConnector.SelectedBoneIndex | 提供 | ビュー |  |
| CAP-225 | 絞り込み表示 | IPEPartsSelectConnector.SelectedExpressionIndex | 提供 | ビュー |  |
| CAP-226 | 絞り込み表示 | IPEPartsSelectConnector.GetCheckedMaterialIndices | 提供 | ビュー |  |
| CAP-227 | 絞り込み表示 | IPEPartsSelectConnector.SetCheckedMaterialIndices | 提供 | ビュー |  |
| CAP-228 | 絞り込み表示 | IPEPartsSelectConnector.GetCheckedBoneIndices | 提供 | ビュー |  |
| CAP-229 | 絞り込み表示 | IPEPartsSelectConnector.SetCheckedBoneIndices | 提供 | ビュー |  |
| CAP-230 | 絞り込み表示 | IPEPartsSelectConnector.GetCheckedExpressionIndices | 提供 | ビュー |  |
| CAP-231 | 絞り込み表示 | IPEPartsSelectConnector.SetCheckedExpressionIndices | 提供 | ビュー |  |
| CAP-232 | 絞り込み表示 | IPEPartsSelectConnector.SelectedPartsVisible | 提供 | ビュー |  |
| CAP-233 | 絞り込み表示 | IPEPartsSelectConnector.BoneSelected | 提供 | ビュー |  |
| CAP-234 | 頂点ガイド | IPEVertexGuideConnector.GetSelectedCurrentVertex | 提供 | ビュー |  |
| CAP-235 | 頂点ガイド | IPEVertexGuideConnector.SelectVertex | 提供 | ビュー |  |
| CAP-236 | 頂点ガイド | IPEVertexGuideConnector.GetVertexIndices | 提供 | ビュー |  |
| CAP-237 | 頂点ガイド | IPEVertexGuideConnector.SetVertexIndices | 提供 | ビュー |  |
| CAP-238 | サブビュー | IPESubViewConnector.UpdateView | 提供 | ビュー |  |
| CAP-239 | サブビュー | IPESubViewConnector.GetClientImage | 提供 | ビュー |  |
| CAP-240 | モデルデータ型 | IPXBody | 要調査 |  | シグネチャを解決できなかったメンバーの変換可否を実機確認: BoxSize, Position, Rotation |
| CAP-241 | モデルデータ型 | IPXBone | 要調査 |  | シグネチャを解決できなかったメンバーの変換可否を実機確認: Position, ToOffset, FixAxis, SetLocalAxis, GetLocalAxis |
| CAP-242 | モデルデータ型 | IPXBoneMorphOffset | 要調査 |  | シグネチャを解決できなかったメンバーの変換可否を実機確認: Translation, Rotation |
| CAP-243 | モデルデータ型 | IPXBoneNodeItem | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-244 | モデルデータ型 | IPXFace | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-245 | モデルデータ型 | IPXGroupMorphOffset | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-246 | モデルデータ型 | IPXHeader | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-247 | モデルデータ型 | IPXIK | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-248 | モデルデータ型 | IPXIKLink | 要調査 |  | シグネチャを解決できなかったメンバーの変換可否を実機確認: Low, High |
| CAP-249 | モデルデータ型 | IPXImpulseMorphOffset | 要調査 |  | シグネチャを解決できなかったメンバーの変換可否を実機確認: Velocity, Torque |
| CAP-250 | モデルデータ型 | IPXJoint | 要調査 |  | シグネチャを解決できなかったメンバーの変換可否を実機確認: Position, Rotation, Limit_MoveLow, Limit_MoveHigh, Limit_AngleLow, Limit_AngleHigh等 |
| CAP-251 | モデルデータ型 | IPXMaterial | 要調査 |  | シグネチャを解決できなかったメンバーの変換可否を実機確認: Diffuse, Specular, Ambient, EdgeColor |
| CAP-252 | モデルデータ型 | IPXMaterialMorphOffset | 要調査 |  | シグネチャを解決できなかったメンバーの変換可否を実機確認: Diffuse, Specular, Ambient, EdgeColor, Tex, Sphere等 |
| CAP-253 | モデルデータ型 | IPXModelInfo | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-254 | モデルデータ型 | IPXMorph | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-255 | モデルデータ型 | IPXMorphNodeItem | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-256 | モデルデータ型 | IPXMorphOffset | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-257 | モデルデータ型 | IPXNode | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-258 | モデルデータ型 | IPXNodeItem | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-259 | モデルデータ型 | IPXPmx | 提供 | モデル | 全公開メンバー(型単位)。FromStream/ToStreamはファイルパス版で代替し対象外 |
| CAP-260 | モデルデータ型 | IPXSoftBody | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-261 | モデルデータ型 | IPXSoftBodyAnchor | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-262 | モデルデータ型 | IPXUVMorphOffset | 要調査 |  | シグネチャを解決できなかったメンバーの変換可否を実機確認: Offset |
| CAP-263 | モデルデータ型 | IPXVertex | 要調査 |  | シグネチャを解決できなかったメンバーの変換可否を実機確認: Position, Normal, UV, UVA1, UVA2, UVA3等 |
| CAP-264 | モデルデータ型 | IPXVertexMorphOffset | 要調査 |  | シグネチャを解決できなかったメンバーの変換可否を実機確認: Offset |
| CAP-265 | PMXビルダ | IPXPmxBuilder.Pmx | 提供 | モデル |  |
| CAP-266 | PMXビルダ | IPXPmxBuilder.Vertex | 要調査 |  | 生成対象型 IPXVertex(要調査)の調査結果に連動 |
| CAP-267 | PMXビルダ | IPXPmxBuilder.Face | 提供 | モデル |  |
| CAP-268 | PMXビルダ | IPXPmxBuilder.Material | 要調査 |  | 生成対象型 IPXMaterial(要調査)の調査結果に連動 |
| CAP-269 | PMXビルダ | IPXPmxBuilder.Bone | 要調査 |  | 生成対象型 IPXBone(要調査)の調査結果に連動 |
| CAP-270 | PMXビルダ | IPXPmxBuilder.IKLink | 要調査 |  | 生成対象型 IPXIKLink(要調査)の調査結果に連動 |
| CAP-271 | PMXビルダ | IPXPmxBuilder.Morph | 提供 | モデル |  |
| CAP-272 | PMXビルダ | IPXPmxBuilder.VertexMorphOffset | 要調査 |  | 生成対象型 IPXVertexMorphOffset(要調査)の調査結果に連動 |
| CAP-273 | PMXビルダ | IPXPmxBuilder.UVMorphOffset | 要調査 |  | 生成対象型 IPXUVMorphOffset(要調査)の調査結果に連動 |
| CAP-274 | PMXビルダ | IPXPmxBuilder.BoneMorphOffset | 要調査 |  | 生成対象型 IPXBoneMorphOffset(要調査)の調査結果に連動 |
| CAP-275 | PMXビルダ | IPXPmxBuilder.MaterialMorphOffset | 要調査 |  | 生成対象型 IPXMaterialMorphOffset(要調査)の調査結果に連動 |
| CAP-276 | PMXビルダ | IPXPmxBuilder.GroupMorphOffset | 提供 | モデル |  |
| CAP-277 | PMXビルダ | IPXPmxBuilder.ImpulseMorphOffset | 要調査 |  | 生成対象型 IPXImpulseMorphOffset(要調査)の調査結果に連動 |
| CAP-278 | PMXビルダ | IPXPmxBuilder.Node | 提供 | モデル |  |
| CAP-279 | PMXビルダ | IPXPmxBuilder.BoneNodeItem | 提供 | モデル |  |
| CAP-280 | PMXビルダ | IPXPmxBuilder.MorphNodeItem | 提供 | モデル |  |
| CAP-281 | PMXビルダ | IPXPmxBuilder.Body | 要調査 |  | 生成対象型 IPXBody(要調査)の調査結果に連動 |
| CAP-282 | PMXビルダ | IPXPmxBuilder.Joint | 要調査 |  | 生成対象型 IPXJoint(要調査)の調査結果に連動 |
| CAP-283 | PMXビルダ | IPXPmxBuilder.SoftBody | 提供 | モデル |  |
| CAP-284 | PMXビルダ | IPXPmxBuilder.SoftBodyAnchor | 提供 | モデル |  |
| CAP-285 | プリミティブ | IPXPrimitiveBuilder.AddPlane | 要調査 |  | シグネチャを解決できなかったため実機確認 |
| CAP-286 | プリミティブ | IPXPrimitiveBuilder.AddBox | 要調査 |  | シグネチャを解決できなかったため実機確認 |
| CAP-287 | プリミティブ | IPXPrimitiveBuilder.AddSphere | 要調査 |  | シグネチャを解決できなかったため実機確認 |
| CAP-288 | プリミティブ | IPXPrimitiveBuilder.AddCylinder | 要調査 |  | シグネチャを解決できなかったため実機確認 |
| CAP-289 | プリミティブ | IPXPrimitiveBuilder.AddTorus | 要調査 |  | シグネチャを解決できなかったため実機確認 |
| CAP-290 | プリミティブ | IPXPrimitiveBuilder.AddText | 要調査 |  | シグネチャを解決できなかったため実機確認 |
| CAP-291 | VMD/VMEビルダ | IPEBuilder.CreateVmd | 提供 | 変形・モーション | PMDを引数に取る版はレガシーのため対象外。他のオーバーロードを提供 |
| CAP-292 | VMD/VMEビルダ | IPEBuilder.CreateVmdIPL | 提供 | 変形・モーション |  |
| CAP-293 | VMD/VMEビルダ | IPEBuilder.CreateVmdBoneKey | 提供 | 変形・モーション |  |
| CAP-294 | VMD/VMEビルダ | IPEBuilder.CreateVmdMorphKey | 提供 | 変形・モーション |  |
| CAP-295 | VMD/VMEビルダ | IPEBuilder.CreateVmdBasCameraKey | 提供 | 変形・モーション |  |
| CAP-296 | VMD/VMEビルダ | IPEBuilder.CreateVmdLightKey | 提供 | 変形・モーション |  |
| CAP-297 | VMD/VMEビルダ | IPEBuilder.CreateVmdSelfShadowKey | 提供 | 変形・モーション |  |
| CAP-298 | VMD/VMEビルダ | IPEBuilder.CreateVmdBonePoseState | 要調査 |  | 生成対象型 IPEVmdBonePoseState(要調査)の調査結果に連動 |
| CAP-299 | VMD/VMEビルダ | IPEBuilder.CreateVme | 要調査 |  | PMDを引数に取る版はレガシーのため対象外。生成対象型 IPEVme(読込不可・要調査)の調査結果に連動 |
| CAP-300 | VMD/VMEビルダ | IPEBuilder.CreateVmeGroup | 提供 | 変形・モーション | デリゲートは直接渡せないため宣言的な記述からの変換設計を前提に提供 |
| CAP-301 | VMD/VMEビルダ | IPEBuilder.CreateVmePath | 要調査 |  | 生成対象型 IPEVmePath(読込不可・要調査)の調査結果に連動 |
| CAP-302 | VMDデータ型 | IPEVmd | 提供 | 変形・モーション | 全公開メンバー(型単位)。Stream入出力メンバーはファイルパス版で代替し対象外 |
| CAP-303 | VMDデータ型 | IPEVmdBoneKey | 提供 | 変形・モーション | 全公開メンバーへのアクセス(型単位) |
| CAP-304 | VMDデータ型 | IPEVmdBonePoseState | 要調査 |  | シグネチャを解決できなかったメンバーの変換可否を実機確認: FromPoseArray, ToPoseArray, GetPose |
| CAP-305 | VMDデータ型 | IPEVmdCameraKey | 提供 | 変形・モーション | 全公開メンバーへのアクセス(型単位) |
| CAP-306 | VMDデータ型 | IPEVmdFrameKey | 提供 | 変形・モーション | 全公開メンバーへのアクセス(型単位) |
| CAP-307 | VMDデータ型 | IPEVmdIKEnable | 提供 | 変形・モーション | 全公開メンバーへのアクセス(型単位) |
| CAP-308 | VMDデータ型 | IPEVmdIPL | 提供 | 変形・モーション | 全公開メンバーへのアクセス(型単位) |
| CAP-309 | VMDデータ型 | IPEVmdLightKey | 提供 | 変形・モーション | 全公開メンバーへのアクセス(型単位) |
| CAP-310 | VMDデータ型 | IPEVmdMorphKey | 提供 | 変形・モーション | 全公開メンバーへのアクセス(型単位) |
| CAP-311 | VMDデータ型 | IPEVmdSelfShadowKey | 提供 | 変形・モーション | 全公開メンバーへのアクセス(型単位) |
| CAP-312 | VMDデータ型 | IPEVmdVisibleIKKey | 提供 | 変形・モーション | 全公開メンバーへのアクセス(型単位) |
| CAP-313 | VMEデータ型 | IPEVmeBoneState | 提供 | 変形・モーション | デリゲートを取るメンバーは宣言的な記述からの変換設計を前提に提供 |
| CAP-314 | VMEデータ型 | IPEVmeCameraState | 提供 | 変形・モーション | デリゲートを取るメンバーは宣言的な記述からの変換設計を前提に提供 |
| CAP-315 | VMEデータ型 | IPEVmeElement | 提供 | 変形・モーション | デリゲートを取るメンバーは宣言的な記述からの変換設計を前提に提供 |
| CAP-316 | VMEデータ型 | IPEVmeEventElement | 提供 | 変形・モーション | デリゲートを取るメンバーは宣言的な記述からの変換設計を前提に提供 |
| CAP-317 | VMEデータ型 | IPEVmeEventState | 提供 | 変形・モーション | デリゲートを取るメンバーは宣言的な記述からの変換設計を前提に提供 |
| CAP-318 | VMEデータ型 | IPEVmeFrameEvent | 提供 | 変形・モーション | デリゲートを取るメンバーは宣言的な記述からの変換設計を前提に提供 |
| CAP-319 | VMEデータ型 | IPEVmeGroup | 提供 | 変形・モーション | デリゲートを取るメンバーは宣言的な記述からの変換設計を前提に提供 |
| CAP-320 | VMEデータ型 | IPEVmeGroupBone | 提供 | 変形・モーション | デリゲートを取るメンバーは宣言的な記述からの変換設計を前提に提供 |
| CAP-321 | VMEデータ型 | IPEVmeGroupMorph | 提供 | 変形・モーション | デリゲートを取るメンバーは宣言的な記述からの変換設計を前提に提供 |
| CAP-322 | VMEデータ型 | IPEVmeLight | 提供 | 変形・モーション | デリゲートを取るメンバーは宣言的な記述からの変換設計を前提に提供 |
| CAP-323 | VMEデータ型 | IPEVmeLightState | 提供 | 変形・モーション | デリゲートを取るメンバーは宣言的な記述からの変換設計を前提に提供 |
| CAP-324 | VMEデータ型 | IPEVmeMorphResult | 提供 | 変形・モーション | デリゲートを取るメンバーは宣言的な記述からの変換設計を前提に提供 |
| CAP-325 | VMEデータ型 | IPEVmeObject | 提供 | 変形・モーション | デリゲートを取るメンバーは宣言的な記述からの変換設計を前提に提供 |
| CAP-326 | VMEデータ型 | IPEVmePrimaryValue`1 | 提供 | 変形・モーション | デリゲートを取るメンバーは宣言的な記述からの変換設計を前提に提供 |
| CAP-327 | VMEデータ型 | IPEVmeResult | 提供 | 変形・モーション | デリゲートを取るメンバーは宣言的な記述からの変換設計を前提に提供 |
| CAP-328 | VMEデータ型 | IPEVmeSingleValue | 提供 | 変形・モーション | デリゲートを取るメンバーは宣言的な記述からの変換設計を前提に提供 |
| CAP-329 | VMEデータ型 | IPEVmeSingleValueElement | 提供 | 変形・モーション | デリゲートを取るメンバーは宣言的な記述からの変換設計を前提に提供 |
| CAP-330 | VMEデータ型 | IPEVmeSingleValueEventOperator | 提供 | 変形・モーション | デリゲートを取るメンバーは宣言的な記述からの変換設計を前提に提供 |
| CAP-331 | VMEデータ型 | IPEVmeSingleValueOperator | 提供 | 変形・モーション | デリゲートを取るメンバーは宣言的な記述からの変換設計を前提に提供 |
| CAP-332 | VMEデータ型 | IPEVmeSingleValueState | 提供 | 変形・モーション | デリゲートを取るメンバーは宣言的な記述からの変換設計を前提に提供 |
| CAP-333 | プラグイン機構 | IPEPlugin / PEPluginClass / PEPluginOption / IPERunArgs / PECheckResult | 非対応 |  | プラグイン自身がホストに登録されるための実装専用API |
| CAP-334 | プラグイン情報 | IPERegisteredPluginInfo / IPEPluginOption | 提供 | セッション | GetPluginInfoの結果として返す読み取り用データ |
| CAP-335 | ビルダ別経路 | PEStaticBuilder / IPEShortBuilder | 非対応 |  | IPXPmxBuilder等の提供経路と重複する短絡経路のため |
| CAP-336 | プラグイン拡張点 | IPECheckerPlugin / IPEImportPlugin / IPEExportPlugin | 非対応 |  | プラグインDLL側の拡張点(MCPからの呼び出し対象ではない) |
| CAP-337 | PMDレガシー | PEPlugin.Pmd.* のコネクタ・データ型と IPEBuilder のPMD/X系生成 | 非対応 |  | PMX系に同等機能。PMDファイル入出力はFormコネクタの能力として提供 |
| CAP-338 | ビューヘルパ | IPEObjectSelectConnector / IPEExtensionEditConnector | 要調査 |  | 公開メンバーがウィンドウ操作のみ。実機で用途確認 |
| CAP-339 | Cプラグイン連携 | PXCPlugin.* の全型 | 要調査 |  | Cプラグイン実行引数・選択状態操作の型。資料が乏しく実機確認 |
| CAP-340 | SDX(読込不可型) | M | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-341 | SDX(読込不可型) | Q | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-342 | SDX(読込不可型) | V2 | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-343 | SDX(読込不可型) | V3 | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-344 | SDX(読込不可型) | V4 | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-345 | Vmd(読込不可型) | PEVmdBonePose | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-346 | Vme(読込不可型) | IPEVme | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-347 | Vme(読込不可型) | IPEVmeBone | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-348 | Vme(読込不可型) | IPEVmeBoneResult | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-349 | Vme(読込不可型) | IPEVmeCamera | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-350 | Vme(読込不可型) | IPEVmeCameraPosition | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-351 | Vme(読込不可型) | IPEVmeCameraResult | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-352 | Vme(読込不可型) | IPEVmeDirection | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-353 | Vme(読込不可型) | IPEVmeDirectionEventOperator | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-354 | Vme(読込不可型) | IPEVmeDirectionOperator | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-355 | Vme(読込不可型) | IPEVmeLightResult | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-356 | Vme(読込不可型) | IPEVmePath | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-357 | Vme(読込不可型) | IPEVmePosition | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-358 | Vme(読込不可型) | IPEVmePositionEventOperator | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-359 | Vme(読込不可型) | IPEVmePositionOperator | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-360 | Vme(読込不可型) | IPEVmeQuaternionValue | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-361 | Vme(読込不可型) | IPEVmeQuaternionValueEventOperator | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-362 | Vme(読込不可型) | IPEVmeQuaternionValueOperator | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-363 | Vme(読込不可型) | IPEVmeScale | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-364 | Vme(読込不可型) | IPEVmeScalingEventOperator | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-365 | Vme(読込不可型) | IPEVmeScalingOperator | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-366 | Vme(読込不可型) | IPEVmeVectorValue | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-367 | Vme(読込不可型) | IPEVmeVectorValueEventOperator | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
| CAP-368 | Vme(読込不可型) | IPEVmeVectorValueOperator | 要調査 |  | リフレクションで型を読み込めない(依存型の解決失敗)。メンバー構成をドキュメントXMLで確認し実機で分類を確定 |
