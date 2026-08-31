using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    public sealed class ExcludedSignatureBuilderTests
    {

        private const string FrozenConstructor = "PEPlugin.SDX.M..ctor()";

        private const string PmdInit = "PEPlugin.Vmd.IPEVmd.Init(PEPlugin.Pmd.IPEPmd)";

        private const string PmxInit = "PEPlugin.Vmd.IPEVmd.Init(PEPlugin.Pmx.IPXPmx)";

        private const string PmxInitWithMoreArguments =
            "PEPlugin.Vmd.IPEVmd.Init(PEPlugin.Pmx.IPXPmx,System.Boolean)";

        private const string PmdReset = "PEPlugin.Vmd.IPEVmd.Reset(PEPlugin.Pmd.IPEPmd)";

        private const string PmxResetWithMoreArguments =
            "PEPlugin.Vmd.IPEVmd.Reset(PEPlugin.Pmx.IPXPmx,System.Boolean)";

        private const string PmdMerge = "PEPlugin.Vmd.IPEVmd.Merge(PEPlugin.Pmd.IPEPmd)";

        private const string PmxMergeWithOtherValueType = "PEPlugin.Vmd.IPEVmd.Merge(PEPlugin.Pmx.IPXPmx)";

        private const string PmdApply = "PEPlugin.Vmd.IPEVmd.Apply(PEPlugin.Pmd.IPEPmd)";

        private const string PmxApplyWithUnrelatedType = "PEPlugin.Vmd.IPEVmd.Apply(PEPlugin.Pmx.IPXBone)";

        private const string PmdLoad = "PEPlugin.Vmd.IPEVmd.Load(PEPlugin.Pmd.IPEPmd)";

        private const string PmxLoadWithTypeArgument = "PEPlugin.Vmd.IPEVmd.Load<1>(PEPlugin.Pmx.IPXPmx)";

        private const string PmdStore = "PEPlugin.Vmd.IPEVmd.Store(PEPlugin.Pmd.IPEPmd)";

        private const string PmxStoreByReference = "PEPlugin.Vmd.IPEVmd.Store(ref PEPlugin.Pmx.IPXPmx)";

        private const string PmdSave = "PEPlugin.Vmd.IPEVmd.Save(PEPlugin.Pmd.IPEPmd)";

        private const string FrozenPmxSave = "PEPlugin.Vmd.IPEVmd.Save(PEPlugin.Pmx.IPXPmx)";

        private const string PmdInitOnOtherType = "PEPlugin.Vme.IPEVme.Init(PEPlugin.Pmd.IPEPmd)";

        private const string FrozenFactoryConstructor = "PXCPlugin.PXFrozenInfo..ctor()";

        private const string FrozenFactory = "PXCPlugin.IPXSystemControl.GetFrozenInfo(System.Int32)";

        private const string PmdWithoutAlternative = "PEPlugin.Vmd.IPEVmd.SetNamesFromPmd(PEPlugin.Pmd.IPEPmd)";

        private const string PmdInMemberNameOnly = "PEPlugin.Form.IPEFormConnector.SavePMDFile(System.String)";

        private const string PmdInValueType = "PEPlugin.IPEConnector.Pmd()";

        private const string FromStream = "PEPlugin.Pmx.IPXPmx.FromStream(System.IO.Stream)";

        private const string ToStream = "PEPlugin.Pmx.IPXPmx.ToStream(System.IO.Stream)";

        private const string StreamInValueType = "PEPlugin.Pmx.IPXPmx.OpenStream()";

        private const string FromFile = "PEPlugin.Pmx.IPXPmx.FromFile(System.String)";

        private const string ToFile = "PEPlugin.Pmx.IPXPmx.ToFile(System.String)";

        private const string DelegateOverload =
            "PEPlugin.Vme.IPEVmeSingleValueEventOperator.Set(System.Int32,PEPlugin.Vme.StateValueProc)";

        private const string ValueOverload =
            "PEPlugin.Vme.IPEVmeSingleValueEventOperator.Set(System.Int32,System.Single)";

        private const string DuplicatedConstructor = "PXCPlugin.PXPluginInfo..ctor()";

        private const string PluginInfoFactory = "PXCPlugin.IPXSystemControl.GetCPluginInfo(System.Int32)";

        private const string CPluginArgumentOverload =
            "PXCPlugin.IPXSystemControl.GetCPluginInfo(PXCPlugin.IPXCPlugin)";

        private const string CPluginConnectorMember = "PXCPlugin.IPXCPluginConnector.GetSelectedBoneIndices()";

        private const string CPluginConnectorArgument =
            "PXCPlugin.IPXSystemControl.Attach(PXCPlugin.IPXCPluginConnector)";

        private const string CPluginInValueType = "PXCPlugin.IPXSystemControl.GetCPlugin(System.Int32)";

        private const string DelegateInValueType =
            "PEPlugin.Vme.IPEVmeSingleValueEventOperator.Changed()";

        private const string ConstructorWithFactoryOnSameType = "PEPlugin.Vme.PEVmeFilter..ctor()";

        private const string StaticFactoryOnSameType = "PEPlugin.Vme.PEVmeFilter.Create()";

        private const string ConstructorWithStaticFactory =
            "PXCPlugin.UIModel.PXUIModelHelper+TextControl..ctor()";

        private const string StaticFactory = "PXCPlugin.UIModel.PXUIModelHelper.CreateTextControl()";

        private const string ConstructorWithPropertyOnly = "PEPlugin.Vme.PEVmeOption..ctor()";

        private const string OptionProperty = "PEPlugin.Vme.IPEVme.Option()";

        private const string CloneOnlyConstructor = "PEPlugin.Vme.PEVmePreviewOption..ctor()";

        private const string CloneMember = "PEPlugin.Vme.PEVmePreviewOption.Clone()";

        private const string StreamDerivedArgument = "PEPlugin.Pmx.IPXPmx.WriteTo(PEPlugin.Pmx.PXStream)";

        private const string ExternalDelegateOverload =
            "PEPlugin.Vme.IPEVmePath.GetPathPoints(System.Func<System.Double,System.Double>)";

        private const string PlainPathPoints = "PEPlugin.Vme.IPEVmePath.GetPathPoints(System.Double)";

        private const string DelegateWithoutPlain =
            "PEPlugin.Vme.IPEVmeGroup.ForEach(PEPlugin.Vme.StateValueProc)";

        private const string PmdConstructor = "PEPlugin.Vmd.PEVmdKey..ctor(PEPlugin.Pmd.IPEPmd)";

        private const string PmxConstructor = "PEPlugin.Vmd.PEVmdKey..ctor(PEPlugin.Pmx.IPXPmx)";

        private const string KeyFactory = "PEPlugin.Vmd.IPEVmd.CreateKey()";

        private const string ConstructorWithPmdFactory = "PXCPlugin.PXPmdInfo..ctor()";

        private const string PmdFactory = "PXCPlugin.IPXSystemControl.GetPmdInfo(PEPlugin.Pmd.IPEPmd)";

        private const string ConstructorWithDelegateFactory = "PXCPlugin.PXDelegateInfo..ctor()";

        private const string ExcludedFactory =
            "PXCPlugin.IPXSystemControl.GetDelegateInfo(PXCPlugin.IPXCPlugin)";

        private const string ConstructorNamedLikeTypeArgument = "T..ctor()";

        private const string TypeArgumentFactory = "PEPlugin.Vmd.IPEVmd.Make<1>()";

        private const string PmdWithTypeArgument = "PEPlugin.Vmd.IPEVmd.Bind<1>(PEPlugin.Pmd.IPEPmd,T)";

        private const string PmxWithRealTypeNamedT = "PEPlugin.Vmd.IPEVmd.Bind<1>(PEPlugin.Pmx.IPXPmx,T)";

        private const string OutsideEveryCategory = "PEPlugin.Pmx.IPXBone.Name()";

        private static InventoryRecord Inventory()
        {
            List<TypeRecord> types = new List<TypeRecord>
            {
                Type("PEPlugin.SDX.M", TypeKind.Class),
                Type("PEPlugin.Vmd.IPEVmd", TypeKind.Interface),
                Type("PEPlugin.Pmd.IPEPmd", TypeKind.Interface),
                Type("PEPlugin.Pmx.IPXPmx", TypeKind.Interface),
                Type("PEPlugin.Pmx.IPXBone", TypeKind.Interface),
                Type("PEPlugin.Form.IPEFormConnector", TypeKind.Interface),
                Type("PEPlugin.IPEConnector", TypeKind.Interface),
                Type("PEPlugin.Pmx.PXStream", TypeKind.Class, "System.IO.Stream"),
                Type("T", TypeKind.Class),
                Type("PEPlugin.Vmd.PEVmdKey", TypeKind.Class),
                Type("PXCPlugin.PXPmdInfo", TypeKind.Class),
                Type("PXCPlugin.PXDelegateInfo", TypeKind.Class),
                Type("PEPlugin.Vme.IPEVme", TypeKind.Interface),
                Type("PEPlugin.Vme.IPEVmePath", TypeKind.Interface),
                Type("PEPlugin.Vme.IPEVmeGroup", TypeKind.Interface),
                Type("PEPlugin.Vme.IPEVmeSingleValueEventOperator", TypeKind.Interface),
                Type("PEPlugin.Vme.StateValueProc", TypeKind.Delegate),
                Type("PEPlugin.Vme.PEVmePreviewOption", TypeKind.Class),
                Type("PEPlugin.Vme.PEVmeOption", TypeKind.Class),
                Type("PEPlugin.Vme.PEVmeFilter", TypeKind.Class),
                Type("PXCPlugin.UIModel.PXUIModelHelper", TypeKind.Class),
                Type("PXCPlugin.UIModel.PXUIModelHelper+TextControl", TypeKind.Class),
                Type("PXCPlugin.PXPluginInfo", TypeKind.Class),
                Type("PXCPlugin.PXFrozenInfo", TypeKind.Class),
                Type("PXCPlugin.IPXSystemControl", TypeKind.Interface),
                Type("PXCPlugin.IPXCPlugin", TypeKind.Interface),
                Type("PXCPlugin.IPXCPluginConnector", TypeKind.Interface),
            };

            List<SignatureRecord> signatures = new List<SignatureRecord>
            {
                Constructor("PEPlugin.SDX.M"),

                Method("PEPlugin.Vmd.IPEVmd", "Init", "System.Void", Arg("pmd", "PEPlugin.Pmd.IPEPmd")),
                Method("PEPlugin.Vmd.IPEVmd", "Init", "System.Void", Arg("pmx", "PEPlugin.Pmx.IPXPmx")),
                Method(
                    "PEPlugin.Vmd.IPEVmd",
                    "Init",
                    "System.Void",
                    Arg("pmx", "PEPlugin.Pmx.IPXPmx"),
                    Arg("clear", "System.Boolean")),

                Method("PEPlugin.Vmd.IPEVmd", "Reset", "System.Void", Arg("pmd", "PEPlugin.Pmd.IPEPmd")),
                Method(
                    "PEPlugin.Vmd.IPEVmd",
                    "Reset",
                    "System.Void",
                    Arg("pmx", "PEPlugin.Pmx.IPXPmx"),
                    Arg("clear", "System.Boolean")),

                Method("PEPlugin.Vmd.IPEVmd", "Merge", "System.Void", Arg("pmd", "PEPlugin.Pmd.IPEPmd")),
                Method("PEPlugin.Vmd.IPEVmd", "Merge", "System.Boolean", Arg("pmx", "PEPlugin.Pmx.IPXPmx")),

                Method("PEPlugin.Vmd.IPEVmd", "Apply", "System.Void", Arg("pmd", "PEPlugin.Pmd.IPEPmd")),
                Method("PEPlugin.Vmd.IPEVmd", "Apply", "System.Void", Arg("bone", "PEPlugin.Pmx.IPXBone")),

                Method("PEPlugin.Vmd.IPEVmd", "Load", "System.Void", Arg("pmd", "PEPlugin.Pmd.IPEPmd")),
                GenericMethod(
                    "PEPlugin.Vmd.IPEVmd", "Load", 1, "System.Void", Arg("pmx", "PEPlugin.Pmx.IPXPmx")),

                Method("PEPlugin.Vmd.IPEVmd", "Store", "System.Void", Arg("pmd", "PEPlugin.Pmd.IPEPmd")),
                Method("PEPlugin.Vmd.IPEVmd", "Store", "System.Void", RefArg("pmx", "PEPlugin.Pmx.IPXPmx")),

                Method("PEPlugin.Vmd.IPEVmd", "Save", "System.Void", Arg("pmd", "PEPlugin.Pmd.IPEPmd")),
                Method("PEPlugin.Vmd.IPEVmd", "Save", "System.Void", Arg("pmx", "PEPlugin.Pmx.IPXPmx")),

                Method("PEPlugin.Vme.IPEVme", "Init", "System.Void", Arg("pmd", "PEPlugin.Pmd.IPEPmd")),

                Method(
                    "PEPlugin.Vmd.IPEVmd", "SetNamesFromPmd", "System.Void", Arg("pmd", "PEPlugin.Pmd.IPEPmd")),
                Method(
                    "PEPlugin.Form.IPEFormConnector",
                    "SavePMDFile",
                    "System.Boolean",
                    Arg("path", "System.String")),
                Property("PEPlugin.IPEConnector", "Pmd", "PEPlugin.Pmd.IPEPmd"),

                Method("PEPlugin.Pmx.IPXPmx", "FromStream", "System.Boolean", Arg("s", "System.IO.Stream")),
                Method("PEPlugin.Pmx.IPXPmx", "ToStream", "System.Boolean", Arg("s", "System.IO.Stream")),
                Method("PEPlugin.Pmx.IPXPmx", "OpenStream", "System.IO.Stream"),
                Method("PEPlugin.Pmx.IPXPmx", "FromFile", "System.Boolean", Arg("path", "System.String")),
                Method("PEPlugin.Pmx.IPXPmx", "ToFile", "System.Boolean", Arg("path", "System.String")),
                Method(
                    "PEPlugin.Pmx.IPXPmx", "WriteTo", "System.Boolean", Arg("s", "PEPlugin.Pmx.PXStream")),

                Method(
                    "PEPlugin.Vme.IPEVmePath",
                    "GetPathPoints",
                    "System.Void",
                    Arg("f", "System.Func<System.Double,System.Double>")),
                Method("PEPlugin.Vme.IPEVmePath", "GetPathPoints", "System.Void", Arg("step", "System.Double")),
                Method(
                    "PEPlugin.Vme.IPEVmeGroup",
                    "ForEach",
                    "System.Void",
                    Arg("proc", "PEPlugin.Vme.StateValueProc")),

                Constructor("PEPlugin.Vmd.PEVmdKey", Arg("pmd", "PEPlugin.Pmd.IPEPmd")),
                Constructor("PEPlugin.Vmd.PEVmdKey", Arg("pmx", "PEPlugin.Pmx.IPXPmx")),
                Method("PEPlugin.Vmd.IPEVmd", "CreateKey", "PEPlugin.Vmd.PEVmdKey"),

                Constructor("PXCPlugin.PXPmdInfo"),
                Method(
                    "PXCPlugin.IPXSystemControl",
                    "GetPmdInfo",
                    "PXCPlugin.PXPmdInfo",
                    Arg("pmd", "PEPlugin.Pmd.IPEPmd")),

                Constructor("PXCPlugin.PXDelegateInfo"),
                Method(
                    "PXCPlugin.IPXSystemControl",
                    "GetDelegateInfo",
                    "PXCPlugin.PXDelegateInfo",
                    Arg("plugin", "PXCPlugin.IPXCPlugin")),

                Method(
                    "PEPlugin.Vme.IPEVmeSingleValueEventOperator",
                    "Set",
                    "System.Void",
                    Arg("frame", "System.Int32"),
                    Arg("proc", "PEPlugin.Vme.StateValueProc")),
                Method(
                    "PEPlugin.Vme.IPEVmeSingleValueEventOperator",
                    "Set",
                    "System.Void",
                    Arg("frame", "System.Int32"),
                    Arg("value", "System.Single")),
                Event(
                    "PEPlugin.Vme.IPEVmeSingleValueEventOperator", "Changed", "PEPlugin.Vme.StateValueProc"),

                Constructor("PXCPlugin.PXPluginInfo"),
                Method(
                    "PXCPlugin.IPXSystemControl",
                    "GetCPluginInfo",
                    "PXCPlugin.PXPluginInfo",
                    Arg("index", "System.Int32")),
                Method(
                    "PXCPlugin.IPXSystemControl",
                    "GetCPluginInfo",
                    "PXCPlugin.PXPluginInfo",
                    Arg("plugin", "PXCPlugin.IPXCPlugin")),
                Method("PXCPlugin.IPXCPluginConnector", "GetSelectedBoneIndices", "System.Int32[]"),
                Method(
                    "PXCPlugin.IPXSystemControl",
                    "Attach",
                    "System.Void",
                    Arg("connector", "PXCPlugin.IPXCPluginConnector")),
                Method(
                    "PXCPlugin.IPXSystemControl",
                    "GetCPlugin",
                    "PXCPlugin.IPXCPlugin",
                    Arg("index", "System.Int32")),

                Constructor("PXCPlugin.PXFrozenInfo"),
                Method(
                    "PXCPlugin.IPXSystemControl",
                    "GetFrozenInfo",
                    "PXCPlugin.PXFrozenInfo",
                    Arg("index", "System.Int32")),

                Constructor("PEPlugin.Vme.PEVmePreviewOption"),
                Method("PEPlugin.Vme.PEVmePreviewOption", "Clone", "PEPlugin.Vme.PEVmePreviewOption"),

                Constructor("PXCPlugin.UIModel.PXUIModelHelper+TextControl"),
                StaticMethod(
                    "PXCPlugin.UIModel.PXUIModelHelper",
                    "CreateTextControl",
                    "PXCPlugin.UIModel.PXUIModelHelper+TextControl"),

                Constructor("PEPlugin.Vme.PEVmeOption"),
                Property("PEPlugin.Vme.IPEVme", "Option", "PEPlugin.Vme.PEVmeOption"),

                Constructor("PEPlugin.Vme.PEVmeFilter"),
                StaticMethod("PEPlugin.Vme.PEVmeFilter", "Create", "PEPlugin.Vme.PEVmeFilter"),

                Constructor("T"),
                GenericMethod("PEPlugin.Vmd.IPEVmd", "Make", 1, "T", true),
                GenericMethod(
                    "PEPlugin.Vmd.IPEVmd",
                    "Bind",
                    1,
                    "System.Void",
                    false,
                    Arg("pmd", "PEPlugin.Pmd.IPEPmd"),
                    TypeArgument("value", "T")),
                GenericMethod(
                    "PEPlugin.Vmd.IPEVmd",
                    "Bind",
                    1,
                    "System.Void",
                    false,
                    Arg("pmx", "PEPlugin.Pmx.IPXPmx"),
                    Arg("value", "T")),

                Property("PEPlugin.Pmx.IPXBone", "Name", "System.String"),
            };

            List<TypeRecord> referencedTypes = new List<TypeRecord>
            {
                Type("System.Func<System.Double,System.Double>", TypeKind.Delegate),
                Type("System.IO.MemoryStream", TypeKind.Class, "System.IO.Stream"),
            };

            return new InventoryRecord(
                "PEPlugin",
                "0.0.8.9",
                new ReadOnlyCollection<TypeRecord>(types),
                new ReadOnlyCollection<TypeRecord>(referencedTypes),
                new ReadOnlyCollection<SignatureRecord>(
                    signatures.OrderBy(s => s.Key, StringComparer.Ordinal).ToList()));
        }

        private static TypeRecord Type(string name, TypeKind kind, params string[] baseTypes)
        {
            return new TypeRecord(
                name,
                kind,
                false,
                false,
                false,
                new ReadOnlyCollection<string>(baseTypes.ToList()),
                new ReadOnlyCollection<string>(new List<string>()));
        }

        private static ParameterRecord Arg(string name, string typeName)
        {
            return new ParameterRecord(name, typeName, ParameterDirection.In, false);
        }

        private static ParameterRecord RefArg(string name, string typeName)
        {
            return new ParameterRecord(name, typeName, ParameterDirection.Ref, false);
        }

        private static ParameterRecord TypeArgument(string name, string typeName)
        {
            return new ParameterRecord(name, typeName, ParameterDirection.In, false, true);
        }

        private static SignatureRecord Method(
            string declaringType, string memberName, string returnType, params ParameterRecord[] parameters)
        {
            return GenericMethod(declaringType, memberName, 0, returnType, parameters);
        }

        private static SignatureRecord GenericMethod(
            string declaringType,
            string memberName,
            int genericArity,
            string returnType,
            params ParameterRecord[] parameters)
        {
            return GenericMethod(declaringType, memberName, genericArity, returnType, false, parameters);
        }

        private static SignatureRecord GenericMethod(
            string declaringType,
            string memberName,
            int genericArity,
            string returnType,
            bool returnTypeIsTypeArgument,
            params ParameterRecord[] parameters)
        {
            return Signature(
                declaringType, MemberKind.Method, memberName, genericArity, parameters, returnType,
                false, false, false, OperationDirection.Write, returnTypeIsTypeArgument);
        }

        private static SignatureRecord StaticMethod(
            string declaringType, string memberName, string returnType, params ParameterRecord[] parameters)
        {
            return Signature(
                declaringType, MemberKind.Method, memberName, 0, parameters, returnType,
                true, false, false, OperationDirection.Write);
        }

        private static SignatureRecord Constructor(string declaringType, params ParameterRecord[] parameters)
        {
            return Signature(
                declaringType, MemberKind.Constructor, ".ctor", 0, parameters, declaringType,
                false, false, false, OperationDirection.Write);
        }

        private static SignatureRecord Event(string declaringType, string memberName, string handlerType)
        {
            return Signature(
                declaringType, MemberKind.Event, memberName, 0, new ParameterRecord[0], handlerType,
                false, false, false, OperationDirection.Write);
        }

        private static SignatureRecord Property(string declaringType, string memberName, string valueType)
        {
            return Signature(
                declaringType, MemberKind.Property, memberName, 0, new ParameterRecord[0], valueType,
                false, true, true, OperationDirection.Read);
        }

        private static SignatureRecord Signature(
            string declaringType,
            MemberKind memberKind,
            string memberName,
            int genericArity,
            ParameterRecord[] parameters,
            string valueType,
            bool isStatic,
            bool canRead,
            bool canWrite,
            OperationDirection operationDirection,
            bool valueTypeIsTypeArgument = false)
        {
            ReadOnlyCollection<ParameterRecord> declared =
                new ReadOnlyCollection<ParameterRecord>(parameters.ToList());

            return new SignatureRecord(
                SignatureKeyBuilder.Build(declaringType, memberName, genericArity, declared, valueType),
                declaringType,
                memberKind,
                memberName,
                isStatic,
                genericArity,
                declared,
                valueType,
                canRead,
                canWrite,
                operationDirection,
                valueTypeIsTypeArgument);
        }

        private static ExcludedBaselineEntry Entry(string capabilityId, params string[] signatures)
        {
            return new ExcludedBaselineEntry(
                capabilityId, new ReadOnlyCollection<string>(signatures.ToList()));
        }

        private static IList<ExcludedBaselineEntry> Baseline()
        {
            return new List<ExcludedBaselineEntry>
            {
                Entry("CAP-269", CPluginArgumentOverload, FrozenFactory),
                Entry("CAP-339", FromStream, StreamDerivedArgument, StreamInValueType, ToStream),
                Entry("CAP-390", FrozenPmxSave),
                Entry("CAP-466", FrozenConstructor),
            };
        }

        private static IList<ExcludedBaselineEntry> BaselineExcept(string key)
        {
            return Baseline()
                .Select(e => Entry(
                    e.CapabilityId,
                    e.Signatures.Where(s => s != key).ToArray()))
                .Where(e => e.Signatures.Count != 0)
                .ToList();
        }

        private static IList<ExcludedBaselineEntry> BaselineOf(params ExcludedBaselineEntry[] entries)
        {
            return new List<ExcludedBaselineEntry>(entries);
        }

        private static IList<ExcludedSignatureRecord> Build()
        {
            return ExcludedSignatureBuilder.Build(Baseline(), Inventory());
        }

        private static ExcludedSignatureRecord Find(string key)
        {
            ExcludedSignatureRecord found = Build().SingleOrDefault(r => r.Key == key);
            Assert.True(found != null, "除外されていない: " + key);
            return found;
        }

        [Fact]
        public void 題材の行キーが行キーの組み立て規則と一致する()
        {
            string[] keys = Inventory().Signatures.Select(s => s.Key).ToArray();

            Assert.All(
                new[]
                {
                    FrozenConstructor, PmdInit, PmxInit, PmxInitWithMoreArguments,
                    PmdReset, PmxResetWithMoreArguments, PmdMerge, PmxMergeWithOtherValueType,
                    PmdApply, PmxApplyWithUnrelatedType, PmdLoad, PmxLoadWithTypeArgument,
                    PmdStore, PmxStoreByReference, PmdSave, FrozenPmxSave, PmdInitOnOtherType,
                    PmdWithoutAlternative, PmdInMemberNameOnly, PmdInValueType,
                    FromStream, ToStream, StreamInValueType, FromFile, ToFile, DelegateOverload, ValueOverload,
                    DuplicatedConstructor, PluginInfoFactory, FrozenFactoryConstructor, FrozenFactory,
                    ConstructorWithStaticFactory, StaticFactory, ConstructorWithPropertyOnly, OptionProperty,
                    ConstructorWithFactoryOnSameType, StaticFactoryOnSameType,
                    CPluginArgumentOverload, CPluginConnectorMember, CPluginConnectorArgument,
                    CPluginInValueType, DelegateInValueType, StreamDerivedArgument, ExternalDelegateOverload,
                    PmdConstructor, PmxConstructor, KeyFactory, ConstructorWithPmdFactory, PmdFactory,
                    ConstructorNamedLikeTypeArgument, TypeArgumentFactory, PmdWithTypeArgument,
                    PmxWithRealTypeNamedT,
                    ConstructorWithDelegateFactory, ExcludedFactory,
                    CloneOnlyConstructor, CloneMember, OutsideEveryCategory,
                },
                key => Assert.Contains(key, keys));
        }

        [Fact]
        public void 凍結した組はベースラインを根拠に載る()
        {
            ExcludedSignatureRecord record = Find(FrozenConstructor);

            Assert.Equal(ExclusionQualification.Baseline, record.Qualification);
            Assert.Equal("CAP-466", record.CapabilityId);
            Assert.Equal(ExclusionCategory.None, record.Category);
            Assert.Equal(string.Empty, record.Alternative);
        }

        [Fact]
        public void 代替のあるPMD版はカテゴリを根拠に載る()
        {
            ExcludedSignatureRecord record = Find(PmdInit);

            Assert.Equal(ExclusionQualification.Category, record.Qualification);
            Assert.Equal(ExclusionCategory.Pmd, record.Category);
            Assert.Equal(PmxInit, record.Alternative);
            Assert.Equal(string.Empty, record.CapabilityId);
        }

        [Fact]
        public void 代替の無いPMD版は除外しない()
        {
            Assert.DoesNotContain(PmdWithoutAlternative, Build().Select(r => r.Key));
        }

        [Fact]
        public void 引数の個数が違うPMX版しか無いPMD版は除外しない()
        {
            Assert.DoesNotContain(PmdReset, Build().Select(r => r.Key));
        }

        [Fact]
        public void 戻り値が違うPMX版しか無いPMD版は除外しない()
        {
            Assert.DoesNotContain(PmdMerge, Build().Select(r => r.Key));
        }

        [Fact]
        public void 対応表に無い型を取るPMX版しか無いPMD版は除外しない()
        {
            Assert.DoesNotContain(PmdApply, Build().Select(r => r.Key));
        }

        [Fact]
        public void 総称型引数の数が違うPMX版しか無いPMD版は除外しない()
        {
            Assert.DoesNotContain(PmdLoad, Build().Select(r => r.Key));
        }

        [Fact]
        public void 引数の向きが違うPMX版しか無いPMD版は除外しない()
        {
            Assert.DoesNotContain(PmdStore, Build().Select(r => r.Key));
        }

        [Fact]
        public void 宣言型が違うPMX版しか無いPMD版は除外しない()
        {
            Assert.DoesNotContain(PmdInitOnOtherType, Build().Select(r => r.Key));
        }

        [Fact]
        public void 凍結されたPMX版は代替にならない()
        {
            Assert.DoesNotContain(PmdSave, Build().Select(r => r.Key));
        }

        [Fact]
        public void 代替には引数の個数が同じPMX版を選ぶ()
        {
            Assert.Equal(PmxInit, Find(PmdInit).Alternative);
        }

        [Fact]
        public void メンバー名だけがPMDを含むシグネチャは除外しない()
        {
            Assert.DoesNotContain(PmdInMemberNameOnly, Build().Select(r => r.Key));
        }

        [Fact]
        public void 戻り値だけがPMD型で代替の無いシグネチャは除外しない()
        {
            Assert.DoesNotContain(PmdInValueType, Build().Select(r => r.Key));
        }

        [Fact]
        public void デリゲートを取る版はカテゴリを根拠に載る()
        {
            ExcludedSignatureRecord record = Find(DelegateOverload);

            Assert.Equal(ExclusionQualification.Category, record.Qualification);
            Assert.Equal(ExclusionCategory.Delegate, record.Category);
            Assert.Equal(string.Empty, record.Alternative);
            Assert.DoesNotContain(ValueOverload, Build().Select(r => r.Key));
        }

        [Fact]
        public void Cプラグイン実装本体を取る版はカテゴリを根拠に載る()
        {
            IList<ExcludedSignatureRecord> records = ExcludedSignatureBuilder.Build(
                BaselineExcept(CPluginArgumentOverload), Inventory());
            ExcludedSignatureRecord record = records.Single(r => r.Key == CPluginArgumentOverload);

            Assert.Equal(ExclusionQualification.Category, record.Qualification);
            Assert.Equal(ExclusionCategory.CPluginArgument, record.Category);
            Assert.Equal(string.Empty, record.Alternative);
        }

        [Fact]
        public void 接続用インターフェースのメンバーは除外しない()
        {
            Assert.DoesNotContain(CPluginConnectorMember, Build().Select(r => r.Key));
        }

        [Fact]
        public void 接続用インターフェースを引数に取るシグネチャは除外しない()
        {
            Assert.DoesNotContain(CPluginConnectorArgument, Build().Select(r => r.Key));
        }

        [Fact]
        public void 戻り値だけがCプラグイン実装本体のシグネチャは除外しない()
        {
            Assert.DoesNotContain(CPluginInValueType, Build().Select(r => r.Key));
        }

        [Fact]
        public void デリゲート型をハンドラに持つイベントは除外しない()
        {
            Assert.DoesNotContain(DelegateInValueType, Build().Select(r => r.Key));
        }

        [Fact]
        public void 生成メンバーのある公開コンストラクタはカテゴリを根拠に載る()
        {
            ExcludedSignatureRecord record = Find(DuplicatedConstructor);

            Assert.Equal(ExclusionQualification.Category, record.Qualification);
            Assert.Equal(ExclusionCategory.ConstructorDuplicate, record.Category);
            Assert.Equal(PluginInfoFactory, record.Alternative);
        }

        [Fact]
        public void 自分自身を返すメンバーは代替にならない()
        {
            Assert.DoesNotContain(CloneOnlyConstructor, Build().Select(r => r.Key));
        }

        [Fact]
        public void 静的な生成メンバーのある公開コンストラクタもカテゴリを根拠に載る()
        {
            ExcludedSignatureRecord record = Find(ConstructorWithStaticFactory);

            Assert.Equal(ExclusionCategory.ConstructorDuplicate, record.Category);
            Assert.Equal(StaticFactory, record.Alternative);
        }

        [Fact]
        public void 同じ型を返すプロパティは生成メンバーに数えない()
        {
            Assert.DoesNotContain(ConstructorWithPropertyOnly, Build().Select(r => r.Key));
        }

        [Fact]
        public void 対象アセンブリの外のデリゲート型を取る版もカテゴリを根拠に載る()
        {
            ExcludedSignatureRecord record = Find(ExternalDelegateOverload);

            Assert.Equal(ExclusionCategory.Delegate, record.Category);
            Assert.Equal(string.Empty, record.Alternative);
        }

        [Fact]
        public void 代替の有無を問わずデリゲートを取る版は載る()
        {
            ExcludedSignatureRecord record = Find(DelegateWithoutPlain);

            Assert.Equal(ExclusionCategory.Delegate, record.Category);
        }

        [Fact]
        public void PMD型を取る公開コンストラクタも生成メンバーが在れば載る()
        {
            ExcludedSignatureRecord record = Find(PmdConstructor);

            Assert.Equal(ExclusionCategory.ConstructorDuplicate, record.Category);
            Assert.Equal(KeyFactory, record.Alternative);
        }

        [Fact]
        public void PMD型を取る生成メンバーでも除外されないなら代替に数える()
        {
            ExcludedSignatureRecord record = Find(ConstructorWithPmdFactory);

            Assert.Equal(ExclusionCategory.ConstructorDuplicate, record.Category);
            Assert.Equal(PmdFactory, record.Alternative);
        }

        [Fact]
        public void カテゴリで除外される生成メンバーは代替に数えない()
        {
            Assert.DoesNotContain(ConstructorWithDelegateFactory, Build().Select(r => r.Key));
        }

        [Fact]
        public void 総称型引数を返すメソッドは生成メンバーに数えない()
        {
            Assert.DoesNotContain(ConstructorNamedLikeTypeArgument, Build().Select(r => r.Key));
        }

        [Fact]
        public void 総称型引数と同じ名前の実型は代替の照合で別の型として扱う()
        {
            Assert.DoesNotContain(PmdWithTypeArgument, Build().Select(r => r.Key));
        }

        [Fact]
        public void 同じ宣言型の静的メソッドは生成メンバーに数えない()
        {
            Assert.DoesNotContain(ConstructorWithFactoryOnSameType, Build().Select(r => r.Key));
        }

        [Fact]
        public void 凍結された生成メンバーは代替にならない()
        {
            Assert.DoesNotContain(FrozenFactoryConstructor, Build().Select(r => r.Key));
        }

        [Theory]
        [InlineData(FromStream)]
        [InlineData(ToStream)]
        [InlineData(StreamInValueType)]
        [InlineData(StreamDerivedArgument)]
        public void 凍結されていないStream版が在ると例外になる(string key)
        {
            // 形式が同じかどうかは一次資料でしか決まらないので、除外するか残すかを機械で決められない。
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ExcludedSignatureBuilder.Build(BaselineExcept(key), Inventory()));

            Assert.Contains(key, error.Message);
        }

        [Fact]
        public void Stream版がすべて凍結されていれば止まらない()
        {
            string[] keys = Build().Select(r => r.Key).ToArray();

            Assert.Contains(FromStream, keys);
            Assert.Contains(ToStream, keys);
            Assert.Contains(StreamInValueType, keys);
        }

        [Fact]
        public void どの根拠も無いシグネチャは載らない()
        {
            Assert.DoesNotContain(OutsideEveryCategory, Build().Select(r => r.Key));
        }

        [Fact]
        public void 行キーの昇順で重複なく並ぶ()
        {
            string[] keys = Build().Select(r => r.Key).ToArray();

            Assert.Equal(keys.OrderBy(k => k, StringComparer.Ordinal).ToArray(), keys);
            Assert.Equal(
                new[]
                {
                    FrozenConstructor, FromStream, ToStream, StreamInValueType, StreamDerivedArgument,
                    CPluginArgumentOverload, FrozenFactory, FrozenPmxSave, PmdInit, DelegateOverload,
                    ExternalDelegateOverload, DelegateWithoutPlain, ExcludedFactory, DuplicatedConstructor,
                    ConstructorWithStaticFactory, ConstructorWithPmdFactory, PmdConstructor, PmxConstructor,
                }.OrderBy(k => k, StringComparer.Ordinal).ToArray(),
                keys);
        }

        [Fact]
        public void カテゴリの述語も満たす凍結した組は1件だけ載る()
        {
            IList<ExcludedSignatureRecord> records = ExcludedSignatureBuilder.Build(
                BaselineOf(
                    Entry("CAP-269", CPluginArgumentOverload),
                    Entry("CAP-339", FromStream, StreamDerivedArgument, StreamInValueType, ToStream),
                    Entry("CAP-390", PmdInit),
                    Entry("CAP-459", DuplicatedConstructor),
                    Entry("CAP-461", DelegateOverload)),
                Inventory());

            foreach (string key in new[]
                { CPluginArgumentOverload, FromStream, ToStream, PmdInit, DuplicatedConstructor, DelegateOverload })
            {
                ExcludedSignatureRecord record = records.Single(r => r.Key == key);
                Assert.Equal(ExclusionQualification.Baseline, record.Qualification);
                Assert.Equal(ExclusionCategory.None, record.Category);
            }
        }

        [Fact]
        public void 凍結した組が列挙と食い違うと例外になる()
        {
            InvalidOperationException error = Assert.Throws<InvalidOperationException>(
                () => ExcludedSignatureBuilder.Build(
                    BaselineOf(Entry("CAP-466", "PEPlugin.SDX.M.Removed()")), Inventory()));

            Assert.Contains("PEPlugin.SDX.M.Removed()", error.Message);
        }

        [Fact]
        public void 凍結した組や列挙を渡さないと例外になる()
        {
            Assert.Throws<ArgumentNullException>(() => ExcludedSignatureBuilder.Build(null, Inventory()));
            Assert.Throws<ArgumentNullException>(() => ExcludedSignatureBuilder.Build(Baseline(), null));
        }
    }
}
