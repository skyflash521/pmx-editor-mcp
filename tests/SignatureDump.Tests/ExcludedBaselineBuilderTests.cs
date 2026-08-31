using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ExcludedBaselineBuilderTests
    {
        // 列挙結果の題材。凍結の対象が指す先を型ごとに複数持たせ、選ばれてはいけない隣——同じ
        // メンバーの別のオーバーロード・同じ型の別のメンバー・値の表現を狭めるだけの記載が
        // 指すメンバー・関わりのない型——も並べる。値は列挙器が実物へ書き出したものをそのまま使う。
        // 引数は名前・型・向き・省略可否を縦棒で区切って並べる。
        private static readonly object[][] SignatureRows =
        {
            new object[]
            {
                "PEPlugin.IPEBuilder.CreatePmd()",
                "PEPlugin.IPEBuilder", "PEPlugin.Pmd.IPEPmd",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.IPEBuilder.CreatePmd(System.String)",
                "PEPlugin.IPEBuilder", "PEPlugin.Pmd.IPEPmd",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new[] { "path|System.String|In|false" },
            },
            new object[]
            {
                "PEPlugin.IPEBuilder.CreateVertex()",
                "PEPlugin.IPEBuilder", "PEPlugin.Pmd.IPEVertex",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.IPEBuilder.CreateVmd()",
                "PEPlugin.IPEBuilder", "PEPlugin.Vmd.IPEVmd",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.IPEBuilder.CreateVmd(PEPlugin.Pmd.IPEPmd)",
                "PEPlugin.IPEBuilder", "PEPlugin.Vmd.IPEVmd",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new[] { "pmd|PEPlugin.Pmd.IPEPmd|In|false" },
            },
            new object[]
            {
                "PEPlugin.IPEBuilder.CreateVmd(PEPlugin.Pmd.IPEPmd,System.String)",
                "PEPlugin.IPEBuilder", "PEPlugin.Vmd.IPEVmd",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new[] { "pmd|PEPlugin.Pmd.IPEPmd|In|false", "vmdPath|System.String|In|false" },
            },
            new object[]
            {
                "PEPlugin.IPEBuilder.CreateVmd(System.String[],System.String[])",
                "PEPlugin.IPEBuilder", "PEPlugin.Vmd.IPEVmd",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new[] { "boneNames|System.String[]|In|false", "morphNames|System.String[]|In|false" },
            },
            new object[]
            {
                "PEPlugin.IPEBuilder.CreateVme()",
                "PEPlugin.IPEBuilder", "PEPlugin.Vme.IPEVme",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.IPEBuilder.CreateVme(PEPlugin.Pmd.IPEPmd)",
                "PEPlugin.IPEBuilder", "PEPlugin.Vme.IPEVme",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new[] { "pmd|PEPlugin.Pmd.IPEPmd|In|false" },
            },
            new object[]
            {
                "PEPlugin.IPEBuilder.CreateXPmd()",
                "PEPlugin.IPEBuilder", "PEPlugin.Pmd.IPEXPmd",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.IPEBuilder.CreateXPmd(PEPlugin.Pmd.IPEPmd)",
                "PEPlugin.IPEBuilder", "PEPlugin.Pmd.IPEXPmd",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new[] { "pmd|PEPlugin.Pmd.IPEPmd|In|false" },
            },
            new object[]
            {
                "PEPlugin.IPECheckerPlugin.CheckPmx(PEPlugin.Pmx.IPXPmx)",
                "PEPlugin.IPECheckerPlugin", "System.Collections.Generic.IEnumerable<PEPlugin.PECheckResult>",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new[] { "pmx|PEPlugin.Pmx.IPXPmx|In|false" },
            },
            new object[]
            {
                "PEPlugin.IPEExportPlugin.Caption()",
                "PEPlugin.IPEExportPlugin", "System.String",
                MemberKind.Property, false, true, false, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.IPEExportPlugin.Export(PEPlugin.Pmx.IPXPmx,System.String,PEPlugin.IPERunArgs)",
                "PEPlugin.IPEExportPlugin", "System.Void",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new[] { "pmx|PEPlugin.Pmx.IPXPmx|In|false", "path|System.String|In|false", "args|PEPlugin.IPERunArgs|In|false" },
            },
            new object[]
            {
                "PEPlugin.IPEExportPlugin.Ext()",
                "PEPlugin.IPEExportPlugin", "System.String",
                MemberKind.Property, false, true, false, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.IPEImportPlugin.Caption()",
                "PEPlugin.IPEImportPlugin", "System.String",
                MemberKind.Property, false, true, false, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.IPEImportPlugin.Ext()",
                "PEPlugin.IPEImportPlugin", "System.String",
                MemberKind.Property, false, true, false, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.IPEImportPlugin.Import(System.String,PEPlugin.IPERunArgs)",
                "PEPlugin.IPEImportPlugin", "PEPlugin.Pmx.IPXPmx",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new[] { "path|System.String|In|false", "args|PEPlugin.IPERunArgs|In|false" },
            },
            new object[]
            {
                "PEPlugin.IPEPlugin.Description()",
                "PEPlugin.IPEPlugin", "System.String",
                MemberKind.Property, false, true, false, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.IPEPlugin.Name()",
                "PEPlugin.IPEPlugin", "System.String",
                MemberKind.Property, false, true, false, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.IPEPlugin.Option()",
                "PEPlugin.IPEPlugin", "PEPlugin.IPEPluginOption",
                MemberKind.Property, false, true, false, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.IPERunArgs.Host()",
                "PEPlugin.IPERunArgs", "PEPlugin.IPEPluginHost",
                MemberKind.Property, false, true, false, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.IPERunArgs.IsBootup()",
                "PEPlugin.IPERunArgs", "System.Boolean",
                MemberKind.Property, false, true, false, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.IPERunArgs.ModulePath()",
                "PEPlugin.IPERunArgs", "System.String",
                MemberKind.Property, false, true, false, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.IPEShortBuilder.Body()",
                "PEPlugin.IPEShortBuilder", "PEPlugin.Pmd.IPEBody",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.IPEShortBuilder.Bone()",
                "PEPlugin.IPEShortBuilder", "PEPlugin.Pmd.IPEBone",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.IPEShortBuilder.Expression()",
                "PEPlugin.IPEShortBuilder", "PEPlugin.Pmd.IPEExpression",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.IPESystemConnector.GetShareObject(System.String,System.Boolean)",
                "PEPlugin.IPESystemConnector", "System.Object",
                MemberKind.Method, false, false, false, OperationDirection.Read,
                new[] { "key|System.String|In|false", "clear|System.Boolean|In|true" },
            },
            new object[]
            {
                "PEPlugin.IPESystemConnector.SetShareObject(System.String,System.Object)",
                "PEPlugin.IPESystemConnector", "System.Boolean",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new[] { "key|System.String|In|false", "obj|System.Object|In|false" },
            },
            new object[]
            {
                "PEPlugin.PECheckResult.Filter()",
                "PEPlugin.PECheckResult", "System.Int32",
                MemberKind.Property, false, true, true, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.PECheckResult.Text()",
                "PEPlugin.PECheckResult", "System.String",
                MemberKind.Property, false, true, true, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.PEPluginClass.Description()",
                "PEPlugin.PEPluginClass", "System.String",
                MemberKind.Property, false, true, false, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.PEPluginClass.Dispose()",
                "PEPlugin.PEPluginClass", "System.Void",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.PEPluginClass.Name()",
                "PEPlugin.PEPluginClass", "System.String",
                MemberKind.Property, false, true, false, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.PEPluginOption.Bootup()",
                "PEPlugin.PEPluginOption", "System.Boolean",
                MemberKind.Property, false, true, false, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.PEPluginOption.RegisterMenu()",
                "PEPlugin.PEPluginOption", "System.Boolean",
                MemberKind.Property, false, true, false, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.PEPluginOption.RegisterMenuText()",
                "PEPlugin.PEPluginOption", "System.String",
                MemberKind.Property, false, true, false, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.PEStaticBuilder.Builder()",
                "PEPlugin.PEStaticBuilder", "PEPlugin.IPEBuilder",
                MemberKind.Property, true, true, true, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.PEStaticBuilder.Pmx()",
                "PEPlugin.PEStaticBuilder", "PEPlugin.IPXPmxBuilder",
                MemberKind.Property, true, true, true, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.PEStaticBuilder.SC()",
                "PEPlugin.PEStaticBuilder", "PEPlugin.IPEShortBuilder",
                MemberKind.Property, true, true, false, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.Pmd.IPEPmd.Body()",
                "PEPlugin.Pmd.IPEPmd", "System.Collections.Generic.IList<PEPlugin.Pmd.IPEBody>",
                MemberKind.Property, false, true, false, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.Pmd.IPEPmd.Bone()",
                "PEPlugin.Pmd.IPEPmd", "System.Collections.Generic.IList<PEPlugin.Pmd.IPEBone>",
                MemberKind.Property, false, true, false, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.Pmd.IPEPmd.Clear()",
                "PEPlugin.Pmd.IPEPmd", "System.Void",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.Pmd.IPEVertex.Bone1()",
                "PEPlugin.Pmd.IPEVertex", "System.Int32",
                MemberKind.Property, false, true, true, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.Pmd.IPEVertex.Bone2()",
                "PEPlugin.Pmd.IPEVertex", "System.Int32",
                MemberKind.Property, false, true, true, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.Pmd.IPEVertex.NonEdgeFlag()",
                "PEPlugin.Pmd.IPEVertex", "System.Boolean",
                MemberKind.Property, false, true, true, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.Pmx.IPXBone.AppendParent()",
                "PEPlugin.Pmx.IPXBone", "PEPlugin.Pmx.IPXBone",
                MemberKind.Property, false, true, true, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.Pmx.IPXPmx.FromFile(System.String)",
                "PEPlugin.Pmx.IPXPmx", "System.Void",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new[] { "path|System.String|In|false" },
            },
            new object[]
            {
                "PEPlugin.Pmx.IPXPmx.FromStream(System.IO.Stream)",
                "PEPlugin.Pmx.IPXPmx", "System.Void",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new[] { "s|System.IO.Stream|In|false" },
            },
            new object[]
            {
                "PEPlugin.Pmx.IPXPmx.ToStream(System.IO.Stream)",
                "PEPlugin.Pmx.IPXPmx", "System.Void",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new[] { "s|System.IO.Stream|In|false" },
            },
            new object[]
            {
                "PEPlugin.Pmx.IPXPmxConnector.GetCurrentState()",
                "PEPlugin.Pmx.IPXPmxConnector", "PEPlugin.Pmx.IPXPmx",
                MemberKind.Method, false, false, false, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.SDX.M..ctor()",
                "PEPlugin.SDX.M", "PEPlugin.SDX.M",
                MemberKind.Constructor, false, false, false, OperationDirection.Write,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.SDX.M.AddTrans(ref SlimDX.Matrix,SlimDX.Vector3)",
                "PEPlugin.SDX.M", "System.Void",
                MemberKind.Method, true, false, false, OperationDirection.Write,
                new[] { "m|SlimDX.Matrix|Ref|false", "trans|SlimDX.Vector3|In|false" },
            },
            new object[]
            {
                "PEPlugin.SDX.M.Clone()",
                "PEPlugin.SDX.M", "PEPlugin.SDX.M",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.SDX.Q..ctor()",
                "PEPlugin.SDX.Q", "PEPlugin.SDX.Q",
                MemberKind.Constructor, false, false, false, OperationDirection.Write,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.SDX.Q.Clone()",
                "PEPlugin.SDX.Q", "PEPlugin.SDX.Q",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.SDX.Q.D2R(SlimDX.Vector3)",
                "PEPlugin.SDX.Q", "SlimDX.Vector3",
                MemberKind.Method, true, false, false, OperationDirection.Write,
                new[] { "deg|SlimDX.Vector3|In|false" },
            },
            new object[]
            {
                "PEPlugin.SDX.V2..ctor()",
                "PEPlugin.SDX.V2", "PEPlugin.SDX.V2",
                MemberKind.Constructor, false, false, false, OperationDirection.Write,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.SDX.V2.Clone()",
                "PEPlugin.SDX.V2", "PEPlugin.SDX.V2",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.SDX.V2.Length()",
                "PEPlugin.SDX.V2", "System.Single",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.SDX.V3..ctor()",
                "PEPlugin.SDX.V3", "PEPlugin.SDX.V3",
                MemberKind.Constructor, false, false, false, OperationDirection.Write,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.SDX.V3.B()",
                "PEPlugin.SDX.V3", "System.Single",
                MemberKind.Property, false, true, true, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.SDX.V3.Clone()",
                "PEPlugin.SDX.V3", "PEPlugin.SDX.V3",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.SDX.V4..ctor()",
                "PEPlugin.SDX.V4", "PEPlugin.SDX.V4",
                MemberKind.Constructor, false, false, false, OperationDirection.Write,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.SDX.V4.A()",
                "PEPlugin.SDX.V4", "System.Single",
                MemberKind.Property, false, true, true, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.SDX.V4.B()",
                "PEPlugin.SDX.V4", "System.Single",
                MemberKind.Property, false, true, true, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.View.IPEPMDViewConnector.BootupVmdView()",
                "PEPlugin.View.IPEPMDViewConnector", "System.Void",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.View.IPEPMDViewConnector.BootupVmdView(PEPlugin.Pmd.IPEPmd,PEPlugin.Vmd.IPEVmd)",
                "PEPlugin.View.IPEPMDViewConnector", "System.Void",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new[] { "pmd|PEPlugin.Pmd.IPEPmd|In|false", "vmd|PEPlugin.Vmd.IPEVmd|In|false" },
            },
            new object[]
            {
                "PEPlugin.View.IPEPMDViewConnector.BootupVmdView(PEPlugin.Pmx.IPXPmx,PEPlugin.Vmd.IPEVmd)",
                "PEPlugin.View.IPEPMDViewConnector", "System.Void",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new[] { "pmx|PEPlugin.Pmx.IPXPmx|In|false", "vmd|PEPlugin.Vmd.IPEVmd|In|false" },
            },
            new object[]
            {
                "PEPlugin.Vmd.IPEVmd.Bone()",
                "PEPlugin.Vmd.IPEVmd", "System.Collections.Generic.IList<PEPlugin.Vmd.IPEVmdBoneKey>",
                MemberKind.Property, false, true, false, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PEPlugin.Vmd.IPEVmd.Camera()",
                "PEPlugin.Vmd.IPEVmd", "System.Collections.Generic.IList<PEPlugin.Vmd.IPEVmdCameraKey>",
                MemberKind.Property, false, true, false, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PXCPlugin.IPXCPlugin.Description()",
                "PXCPlugin.IPXCPlugin", "System.String",
                MemberKind.Property, false, true, false, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PXCPlugin.IPXCPlugin.MenuText()",
                "PXCPlugin.IPXCPlugin", "System.String",
                MemberKind.Property, false, true, false, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PXCPlugin.IPXCPlugin.Name()",
                "PXCPlugin.IPXCPlugin", "System.String",
                MemberKind.Property, false, true, false, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PXCPlugin.IPXSystemControl.GetCPluginInfo(PXCPlugin.IPXCPlugin)",
                "PXCPlugin.IPXSystemControl", "PXCPlugin.PXPluginInfo",
                MemberKind.Method, false, false, false, OperationDirection.Read,
                new[] { "plugin|PXCPlugin.IPXCPlugin|In|false" },
            },
            new object[]
            {
                "PXCPlugin.IPXSystemControl.GetCPluginInfo(System.Int32)",
                "PXCPlugin.IPXSystemControl", "PXCPlugin.PXPluginInfo",
                MemberKind.Method, false, false, false, OperationDirection.Read,
                new[] { "n|System.Int32|In|false" },
            },
            new object[]
            {
                "PXCPlugin.PXCPluginClass.Description()",
                "PXCPlugin.PXCPluginClass", "System.String",
                MemberKind.Property, false, true, false, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PXCPlugin.PXCPluginClass.Dispose()",
                "PXCPlugin.PXCPluginClass", "System.Void",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new string[0],
            },
            new object[]
            {
                "PXCPlugin.PXCPluginClass.InitializeLifetimeService()",
                "PXCPlugin.PXCPluginClass", "System.Object",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new string[0],
            },
            new object[]
            {
                "PXCPlugin.RegisterBase.ClassNames()",
                "PXCPlugin.RegisterBase", "System.String[]",
                MemberKind.Property, false, true, false, OperationDirection.Read,
                new string[0],
            },
            new object[]
            {
                "PXCPlugin.UIModel.IPXUIModel.CreateEventListener(PXCPlugin.Event.IPXEventConnector,System.Int32[])",
                "PXCPlugin.UIModel.IPXUIModel", "PXCPlugin.Event.IPXUIModelEventListener",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new[] { "c|PXCPlugin.Event.IPXEventConnector|In|false", "materials|System.Int32[]|In|false" },
            },
            new object[]
            {
                "PXCPlugin.UIModel.IPXUIModel.SetAutoRelease(PXCPlugin.IPXCPlugin)",
                "PXCPlugin.UIModel.IPXUIModel", "System.Void",
                MemberKind.Method, false, false, false, OperationDirection.Write,
                new[] { "plugin|PXCPlugin.IPXCPlugin|In|false" },
            },
        };

        // 台帳の題材。凍結する能力に加えて、凍結してはいけない能力——値の表現を狭めるだけの
        // 記載・指す先を持たない記載・記載を持たない提供能力——も置く。値は台帳の行をそのまま使う。
        private static readonly object[][] LedgerRows =
        {
            new object[]
            {
                "CAP-001", "PMXデータ", "IPXPmxConnector.GetCurrentState",
                CapabilityTargetKind.Single, CapabilityStatus.Provided, CapabilityOwner.Model,
                "",
            },
            new object[]
            {
                "CAP-072", "システム", "IPESystemConnector.SetShareObject",
                CapabilityTargetKind.Single, CapabilityStatus.Provided, CapabilityOwner.Session,
                "JSONで表現できる値に限定して提供(任意の.NETオブジェクトは対象外)",
            },
            new object[]
            {
                "CAP-073", "システム", "IPESystemConnector.GetShareObject",
                CapabilityTargetKind.Single, CapabilityStatus.Provided, CapabilityOwner.Session,
                "JSONで表現できる値に限定して提供(任意の.NETオブジェクトは対象外)",
            },
            new object[]
            {
                "CAP-114", "PmxView", "IPXPmxViewConnector.BootupVmdView",
                CapabilityTargetKind.Single, CapabilityStatus.Provided, CapabilityOwner.MotionTransform,
                "非対応件数: 1。PMX+VMD版と引数なし版を対象。契約注記: PMDを引数に取る版はレガシーのため対象外",
            },
            new object[]
            {
                "CAP-269", "Cプラグイン連携", "IPXSystemControl.GetCPluginInfo",
                CapabilityTargetKind.Single, CapabilityStatus.Provided, CapabilityOwner.Session,
                "非対応件数: 1。Int32版を提供。契約注記: IPXCPluginを引数に取る版は対象外。取得経路(GetCPluginRunArgsClone)は一次資料で利用非推奨",
            },
            new object[]
            {
                "CAP-304", "Cプラグイン連携", "IPXUIModel.SetAutoRelease",
                CapabilityTargetKind.Single, CapabilityStatus.NotSupported, CapabilityOwner.None,
                "引数のIPXCPlugin(実装拡張点・非対応)を取得経路から得られないため",
            },
            new object[]
            {
                "CAP-339", "モデルデータ型", "IPXPmx",
                CapabilityTargetKind.Single, CapabilityStatus.Provided, CapabilityOwner.Model,
                "非対応件数: 2。全公開メンバー(型単位)。契約注記: FromStream/ToStreamはファイルパス版で代替し対象外",
            },
            new object[]
            {
                "CAP-390", "VMD/VMEビルダ", "IPEBuilder.CreateVmd",
                CapabilityTargetKind.Single, CapabilityStatus.Provided, CapabilityOwner.MotionTransform,
                "非対応件数: 2。他のオーバーロードを提供。契約注記: PMDを引数に取る版はレガシーのため対象外",
            },
            new object[]
            {
                "CAP-398", "VMD/VMEビルダ", "IPEBuilder.CreateVme",
                CapabilityTargetKind.Single, CapabilityStatus.Provided, CapabilityOwner.MotionTransform,
                "非対応件数: 1。契約注記: PMDを引数に取る版はレガシーのため対象外",
            },
            new object[]
            {
                "CAP-445", "VMDデータ型", "IPEVmd",
                CapabilityTargetKind.Single, CapabilityStatus.Provided, CapabilityOwner.MotionTransform,
                "全公開メンバー(型単位)。Stream入出力メンバーはファイルパス版で代替し対象外",
            },
            new object[]
            {
                "CAP-459", "プラグイン機構", "IPEPlugin / PEPluginClass / PEPluginOption / IPERunArgs / PECheckResult",
                CapabilityTargetKind.Group, CapabilityStatus.NotSupported, CapabilityOwner.None,
                "プラグイン自身がホストに登録されるための実装専用API",
            },
            new object[]
            {
                "CAP-461", "ビルダ別経路", "PEStaticBuilder / IPEShortBuilder",
                CapabilityTargetKind.Group, CapabilityStatus.NotSupported, CapabilityOwner.None,
                "IPXPmxBuilder等の提供経路と重複する短絡経路のため",
            },
            new object[]
            {
                "CAP-462", "プラグイン拡張点", "IPECheckerPlugin / IPEImportPlugin / IPEExportPlugin",
                CapabilityTargetKind.Group, CapabilityStatus.NotSupported, CapabilityOwner.None,
                "プラグインDLL側の拡張点(MCPからの呼び出し対象ではない)",
            },
            new object[]
            {
                "CAP-463", "PMDレガシー", "PEPlugin.Pmd.* のコネクタ・データ型と IPEBuilder のPMD/X系生成",
                CapabilityTargetKind.Pattern, CapabilityStatus.NotSupported, CapabilityOwner.None,
                "PMX系に同等機能。PMDファイル入出力はFormコネクタの能力として提供",
            },
            new object[]
            {
                "CAP-465", "Cプラグイン実装拡張点", "PXCPlugin.RegisterBase / IPXCPlugin / PXCPluginClass",
                CapabilityTargetKind.Group, CapabilityStatus.NotSupported, CapabilityOwner.None,
                "Cプラグインを実装する側の基底クラス・エントリポイント(実装専用)",
            },
            new object[]
            {
                "CAP-466", "SDX数値型", "PEPlugin.SDX.*(M・Q・V2・V3・V4)",
                CapabilityTargetKind.Pattern, CapabilityStatus.NotSupported, CapabilityOwner.None,
                "SlimDX数値型の橋渡し型。演算メンバーはモデル状態に作用せずクライアント側で完結する数値計算のため対象外。値の受け渡しはJSON数値配列(共通契約仕様書が定める)",
            },
        };

        // 凍結する組の全体。能力IDの昇順、その中は行キーの昇順。
        private static readonly string[][] Expected =
        {
            new[]
            {
                "CAP-114",
                "PEPlugin.View.IPEPMDViewConnector.BootupVmdView(PEPlugin.Pmd.IPEPmd,PEPlugin.Vmd.IPEVmd)",
            },
            new[]
            {
                "CAP-269",
                "PXCPlugin.IPXSystemControl.GetCPluginInfo(PXCPlugin.IPXCPlugin)",
            },
            new[]
            {
                "CAP-304",
                "PXCPlugin.UIModel.IPXUIModel.SetAutoRelease(PXCPlugin.IPXCPlugin)",
            },
            new[]
            {
                "CAP-339",
                "PEPlugin.Pmx.IPXPmx.FromStream(System.IO.Stream)",
                "PEPlugin.Pmx.IPXPmx.ToStream(System.IO.Stream)",
            },
            new[]
            {
                "CAP-390",
                "PEPlugin.IPEBuilder.CreateVmd(PEPlugin.Pmd.IPEPmd)",
                "PEPlugin.IPEBuilder.CreateVmd(PEPlugin.Pmd.IPEPmd,System.String)",
            },
            new[]
            {
                "CAP-398",
                "PEPlugin.IPEBuilder.CreateVme(PEPlugin.Pmd.IPEPmd)",
            },
            new[]
            {
                "CAP-459",
                "PEPlugin.IPEPlugin.Description()",
                "PEPlugin.IPEPlugin.Name()",
                "PEPlugin.IPEPlugin.Option()",
                "PEPlugin.IPERunArgs.Host()",
                "PEPlugin.IPERunArgs.IsBootup()",
                "PEPlugin.IPERunArgs.ModulePath()",
                "PEPlugin.PECheckResult.Filter()",
                "PEPlugin.PECheckResult.Text()",
                "PEPlugin.PEPluginClass.Description()",
                "PEPlugin.PEPluginClass.Dispose()",
                "PEPlugin.PEPluginClass.Name()",
                "PEPlugin.PEPluginOption.Bootup()",
                "PEPlugin.PEPluginOption.RegisterMenu()",
                "PEPlugin.PEPluginOption.RegisterMenuText()",
            },
            new[]
            {
                "CAP-461",
                "PEPlugin.IPEShortBuilder.Body()",
                "PEPlugin.IPEShortBuilder.Bone()",
                "PEPlugin.IPEShortBuilder.Expression()",
                "PEPlugin.PEStaticBuilder.Builder()",
                "PEPlugin.PEStaticBuilder.Pmx()",
                "PEPlugin.PEStaticBuilder.SC()",
            },
            new[]
            {
                "CAP-462",
                "PEPlugin.IPECheckerPlugin.CheckPmx(PEPlugin.Pmx.IPXPmx)",
                "PEPlugin.IPEExportPlugin.Caption()",
                "PEPlugin.IPEExportPlugin.Export(PEPlugin.Pmx.IPXPmx,System.String,PEPlugin.IPERunArgs)",
                "PEPlugin.IPEExportPlugin.Ext()",
                "PEPlugin.IPEImportPlugin.Caption()",
                "PEPlugin.IPEImportPlugin.Ext()",
                "PEPlugin.IPEImportPlugin.Import(System.String,PEPlugin.IPERunArgs)",
            },
            new[]
            {
                "CAP-463",
                "PEPlugin.IPEBuilder.CreatePmd()",
                "PEPlugin.IPEBuilder.CreatePmd(System.String)",
                "PEPlugin.IPEBuilder.CreateVertex()",
                "PEPlugin.IPEBuilder.CreateXPmd()",
                "PEPlugin.IPEBuilder.CreateXPmd(PEPlugin.Pmd.IPEPmd)",
                "PEPlugin.Pmd.IPEPmd.Body()",
                "PEPlugin.Pmd.IPEPmd.Bone()",
                "PEPlugin.Pmd.IPEPmd.Clear()",
                "PEPlugin.Pmd.IPEVertex.Bone1()",
                "PEPlugin.Pmd.IPEVertex.Bone2()",
                "PEPlugin.Pmd.IPEVertex.NonEdgeFlag()",
            },
            new[]
            {
                "CAP-465",
                "PXCPlugin.IPXCPlugin.Description()",
                "PXCPlugin.IPXCPlugin.MenuText()",
                "PXCPlugin.IPXCPlugin.Name()",
                "PXCPlugin.PXCPluginClass.Description()",
                "PXCPlugin.PXCPluginClass.Dispose()",
                "PXCPlugin.PXCPluginClass.InitializeLifetimeService()",
                "PXCPlugin.RegisterBase.ClassNames()",
            },
            new[]
            {
                "CAP-466",
                "PEPlugin.SDX.M..ctor()",
                "PEPlugin.SDX.M.AddTrans(ref SlimDX.Matrix,SlimDX.Vector3)",
                "PEPlugin.SDX.M.Clone()",
                "PEPlugin.SDX.Q..ctor()",
                "PEPlugin.SDX.Q.Clone()",
                "PEPlugin.SDX.Q.D2R(SlimDX.Vector3)",
                "PEPlugin.SDX.V2..ctor()",
                "PEPlugin.SDX.V2.Clone()",
                "PEPlugin.SDX.V2.Length()",
                "PEPlugin.SDX.V3..ctor()",
                "PEPlugin.SDX.V3.B()",
                "PEPlugin.SDX.V3.Clone()",
                "PEPlugin.SDX.V4..ctor()",
                "PEPlugin.SDX.V4.A()",
                "PEPlugin.SDX.V4.B()",
            },
        };

        private static IList<CapabilityRecord> Ledger()
        {
            return LedgerRows.Select(Capability).ToList();
        }

        private static CapabilityRecord Capability(object[] row)
        {
            string target = (string)row[2];
            CapabilityTargetKind kind = (CapabilityTargetKind)row[3];
            List<string> names = kind == CapabilityTargetKind.Pattern
                ? new List<string>()
                : target.Split(new[] { " / " }, StringSplitOptions.None).ToList();

            return new CapabilityRecord(
                (string)row[0],
                (string)row[1],
                target,
                kind,
                new ReadOnlyCollection<string>(names),
                (CapabilityStatus)row[4],
                (CapabilityOwner)row[5],
                (string)row[6]);
        }

        private static CapabilityRecord WithStatus(
            CapabilityRecord capability, CapabilityStatus status, string owner)
        {
            return new CapabilityRecord(
                capability.Id,
                capability.Category,
                capability.Target,
                capability.TargetKind,
                capability.TargetNames,
                status,
                (CapabilityOwner)Enum.Parse(typeof(CapabilityOwner), owner == "モデル" ? "Model" : "None"),
                capability.Remarks);
        }

        private static CapabilityRecord WithTarget(CapabilityRecord capability, string target)
        {
            return new CapabilityRecord(
                capability.Id,
                capability.Category,
                target,
                capability.TargetKind,
                capability.TargetNames,
                capability.Status,
                capability.Owner,
                capability.Remarks);
        }

        private static CapabilityRecord WithRemarks(CapabilityRecord capability, string remarks)
        {
            return new CapabilityRecord(
                capability.Id,
                capability.Category,
                capability.Target,
                capability.TargetKind,
                capability.TargetNames,
                capability.Status,
                capability.Owner,
                remarks);
        }

        private static IList<SignatureRecord> Signatures()
        {
            return SignatureRows.Select(Signature).ToList();
        }

        private static SignatureRecord Signature(object[] row)
        {
            return new SignatureRecord(
                (string)row[0],
                (string)row[1],
                (MemberKind)row[3],
                MemberName((string)row[0], (string)row[1]),
                (bool)row[4],
                0,
                new ReadOnlyCollection<ParameterRecord>(((string[])row[8]).Select(Parameter).ToList()),
                (string)row[2],
                (bool)row[5],
                (bool)row[6],
                (OperationDirection)row[7]);
        }

        private static string MemberName(string key, string declaringType)
        {
            return key.Split(':')[0].Substring(declaringType.Length + 1).Split('(')[0];
        }

        private static ParameterRecord Parameter(string text)
        {
            string[] parts = text.Split('|');
            return new ParameterRecord(
                parts[0],
                parts[1],
                (ParameterDirection)Enum.Parse(typeof(ParameterDirection), parts[2]),
                bool.Parse(parts[3]));
        }

        private static string[][] Describe(IList<ExcludedBaselineEntry> entries)
        {
            return entries
                .Select(e => new[] { e.CapabilityId }.Concat(e.Signatures).ToArray())
                .ToArray();
        }

        [Fact]
        public void FreezesSignatureSetPerCapability()
        {
            Assert.Equal(Expected, Describe(ExcludedBaselineBuilder.Build(Ledger(), Signatures())));
        }

        [Fact]
        public void LedgerOrderDoesNotChangeTheFrozenSet()
        {
            // 台帳へ行を挿し込んだだけで凍結の並びが変わると、行単位の差分が実際の変化を指さなくなる。
            IList<CapabilityRecord> reversed = Ledger().Reverse().ToList();
            IList<SignatureRecord> shuffled = Signatures().Reverse().ToList();

            Assert.Equal(Expected, Describe(ExcludedBaselineBuilder.Build(reversed, shuffled)));
        }

        [Fact]
        public void TheSameSignatureIsNotPlacedUnderTwoCapabilities()
        {
            // 重なると、除外一覧の照合でどちらの根拠にも通ってしまい、件数の一致も崩れる。
            IList<string> all = ExcludedBaselineBuilder.Build(Ledger(), Signatures())
                .SelectMany(e => e.Signatures).ToList();

            Assert.Equal(all.Count, all.Distinct(StringComparer.Ordinal).Count());
        }

        [Fact]
        public void CapabilityMissingFromLedgerThrows()
        {
            // 台帳を正としない集合を凍結すると、根拠の無い除外がそのまま正本になる。
            IList<CapabilityRecord> ledger = Ledger().Where(c => c.Id != "CAP-459").ToList();

            Assert.Throws<InvalidOperationException>(() => ExcludedBaselineBuilder.Build(ledger, Signatures()));
        }

        [Fact]
        public void NameWithoutAnyTargetThrows()
        {
            // 能力の単位で1件でも残れば通す作りだと、並べた名前のうち1つが指す先を失っても気づけない。
            IList<SignatureRecord> signatures = Signatures()
                .Where(s => s.DeclaringType != "PEPlugin.PECheckResult").ToList();

            Assert.Throws<InvalidOperationException>(() => ExcludedBaselineBuilder.Build(Ledger(), signatures));
        }

        [Fact]
        public void MissingNamedSignatureThrows()
        {
            // 1件でも欠けたまま凍結すると、以後その1件は資格を失ったことに気づけない。
            IList<SignatureRecord> signatures = Signatures()
                .Where(s => s.Key != "PEPlugin.Pmx.IPXPmx.ToStream(System.IO.Stream)").ToList();

            Assert.Throws<InvalidOperationException>(() => ExcludedBaselineBuilder.Build(Ledger(), signatures));
        }

        [Fact]
        public void LedgerWordingDifferentFromFreezePremiseThrows()
        {
            // 凍結できるのは台帳がすでに非対応と記していた範囲だけ。能力IDだけを見る作りだと、
            // 台帳の記載を書き換えても同じ組を凍結でき、根拠にならない。
            IList<CapabilityRecord> provided = Ledger()
                .Select(c => c.Id == "CAP-459" ? WithStatus(c, CapabilityStatus.Provided, "モデル") : c)
                .ToList();
            InvalidOperationException status = Assert.Throws<InvalidOperationException>(
                () => ExcludedBaselineBuilder.Build(provided, Signatures()));
            Assert.Contains("CAP-459", status.Message, StringComparison.Ordinal);

            // 分類が提供の能力は、どのシグネチャを対象外とするかを備考が決めているので備考も見る。
            IList<CapabilityRecord> silent = Ledger()
                .Select(c => c.Id == "CAP-114" ? WithRemarks(c, "PMX+VMD版と引数なし版を対象") : c)
                .ToList();
            InvalidOperationException remarks = Assert.Throws<InvalidOperationException>(
                () => ExcludedBaselineBuilder.Build(silent, Signatures()));
            Assert.Contains("CAP-114", remarks.Message, StringComparison.Ordinal);

            // 対象の欄が変われば、同じ分類・備考でも指している先が変わる。
            IList<CapabilityRecord> moved = Ledger()
                .Select(c => c.Id == "CAP-459" ? WithTarget(c, "IPEPlugin / PEPluginClass") : c)
                .ToList();
            InvalidOperationException target = Assert.Throws<InvalidOperationException>(
                () => ExcludedBaselineBuilder.Build(moved, Signatures()));
            Assert.Contains("CAP-459", target.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void CapabilityAppearingTwiceInLedgerThrows()
        {
            // どの記載を根拠にしたのかが定まらないまま凍結すると、後から根拠をたどれない。
            IList<CapabilityRecord> ledger = Ledger();
            ledger.Add(WithTarget(ledger.Single(c => c.Id == "CAP-459"), "IPEPlugin"));

            InvalidOperationException doubled = Assert.Throws<InvalidOperationException>(
                () => ExcludedBaselineBuilder.Build(ledger, Signatures()));

            Assert.Contains("CAP-459", doubled.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void FailureReasonCarriesTheComparedSignatureCount()
        {
            // 台帳の側が合わないのか、渡された公開シグネチャが空なのかで直し方が違う。
            InvalidOperationException empty = Assert.Throws<InvalidOperationException>(
                () => ExcludedBaselineBuilder.Build(Ledger(), new List<SignatureRecord>()));
            Assert.Contains("突き合わせたシグネチャ: 0 件", empty.Message, StringComparison.Ordinal);

            IList<CapabilityRecord> ledger = Ledger().Where(c => c.Id != "CAP-114").ToList();
            InvalidOperationException missing = Assert.Throws<InvalidOperationException>(
                () => ExcludedBaselineBuilder.Build(ledger, Signatures()));
            Assert.Contains(
                "突き合わせたシグネチャ: " + SignatureRows.Length.ToString(CultureInfo.InvariantCulture) + " 件",
                missing.Message,
                StringComparison.Ordinal);
        }

        [Fact]
        public void MissingLedgerOrEnumerationThrows()
        {
            Assert.Throws<ArgumentNullException>(() => ExcludedBaselineBuilder.Build(null, Signatures()));
            Assert.Throws<ArgumentNullException>(() => ExcludedBaselineBuilder.Build(Ledger(), null));
        }
    }
}
