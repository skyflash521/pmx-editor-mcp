# PEPlugin SDK 能力台帳

PMXエディタ配布物(PmxEditor_0273)の PEPlugin.dll(v0.0.8.9、.NET Framework 4.0)の
公開APIをリフレクションで列挙し、能力単位に集約した台帳。pmx-editor-mcp がツール化する
対象の正本とする。ツール契約仕様書はこの台帳の「担当」列に従って契約を定める。

## 実機確認で確かめたこと

本台帳の分類は、MCPクライアントからPEPlugin APIまでの経路が実機で成立することを前提と
する。この経路は検証専用のプラグインとブリッジで実測済みであり、確かめたのは次の各点で
ある(実測に用いたパイプ名などの値は検証専用であり、ツール契約の値ではない)。

- 配布物の `_plugin` フォルダへ置いたプラグインDLLが、エディタの起動時にロードされる。
  net48 を対象としたDLLが、.NET Framework の追加導入なしに実行できる。
- プラグインのプロセス内で名前付きパイプを待ち受け、外部プロセスからの要求に応答できる。
  待受はプラグインの常駐後も稼働し続ける。
- PEPlugin API の呼び出しは、不可視フォームの Invoke でワーカースレッドからUIスレッドへ
  到達できる。
- 同じパイプへ中継する標準入出力のサーバーをMCPサーバーとして登録すると、MCPクライアントの
  ツール呼び出しがエディタまで届き、応答が返る。モデル名と頂点数の取得まで往復した。

## 凡例

- **能力の単位**: コネクタの公開メソッド1つ=1能力。同名オーバーロードは1能力に
  まとめ、シグネチャによって可否が分かれる場合は備考に記す。プロパティはget/setで
  1能力。データ型は型ごとに1能力(全公開メンバーを含む)。ビルダ生成は生成対象型
  ごとに1能力。列挙型・引数専用型は数えない。コネクタへの経路となるだけの型
  (IPEConnector等)は行を作らず、能力は参照先コネクタで計上する。性質が同一で
  同じ理由により一括分類する型群(プラグイン機構・PMDレガシー等)は1行にまとめる。
- **分類**: 提供=ツール化する / 非対応=理由を備考に記載 / 要調査=実機確認(E2Eスパイク)で確定する
- **担当**: 分類が「提供」の能力を担当するツール契約仕様書。モデル / セッション / ビュー / 変形・モーション
- **契約注記**: 備考のうち、ツール説明文へ転記する利用上の制約は固定接頭辞「契約注記:」から
  始める。接頭辞から備考の末尾までが注記の本文で、複数あるときは句点で区切る。注記を持たない
  能力の備考には接頭辞を書かない。
- 実行時オブジェクト(デリゲート等)は直接は受け渡せない。VMEの操作は、数値引数の
  オーバーロードに対応する事前定義テンプレートを宣言的な記述として与える形で提供し、
  デリゲート・ラムダを要するシグネチャと任意式の評価はシグネチャ単位で非対応とする。
  重複経路やプラグイン機構専用のように、設計しても意味を持たないものも非対応とする。
- 危険な操作(エディタ終了・上書き保存等)も「技術的にツール化するか」で分類する。
  公開の可否・確認フローはアーキテクチャ仕様書の危険操作の公開方針が定める。

集計: 提供 465 / 非対応 7 / 要調査 0(計 472)

| ID | 大分類 | 対象 | 分類 | 担当 | 備考 |
|---|---|---|---|---|---|
| CAP-001 | PMXデータ | IPXPmxConnector.GetCurrentState | 提供 | モデル |  |
| CAP-002 | PMXデータ | IPXPmxConnector.CurrentPath | 提供 | セッション | 開いているファイルパスの取得・設定 |
| CAP-003 | PMXデータ | IPXPmxConnector.Update | 提供 | モデル | 全体更新と部分更新(単一/複数Index)のオーバーロードを含む |
| CAP-004 | PMXデータ | IPXPmxConnector.LockUndo | 提供 | セッション | Undo記録の制御 |
| CAP-005 | PMXデータ | IPXPmxConnector.UnlockUndo | 提供 | セッション | Undo記録の制御 |
| CAP-006 | 本体フォーム | IPEFormConnector.Close | 提供 | セッション | 契約注記: 危険操作。呼び出しにはアーキテクチャ仕様書の危険操作の公開方針が定める確認が要る |
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
| CAP-072 | システム | IPESystemConnector.SetShareObject | 提供 | セッション | 契約注記: JSONで表現できる値に限定。任意の.NETオブジェクトは対象外 |
| CAP-073 | システム | IPESystemConnector.GetShareObject | 提供 | セッション | 契約注記: JSONで表現できる値に限定。任意の.NETオブジェクトは対象外 |
| CAP-074 | システム | IPESystemConnector.RemoveShareObject | 提供 | セッション |  |
| CAP-075 | システム | IPESystemConnector.RegisteredCPluginCount | 提供 | セッション |  |
| CAP-076 | システム | IPESystemConnector.GetCPluginInfo | 提供 | セッション |  |
| CAP-077 | システム | IPESystemConnector.RunCPlugin | 提供 | セッション |  |
| CAP-078 | システム | IPESystemConnector.GetCPluginRunArgsClone | 提供 | セッション | Cプラグイン連携の各操作(モデル/セッション/ビュー)への取得経路。契約注記: 一次資料で利用非推奨 |
| CAP-079 | PmxView | IPXPmxViewConnector.GetViewMatrix | 提供 | ビュー |  |
| CAP-080 | PmxView | IPXPmxViewConnector.GetProjectionMatrix | 提供 | ビュー |  |
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
| CAP-114 | PmxView | IPXPmxViewConnector.BootupVmdView | 提供 | 変形・モーション | PMX+VMD版と引数なし版を対象。契約注記: PMDを引数に取る版はレガシーのため対象外 |
| CAP-115 | PmxView | IPXPmxViewConnector.PlayVmdView | 提供 | 変形・モーション |  |
| CAP-116 | PmxView | IPXPmxViewConnector.StopVmdView | 提供 | 変形・モーション |  |
| CAP-117 | PmxView | IPXPmxViewConnector.ShowBoneVmdView | 提供 | 変形・モーション |  |
| CAP-118 | PmxView | IPXPmxViewConnector.EnableCameraVmdView | 提供 | 変形・モーション |  |
| CAP-119 | PmxView | IPXPmxViewConnector.SetVmeEvent | 提供 | 変形・モーション | IPEVme版とIPEVmeResult版の2種。契約注記: VME記述は数値引数のテンプレートに対応する範囲で扱う |
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
| CAP-240 | Cプラグイン連携 | PXCBridge.Builder | 提供 | モデル | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-241 | Cプラグイン連携 | PXCBridge.BuilderInitialize | 提供 | モデル | Builder利用前の初期化。契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-242 | Cプラグイン連携 | PXCBridge.PrimitiveBuilder | 提供 | モデル | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-243 | Cプラグイン連携 | PXCBridge.PrimitiveBuilderInitialize | 提供 | モデル | PrimitiveBuilder利用前の初期化。契約注記: PrimitiveBuilderの利用前に呼ぶことが一次資料で必須。取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-244 | Cプラグイン連携 | PXCBridge.GetCurrentPmx | 提供 | モデル | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-245 | Cプラグイン連携 | PXCBridge.UpdatePmx | 提供 | モデル | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-246 | Cプラグイン連携 | PXCBridge.GetViewAxis | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-247 | Cプラグイン連携 | PXCBridge.ViewCtrl | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-248 | Cプラグイン連携 | PXCBridge.SystemCtrl | 提供 | セッション | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-249 | Cプラグイン連携 | PXCBridge.RegisterUIModel | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-250 | Cプラグイン連携 | PXCBridge.CreateEventConnector | 提供 | ビュー | 契約注記: イベントはホスト側がハンドラを登録してキューへ積む形で扱う。取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-251 | Cプラグイン連携 | PXCBridge.ReleaseEventConnector | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-252 | Cプラグイン連携 | IPXCPluginConnector.GetSelectedVertexIndices | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-253 | Cプラグイン連携 | IPXCPluginConnector.SetSelectedVertexIndices | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-254 | Cプラグイン連携 | IPXCPluginConnector.GetSelectedFaceIndices | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-255 | Cプラグイン連携 | IPXCPluginConnector.SetSelectedFaceIndices | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-256 | Cプラグイン連携 | IPXCPluginConnector.GetSelectedBoneIndices | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-257 | Cプラグイン連携 | IPXCPluginConnector.SetSelectedBoneIndices | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-258 | Cプラグイン連携 | IPXCPluginConnector.GetSelectedBodyIndices | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-259 | Cプラグイン連携 | IPXCPluginConnector.SetSelectedBodyIndices | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-260 | Cプラグイン連携 | IPXCPluginConnector.GetSelectedJointIndices | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-261 | Cプラグイン連携 | IPXCPluginConnector.SetSelectedJointIndices | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-262 | Cプラグイン連携 | IPXCPluginConnector.GetVisibleMaterialIndices | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-263 | Cプラグイン連携 | IPXSystemControl.PEPluginCount | 提供 | セッション | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-264 | Cプラグイン連携 | IPXSystemControl.FindPEPlugins | 提供 | セッション | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-265 | Cプラグイン連携 | IPXSystemControl.GetPEPluginInfo | 提供 | セッション | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-266 | Cプラグイン連携 | IPXSystemControl.RunPEPlugin | 提供 | セッション | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-267 | Cプラグイン連携 | IPXSystemControl.CPluginCount | 提供 | セッション | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-268 | Cプラグイン連携 | IPXSystemControl.FindCPlugins | 提供 | セッション | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-269 | Cプラグイン連携 | IPXSystemControl.GetCPluginInfo | 提供 | セッション | Int32版を提供。契約注記: IPXCPluginを引数に取る版は対象外。取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-270 | Cプラグイン連携 | IPXSystemControl.RunCPlugin | 提供 | セッション | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-271 | Cプラグイン連携 | IPXSystemControl.SetShareData | 提供 | セッション | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-272 | Cプラグイン連携 | IPXSystemControl.GetShareValue | 提供 | セッション | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-273 | Cプラグイン連携 | IPXSystemControl.GetShareText | 提供 | セッション | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-274 | Cプラグイン連携 | IPXSystemControl.GetShareBuffer | 提供 | セッション | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-275 | Cプラグイン連携 | IPXSystemControl.RemoveShareData | 提供 | セッション | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-276 | Cプラグイン連携 | IPXViewControl.UpdateView | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-277 | Cプラグイン連携 | IPXViewControl.ClientSize | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-278 | Cプラグイン連携 | IPXViewControl.ScreenPosition | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-279 | Cプラグイン連携 | IPXViewControl.ViewMatrix | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-280 | Cプラグイン連携 | IPXViewControl.ProjectionMatrix | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-281 | Cプラグイン連携 | IPXViewControl.Viewport | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-282 | Cプラグイン連携 | IPXViewControl.CameraRotateCenter | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-283 | Cプラグイン連携 | IPXViewControl.CameraTarget | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-284 | Cプラグイン連携 | IPXViewControl.CameraPosition | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-285 | Cプラグイン連携 | IPXViewControl.CameraUpVector | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-286 | Cプラグイン連携 | IPXViewControl.SetCameraParameter | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-287 | Cプラグイン連携 | IPXViewControl.VCursorPosition | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-288 | Cプラグイン連携 | IPXViewControl.ViewAxis | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-289 | Cプラグイン連携 | IPXViewControl.EnableVAxis | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-290 | Cプラグイン連携 | IPXViewControl.GetVAxis | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-291 | Cプラグイン連携 | IPXViewControl.SetVAxis | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-292 | Cプラグイン連携 | IPXViewControl.VAxisOrigin | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-293 | Cプラグイン連携 | IPXViewControl.GetBodyVisibles | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-294 | Cプラグイン連携 | IPXViewControl.SetBodyVisibles | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-295 | Cプラグイン連携 | IPXViewControl.GetJointVisibles | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-296 | Cプラグイン連携 | IPXViewControl.SetJointVisibles | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-297 | Cプラグイン連携 | IPXCPrimitiveBuilder.AddPlane | 提供 | モデル | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-298 | Cプラグイン連携 | IPXCPrimitiveBuilder.AddBox | 提供 | モデル | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-299 | Cプラグイン連携 | IPXCPrimitiveBuilder.AddSphere | 提供 | モデル | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-300 | Cプラグイン連携 | IPXCPrimitiveBuilder.AddCylinder | 提供 | モデル | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-301 | Cプラグイン連携 | IPXCPrimitiveBuilder.AddTorus | 提供 | モデル | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-302 | Cプラグイン連携 | IPXCPrimitiveBuilder.AddText | 提供 | モデル | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-303 | Cプラグイン連携 | IPXUIModel.Release | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-304 | Cプラグイン連携 | IPXUIModel.SetAutoRelease | 非対応 |  | 引数のIPXCPlugin(実装拡張点・非対応)を取得経路から得られないため |
| CAP-305 | Cプラグイン連携 | IPXUIModel.Name | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-306 | Cプラグイン連携 | IPXUIModel.Visible | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-307 | Cプラグイン連携 | IPXUIModel.DrawMode | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-308 | Cプラグイン連携 | IPXUIModel.Light | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-309 | Cプラグイン連携 | IPXUIModel.Depth | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-310 | Cプラグイン連携 | IPXUIModel.TopMost | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-311 | Cプラグイン連携 | IPXUIModel.FixedDrawScale | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-312 | Cプラグイン連携 | IPXUIModel.SetBillboard | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-313 | Cプラグイン連携 | IPXUIModel.SetWorld | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-314 | Cプラグイン連携 | IPXUIModel.GetWorld | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-315 | Cプラグイン連携 | IPXUIModel.SetBone | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-316 | Cプラグイン連携 | IPXUIModel.SetBoneScale | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-317 | Cプラグイン連携 | IPXUIModel.SetBoneRotate | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-318 | Cプラグイン連携 | IPXUIModel.SetBoneTranslate | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-319 | Cプラグイン連携 | IPXUIModel.ResetBone | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-320 | Cプラグイン連携 | IPXUIModel.SetMorph | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-321 | Cプラグイン連携 | IPXUIModel.ResetMorph | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-322 | Cプラグイン連携 | IPXUIModel.UpdateTransform | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-323 | Cプラグイン連携 | IPXUIModel.GetTransformedVertexPosition | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-324 | Cプラグイン連携 | IPXUIModel.GetTransformedVertexNormal | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-325 | Cプラグイン連携 | IPXUIModel.GetTransformedBonePosition | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-326 | Cプラグイン連携 | IPXUIModel.GetTransformedBoneMatrix | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-327 | Cプラグイン連携 | IPXUIModel.UpdateMaterialColor | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-328 | Cプラグイン連携 | IPXUIModel.UpdateMaterialEdge | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-329 | Cプラグイン連携 | IPXUIModel.UpdateMaterialFlags | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-330 | Cプラグイン連携 | IPXUIModel.SetBitmapTexture | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-331 | Cプラグイン連携 | IPXUIModel.UpdateBitmapTexture | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-332 | Cプラグイン連携 | IPXUIModel.CreateEventListener | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-333 | Cプラグイン連携 | IPXUIModel.ReleaseEventListener | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-334 | Cプラグイン連携 | PXUIModelHelper.SetMouseOverColor | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-335 | Cプラグイン連携 | PXUIModelHelper.SetMouseDragMove | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-336 | Cプラグイン連携 | PXUIModelHelper.CreateTextControl | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-337 | Cプラグイン連携 | IPXEventConnector.CreateViewEventListener | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-338 | Cプラグイン連携 | IPXEventConnector.ReleaseViewEventListener | 提供 | ビュー | 契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-339 | モデルデータ型 | IPXPmx | 提供 | モデル | 全公開メンバー(型単位)。契約注記: FromStream/ToStreamはファイルパス版で代替し対象外 |
| CAP-340 | モデルデータ型 | IPXHeader | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-341 | モデルデータ型 | IPXModelInfo | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-342 | モデルデータ型 | IPXVertex | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-343 | モデルデータ型 | IPXFace | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-344 | モデルデータ型 | IPXMaterial | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-345 | モデルデータ型 | IPXBone | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-346 | モデルデータ型 | IPXIK | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-347 | モデルデータ型 | IPXIKLink | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-348 | モデルデータ型 | IPXMorph | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-349 | モデルデータ型 | IPXMorphOffset | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-350 | モデルデータ型 | IPXVertexMorphOffset | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-351 | モデルデータ型 | IPXUVMorphOffset | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-352 | モデルデータ型 | IPXBoneMorphOffset | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-353 | モデルデータ型 | IPXImpulseMorphOffset | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-354 | モデルデータ型 | IPXMaterialMorphOffset | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-355 | モデルデータ型 | IPXGroupMorphOffset | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-356 | モデルデータ型 | IPXNode | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-357 | モデルデータ型 | IPXNodeItem | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-358 | モデルデータ型 | IPXBoneNodeItem | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-359 | モデルデータ型 | IPXMorphNodeItem | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-360 | モデルデータ型 | IPXBody | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-361 | モデルデータ型 | IPXJoint | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-362 | モデルデータ型 | IPXSoftBody | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-363 | モデルデータ型 | IPXSoftBodyAnchor | 提供 | モデル | 全公開メンバーへのアクセス(型単位) |
| CAP-364 | PMXビルダ | IPXPmxBuilder.Pmx | 提供 | モデル |  |
| CAP-365 | PMXビルダ | IPXPmxBuilder.Vertex | 提供 | モデル |  |
| CAP-366 | PMXビルダ | IPXPmxBuilder.Face | 提供 | モデル |  |
| CAP-367 | PMXビルダ | IPXPmxBuilder.Material | 提供 | モデル |  |
| CAP-368 | PMXビルダ | IPXPmxBuilder.Bone | 提供 | モデル |  |
| CAP-369 | PMXビルダ | IPXPmxBuilder.IKLink | 提供 | モデル |  |
| CAP-370 | PMXビルダ | IPXPmxBuilder.Morph | 提供 | モデル |  |
| CAP-371 | PMXビルダ | IPXPmxBuilder.VertexMorphOffset | 提供 | モデル |  |
| CAP-372 | PMXビルダ | IPXPmxBuilder.UVMorphOffset | 提供 | モデル |  |
| CAP-373 | PMXビルダ | IPXPmxBuilder.BoneMorphOffset | 提供 | モデル |  |
| CAP-374 | PMXビルダ | IPXPmxBuilder.MaterialMorphOffset | 提供 | モデル |  |
| CAP-375 | PMXビルダ | IPXPmxBuilder.GroupMorphOffset | 提供 | モデル |  |
| CAP-376 | PMXビルダ | IPXPmxBuilder.ImpulseMorphOffset | 提供 | モデル |  |
| CAP-377 | PMXビルダ | IPXPmxBuilder.Node | 提供 | モデル |  |
| CAP-378 | PMXビルダ | IPXPmxBuilder.BoneNodeItem | 提供 | モデル |  |
| CAP-379 | PMXビルダ | IPXPmxBuilder.MorphNodeItem | 提供 | モデル |  |
| CAP-380 | PMXビルダ | IPXPmxBuilder.Body | 提供 | モデル |  |
| CAP-381 | PMXビルダ | IPXPmxBuilder.Joint | 提供 | モデル |  |
| CAP-382 | PMXビルダ | IPXPmxBuilder.SoftBody | 提供 | モデル |  |
| CAP-383 | PMXビルダ | IPXPmxBuilder.SoftBodyAnchor | 提供 | モデル |  |
| CAP-384 | プリミティブ | IPXPrimitiveBuilder.AddPlane | 提供 | モデル |  |
| CAP-385 | プリミティブ | IPXPrimitiveBuilder.AddBox | 提供 | モデル |  |
| CAP-386 | プリミティブ | IPXPrimitiveBuilder.AddSphere | 提供 | モデル |  |
| CAP-387 | プリミティブ | IPXPrimitiveBuilder.AddCylinder | 提供 | モデル |  |
| CAP-388 | プリミティブ | IPXPrimitiveBuilder.AddTorus | 提供 | モデル |  |
| CAP-389 | プリミティブ | IPXPrimitiveBuilder.AddText | 提供 | モデル |  |
| CAP-390 | VMD/VMEビルダ | IPEBuilder.CreateVmd | 提供 | 変形・モーション | 他のオーバーロードを提供。契約注記: PMDを引数に取る版はレガシーのため対象外 |
| CAP-391 | VMD/VMEビルダ | IPEBuilder.CreateVmdIPL | 提供 | 変形・モーション |  |
| CAP-392 | VMD/VMEビルダ | IPEBuilder.CreateVmdBoneKey | 提供 | 変形・モーション |  |
| CAP-393 | VMD/VMEビルダ | IPEBuilder.CreateVmdMorphKey | 提供 | 変形・モーション |  |
| CAP-394 | VMD/VMEビルダ | IPEBuilder.CreateVmdBasCameraKey | 提供 | 変形・モーション |  |
| CAP-395 | VMD/VMEビルダ | IPEBuilder.CreateVmdLightKey | 提供 | 変形・モーション |  |
| CAP-396 | VMD/VMEビルダ | IPEBuilder.CreateVmdSelfShadowKey | 提供 | 変形・モーション |  |
| CAP-397 | VMD/VMEビルダ | IPEBuilder.CreateVmdBonePoseState | 提供 | 変形・モーション |  |
| CAP-398 | VMD/VMEビルダ | IPEBuilder.CreateVme | 提供 | 変形・モーション | 契約注記: PMDを引数に取る版はレガシーのため対象外 |
| CAP-399 | VMD/VMEビルダ | IPEBuilder.CreateVmeGroup | 提供 | 変形・モーション |  |
| CAP-400 | VMD/VMEビルダ | IPEBuilder.CreateVmePath | 提供 | 変形・モーション |  |
| CAP-401 | VMEデータ型 | IPEVmeElement | 提供 | 変形・モーション |  |
| CAP-402 | VMEデータ型 | IPEVmeEventElement | 提供 | 変形・モーション |  |
| CAP-403 | VMEデータ型 | IPEVmeObject | 提供 | 変形・モーション |  |
| CAP-404 | VMEデータ型 | IPEVme | 提供 | 変形・モーション |  |
| CAP-405 | VMEデータ型 | IPEVmeFrameEvent | 提供 | 変形・モーション | 契約注記: デリゲートを要するシグネチャは非対応(5件) |
| CAP-406 | VMEデータ型 | IPEVmeEventState | 提供 | 変形・モーション |  |
| CAP-407 | VMEデータ型 | IPEVmeSingleValueState | 提供 | 変形・モーション |  |
| CAP-408 | VMEデータ型 | IPEVmeSingleValueElement | 提供 | 変形・モーション |  |
| CAP-409 | VMEデータ型 | IPEVmeGroup | 提供 | 変形・モーション | 契約注記: デリゲートを要するシグネチャは非対応(2件) |
| CAP-410 | VMEデータ型 | IPEVmeGroupBone | 提供 | 変形・モーション |  |
| CAP-411 | VMEデータ型 | IPEVmeGroupMorph | 提供 | 変形・モーション |  |
| CAP-412 | VMEデータ型 | IPEVmePrimaryValue`1 | 提供 | 変形・モーション |  |
| CAP-413 | VMEデータ型 | IPEVmeSingleValue | 提供 | 変形・モーション |  |
| CAP-414 | VMEデータ型 | IPEVmeVectorValue | 提供 | 変形・モーション |  |
| CAP-415 | VMEデータ型 | IPEVmeQuaternionValue | 提供 | 変形・モーション |  |
| CAP-416 | VMEデータ型 | IPEVmePosition | 提供 | 変形・モーション |  |
| CAP-417 | VMEデータ型 | IPEVmeDirection | 提供 | 変形・モーション |  |
| CAP-418 | VMEデータ型 | IPEVmeScale | 提供 | 変形・モーション |  |
| CAP-419 | VMEデータ型 | IPEVmeSingleValueOperator | 提供 | 変形・モーション |  |
| CAP-420 | VMEデータ型 | IPEVmeVectorValueOperator | 提供 | 変形・モーション |  |
| CAP-421 | VMEデータ型 | IPEVmeQuaternionValueOperator | 提供 | 変形・モーション |  |
| CAP-422 | VMEデータ型 | IPEVmePositionOperator | 提供 | 変形・モーション |  |
| CAP-423 | VMEデータ型 | IPEVmeDirectionOperator | 提供 | 変形・モーション |  |
| CAP-424 | VMEデータ型 | IPEVmeScalingOperator | 提供 | 変形・モーション |  |
| CAP-425 | VMEデータ型 | IPEVmeSingleValueEventOperator | 提供 | 変形・モーション | 契約注記: デリゲートを要するシグネチャは非対応(6件) |
| CAP-426 | VMEデータ型 | IPEVmeVectorValueEventOperator | 提供 | 変形・モーション | 契約注記: デリゲートを要するシグネチャは非対応(10件) |
| CAP-427 | VMEデータ型 | IPEVmeQuaternionValueEventOperator | 提供 | 変形・モーション | 契約注記: デリゲートを要するシグネチャは非対応(6件) |
| CAP-428 | VMEデータ型 | IPEVmePositionEventOperator | 提供 | 変形・モーション | 契約注記: デリゲートを要するシグネチャは非対応(5件) |
| CAP-429 | VMEデータ型 | IPEVmeDirectionEventOperator | 提供 | 変形・モーション | 契約注記: デリゲートを要するシグネチャは非対応(2件) |
| CAP-430 | VMEデータ型 | IPEVmeScalingEventOperator | 提供 | 変形・モーション | 契約注記: デリゲートを要するシグネチャは非対応(2件) |
| CAP-431 | VMEデータ型 | IPEVmePath | 提供 | 変形・モーション | 契約注記: デリゲートを要するシグネチャは非対応(3件) |
| CAP-432 | VMEデータ型 | IPEVmeBoneState | 提供 | 変形・モーション |  |
| CAP-433 | VMEデータ型 | IPEVmeBone | 提供 | 変形・モーション |  |
| CAP-434 | VMEデータ型 | IPEVmeCameraState | 提供 | 変形・モーション |  |
| CAP-435 | VMEデータ型 | IPEVmeCameraPosition | 提供 | 変形・モーション |  |
| CAP-436 | VMEデータ型 | IPEVmeCamera | 提供 | 変形・モーション |  |
| CAP-437 | VMEデータ型 | IPEVmeResult | 提供 | 変形・モーション |  |
| CAP-438 | VMEデータ型 | IPEVmeBoneResult | 提供 | 変形・モーション |  |
| CAP-439 | VMEデータ型 | IPEVmeMorphResult | 提供 | 変形・モーション |  |
| CAP-440 | VMEデータ型 | IPEVmeCameraResult | 提供 | 変形・モーション |  |
| CAP-441 | VMEデータ型 | IPEVmeLightResult | 提供 | 変形・モーション |  |
| CAP-442 | VMEデータ型 | IPEVmeLightState | 提供 | 変形・モーション |  |
| CAP-443 | VMEデータ型 | IPEVmeLight | 提供 | 変形・モーション |  |
| CAP-444 | VMDデータ型 | IPEVmdBonePoseState | 提供 | 変形・モーション | 全公開メンバーへのアクセス(型単位) |
| CAP-445 | VMDデータ型 | IPEVmd | 提供 | 変形・モーション | 全公開メンバー(型単位)。契約注記: 入出力はファイルパス版のみ |
| CAP-446 | VMDデータ型 | IPEVmdFrameKey | 提供 | 変形・モーション | 全公開メンバーへのアクセス(型単位) |
| CAP-447 | VMDデータ型 | IPEVmdIPL | 提供 | 変形・モーション | 全公開メンバーへのアクセス(型単位) |
| CAP-448 | VMDデータ型 | IPEVmdBoneKey | 提供 | 変形・モーション | 全公開メンバーへのアクセス(型単位) |
| CAP-449 | VMDデータ型 | IPEVmdMorphKey | 提供 | 変形・モーション | 全公開メンバーへのアクセス(型単位) |
| CAP-450 | VMDデータ型 | IPEVmdCameraKey | 提供 | 変形・モーション | 全公開メンバーへのアクセス(型単位) |
| CAP-451 | VMDデータ型 | IPEVmdLightKey | 提供 | 変形・モーション | 全公開メンバーへのアクセス(型単位) |
| CAP-452 | VMDデータ型 | IPEVmdSelfShadowKey | 提供 | 変形・モーション | 全公開メンバーへのアクセス(型単位) |
| CAP-453 | VMDデータ型 | IPEVmdVisibleIKKey | 提供 | 変形・モーション | 全公開メンバーへのアクセス(型単位) |
| CAP-454 | VMDデータ型 | IPEVmdIKEnable | 提供 | 変形・モーション | 全公開メンバーへのアクセス(型単位) |
| CAP-455 | Cプラグイン連携 | PXPluginInfo | 提供 | セッション | プラグイン情報の読み取り用データ(型単位)。契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-456 | Cプラグイン連携 | IPXViewEventListener | 提供 | ビュー | イベント通知の型(型単位)。契約注記: ホスト側がハンドラを登録してキューへ積む形で扱う。取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-457 | Cプラグイン連携 | IPXUIModelEventListener | 提供 | ビュー | イベント通知の型(型単位)。契約注記: ホスト側がハンドラを登録してキューへ積む形で扱う。取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-458 | Cプラグイン連携 | PXEventArgs | 提供 | ビュー | イベント引数の型(型単位)。契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-459 | プラグイン機構 | IPEPlugin / PEPluginClass / PEPluginOption / IPERunArgs / PECheckResult | 非対応 |  | プラグイン自身がホストに登録されるための実装専用API |
| CAP-460 | プラグイン情報 | IPERegisteredPluginInfo / IPEPluginOption | 提供 | セッション | GetPluginInfoの結果として返す読み取り用データ |
| CAP-461 | ビルダ別経路 | PEStaticBuilder / IPEShortBuilder | 非対応 |  | IPXPmxBuilder等の提供経路と重複する短絡経路のため |
| CAP-462 | プラグイン拡張点 | IPECheckerPlugin / IPEImportPlugin / IPEExportPlugin | 非対応 |  | プラグインDLL側の拡張点(MCPからの呼び出し対象ではない) |
| CAP-463 | PMDレガシー | PEPlugin.Pmd.* のコネクタ・データ型と IPEBuilder のPMD/X系生成 | 非対応 |  | PMX系に同等機能。PMDファイル入出力はFormコネクタの能力として提供 |
| CAP-464 | ビューヘルパ | IPEObjectSelectConnector / IPEExtensionEditConnector | 提供 | ビュー | 固有メンバーなし。契約注記: 基底のウィンドウ表示制御(Visible・Location・Focus)のみ |
| CAP-465 | Cプラグイン実装拡張点 | PXCPlugin.RegisterBase / IPXCPlugin / PXCPluginClass | 非対応 |  | Cプラグインを実装する側の基底クラス・エントリポイント(実装専用) |
| CAP-466 | SDX数値型 | PEPlugin.SDX.*(M・Q・V2・V3・V4) | 非対応 |  | SlimDX数値型の橋渡し型。演算メンバーはモデル状態に作用せずクライアント側で完結する数値計算のため対象外。値の受け渡しはJSON数値配列(共通契約仕様書が定める) |
| CAP-467 | Cプラグイン連携 | PXEventArgs.UIModelMouse | 提供 | ビュー | 入れ子の公開データ型(型単位)。契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-468 | Cプラグイン連携 | PXEventArgs.UIModelMouseDrag | 提供 | ビュー | 入れ子の公開データ型(型単位)。契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-469 | Cプラグイン連携 | PXEventArgs.ViewMouse | 提供 | ビュー | 入れ子の公開データ型(型単位)。契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-470 | Cプラグイン連携 | PXEventArgs.ViewObjectSelected | 提供 | ビュー | 入れ子の公開データ型(型単位)。契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-471 | Cプラグイン連携 | PXUIModelHelper.MaterialColorEvPara | 提供 | ビュー | 入れ子の公開データ型(型単位)。契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
| CAP-472 | Cプラグイン連携 | PXUIModelHelper.TextControl | 提供 | ビュー | 入れ子の公開データ型(型単位)。契約注記: 取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨 |
