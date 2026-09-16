using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PmxEditorMcp.SignatureDump.Tests
{
    /// <summary>受け手のハンドルを得るまでに呼ぶツールの列の導き方。</summary>
    public sealed class ReceiverCallEvidenceTests
    {
        private const string Builder = "PEPlugin.IPEBuilder";

        private const string Vme = "PEPlugin.Vme.IPEVme";

        private const string Camera = "PEPlugin.Vme.IPEVmeCamera";

        private const string Position = "PEPlugin.Vme.IPEVmeCameraPosition";

        private const string Light = "PEPlugin.Vme.IPEVmeLight";

        private const string FrameKey = "PEPlugin.Vmd.IPEVmdFrameKey";

        private const string BoneKey = "PEPlugin.Vmd.IPEVmdBoneKey";

        private const string Stranded = "PEPlugin.Vme.IPEVmeStranded";

        private const string CreateVme = Builder + ".CreateVme()";

        private const string OpenVme = Builder + ".OpenVme()";

        private const string CameraOfVme = Vme + ".Camera()";

        private const string PositionOfCamera = Camera + ".Position()";

        private const string CreateLight = Builder + ".CreateLight()";

        private const string LightOfCamera = Camera + ".Light()";

        private const string CreateBoneKey = Builder + ".CreateVmdBoneKey()";

        private const string SelfOfStranded = Stranded + ".Self()";

        private const string IplOfFrameKey = FrameKey + ".Ipl()";

        private const string CamerasOfVme = Vme + ".Cameras()";

        private const string CreateVmeTool = "motion_create_vme";

        private const string OpenVmeTool = "motion_open_vme";

        private const string CameraTool = "motion_camera_vme";

        private const string PositionTool = "motion_position_vme_camera";

        private const string CreateLightTool = "motion_create_vme_light";

        private const string LightTool = "motion_light_vme_camera";

        private const string CreateBoneKeyTool = "motion_create_vmd_bone_key";

        private const string StrandedTool = "motion_self_vme_stranded";

        private const string IplTool = "motion_ipl_vmd_frame_key";

        private const string ListCamerasTool = "motion_list_vme_cameras";

        [Fact(Skip = "impl pending: 受け手を渡さずに呼べるツールが出す型を1段の列で引けるようにする")]
        public void ATypeMadeWithoutAHandleIsReachedInOneStep()
        {
            Assert.Equal(new[] { CreateVmeTool }, ByType()[Vme]);
        }

        [Fact(Skip = "impl pending: 別のハンドルの先にある型を、そこへ至る列を繋いで引けるようにする")]
        public void ATypeBehindAnotherHandleIsReachedThroughIt()
        {
            Assert.Equal(new[] { CreateVmeTool, CameraTool }, ByType()[Camera]);
        }

        [Fact(Skip = "impl pending: 列の長さに上限を置かない")]
        public void ThePathHasNoLimitOnHowManyStepsItTakes()
        {
            Assert.Equal(
                new[] { CreateVmeTool, CameraTool, PositionTool }, ByType()[Position]);
        }

        [Fact(Skip = "impl pending: 同じ型へ届く列が2つ以上あるとき段数の少ないものを採る")]
        public void TheShorterOfTwoPathsToTheSameTypeWins()
        {
            Assert.Equal(new[] { CreateLightTool }, ByType()[Light]);
        }

        [Fact(Skip = "impl pending: 段数が並ぶ列はツールの名前の綴りの順で先のものを採る")]
        public void PathsOfTheSameLengthAreSettledBySpelling()
        {
            Assert.True(
                string.CompareOrdinal(CreateVmeTool, OpenVmeTool) < 0,
                "題材は綴りの順で" + CreateVmeTool + "が先であることを前提にする。");
            Assert.Equal(new[] { CreateVmeTool }, ByType()[Vme]);
        }

        [Fact(Skip = "impl pending: 根から辿り着けない型を持たない")]
        public void ATypeThatNoPathReachesIsAbsent()
        {
            Assert.False(ByType().ContainsKey(Stranded));
        }

        [Fact(Skip = "impl pending: 派生型へ至る列を基底型の名前からも引けるようにする")]
        public void APathToADerivedTypeAnswersForItsBaseType()
        {
            Assert.Equal(new[] { CreateBoneKeyTool }, ByType()[FrameKey]);
        }

        [Fact(Skip = "impl pending: 受け手をハンドルで要るツールへ、その受け手へ至る列を与える")]
        public void AToolThatNeedsAHandleIsGivenThePathToItsReceiver()
        {
            Assert.Equal(new[] { CreateVmeTool }, ByTool()[CameraTool]);
        }

        [Fact(Skip = "impl pending: 受け手の型ちょうどを作る生成器が無くても派生型の列を与える")]
        public void AToolWhoseReceiverIsOnlyMadeAsADerivedTypeIsStillGivenAPath()
        {
            Assert.Equal(new[] { CreateBoneKeyTool }, ByTool()[IplTool]);
        }

        [Fact(Skip = "impl pending: 自分の行を持たない集約のツールへ、並べる要素を所有する型への列を与える")]
        public void AToolWithoutItsOwnRowIsGivenThePathToTheOwnerOfWhatItLists()
        {
            Assert.Equal(new[] { CreateVmeTool }, ByTool()[ListCamerasTool]);
        }

        [Fact(Skip = "impl pending: 受け手を渡さずに呼べるツールを列の対象にしない")]
        public void AToolCallableWithoutAHandleIsAbsent()
        {
            Assert.False(ByTool().ContainsKey(CreateVmeTool));
        }

        [Fact(Skip = "impl pending: 引数の欠けを呼び出しの時点で断る")]
        public void EveryArgumentIsRequired()
        {
            InventoryRecord inventory = Inventory();
            ToolMap map = Map();
            TypeRoleTable roles = Roles();
            IDictionary<string, string> named = Named();
            ToolSchemaTable schemas = Schemas();

            Assert.Throws<ArgumentNullException>(
                () => ReceiverCallEvidence.ByType(null, map, named, schemas));
            Assert.Throws<ArgumentNullException>(
                () => ReceiverCallEvidence.ByType(inventory, null, named, schemas));
            Assert.Throws<ArgumentNullException>(
                () => ReceiverCallEvidence.ByType(inventory, map, null, schemas));
            Assert.Throws<ArgumentNullException>(
                () => ReceiverCallEvidence.ByType(inventory, map, named, null));
            Assert.Throws<ArgumentNullException>(
                () => ReceiverCallEvidence.ByTool(null, map, roles, named, schemas));
            Assert.Throws<ArgumentNullException>(
                () => ReceiverCallEvidence.ByTool(inventory, null, roles, named, schemas));
            Assert.Throws<ArgumentNullException>(
                () => ReceiverCallEvidence.ByTool(inventory, map, null, named, schemas));
            Assert.Throws<ArgumentNullException>(
                () => ReceiverCallEvidence.ByTool(inventory, map, roles, null, schemas));
            Assert.Throws<ArgumentNullException>(
                () => ReceiverCallEvidence.ByTool(inventory, map, roles, named, null));
        }

        private static IDictionary<string, IList<string>> ByType()
        {
            return ReceiverCallEvidence.ByType(Inventory(), Map(), Named(), Schemas());
        }

        private static IDictionary<string, IList<string>> ByTool()
        {
            return ReceiverCallEvidence.ByTool(Inventory(), Map(), Roles(), Named(), Schemas());
        }

        private static InventoryRecord Inventory()
        {
            return new InventoryRecord(
                "PEPlugin",
                "0.0.0.0",
                new[]
                {
                    Type(Builder),
                    Type(Vme),
                    Type(Camera),
                    Type(Position),
                    Type(Light),
                    Type(FrameKey),
                    Type(BoneKey, FrameKey),
                    Type(Stranded),
                }.ToList(),
                new List<TypeRecord>(),
                new[]
                {
                    Made(Builder, "OpenVme", Vme),
                    Made(Builder, "CreateVme", Vme),
                    Made(Vme, "Camera", Camera),
                    Made(Camera, "Position", Position),
                    Made(Builder, "CreateLight", Light),
                    Made(Camera, "Light", Light),
                    Made(Builder, "CreateVmdBoneKey", BoneKey),
                    Made(Stranded, "Self", Stranded),
                    Made(FrameKey, "Ipl", FrameKey),
                    Listed(Vme, "Cameras", Camera),
                }.ToList());
        }

        /// <summary>ハンドルを出すと述べる行と、受け手だけを要る行を並べた能力対応表。</summary>
        private static ToolMap Map()
        {
            return new ToolMap(new[]
            {
                Issuing(OpenVme),
                Issuing(CreateVme),
                Issuing(CameraOfVme),
                Issuing(PositionOfCamera),
                Issuing(CreateLight),
                Issuing(LightOfCamera),
                Issuing(CreateBoneKey),
                Issuing(SelfOfStranded),
                Plain(IplOfFrameKey),
            });
        }

        private static ToolMapRow Issuing(string key)
        {
            return new ToolMapRow(
                key,
                ToolMapEditKind.Read,
                null,
                "ハンドルを出す。",
                new[]
                {
                    new Postcondition(
                        EffectType.HandleCreated,
                        string.Empty,
                        EffectCheckKind.Handle,
                        null,
                        null,
                        null,
                        EffectComparison.Exists,
                        null,
                        false,
                        null),
                },
                null,
                null);
        }

        private static ToolMapRow Plain(string key)
        {
            return new ToolMapRow(
                key, ToolMapEditKind.Read, null, "持っているものを返すだけである。", null, null, null);
        }

        private static IDictionary<string, string> Named()
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { OpenVme, OpenVmeTool },
                { CreateVme, CreateVmeTool },
                { CameraOfVme, CameraTool },
                { PositionOfCamera, PositionTool },
                { CreateLight, CreateLightTool },
                { LightOfCamera, LightTool },
                { CreateBoneKey, CreateBoneKeyTool },
                { SelfOfStranded, StrandedTool },
                { IplOfFrameKey, IplTool },
            };
        }

        private static ToolSchemaTable Schemas()
        {
            return new ToolSchemaTable(new[]
            {
                Free(OpenVmeTool),
                Free(CreateVmeTool),
                Free(CreateLightTool),
                Free(CreateBoneKeyTool),
                Held(CameraTool),
                Held(PositionTool),
                Held(LightTool),
                Held(StrandedTool),
                Held(IplTool),
                Held(ListCamerasTool),
            });
        }

        /// <summary>担当群を解いた型役割表。</summary>
        private static TypeRoleTable Roles()
        {
            TypeRoleRecord[] types =
            {
                Role(Vme, "vme", "vmes"),
                Role(Camera, "vme_camera", "vme_cameras"),
                Role(Position, "vme_camera_position", "vme_camera_positions"),
                Role(Light, "vme_light", "vme_lights"),
            };
            ElementCollectionRecord[] collections =
            {
                new ElementCollectionRecord(
                    CamerasOfVme, true, "題材。", new[] { CamerasOfVme }),
            };

            return new TypeRoleTable(types, new HandleIssuanceRecord[0], collections);
        }

        private static TypeRoleRecord Role(string typeName, string noun, string plural)
        {
            return new TypeRoleRecord(
                typeName,
                TypeRole.HandleTarget,
                "題材の根拠。",
                noun,
                plural,
                CapabilityOwner.MotionTransform);
        }

        /// <summary>受け手を渡さずに呼べるツール。</summary>
        private static ToolSchema Free(string tool)
        {
            return new ToolSchema(
                tool,
                new[]
                {
                    new SchemaBranch(
                        "only", null, null, new SchemaItem[0], new SchemaChoice[0]),
                },
                Output(),
                null);
        }

        /// <summary>受け手をハンドルで指すツール。</summary>
        private static ToolSchema Held(string tool)
        {
            return new ToolSchema(
                tool,
                new[]
                {
                    new SchemaBranch(
                        "only",
                        null,
                        null,
                        new[]
                        {
                            new SchemaItem(
                                "number", null, null, "handles", ItemOrigin.HostInput, true, null,
                                false, null, null, null, false, null),
                        },
                        new SchemaChoice[0]),
                },
                Output(),
                null);
        }

        private static SchemaItem Output()
        {
            return new SchemaItem(
                "number", null, null, null, ItemOrigin.HostOutput, null, null, false, null, null,
                null, false, null);
        }

        private static TypeRecord Type(string name, params string[] baseTypes)
        {
            return new TypeRecord(
                name,
                TypeKind.Interface,
                false,
                false,
                false,
                baseTypes.ToList(),
                new List<string>());
        }

        /// <summary>値を返すメソッド1件。</summary>
        private static SignatureRecord Made(
            string declaringType, string memberName, string valueType)
        {
            return new SignatureRecord(
                declaringType + "." + memberName + "()",
                declaringType,
                MemberKind.Method,
                memberName,
                false,
                0,
                new ParameterRecord[0],
                valueType,
                false,
                false,
                OperationDirection.Read);
        }

        /// <summary>要素を並べるリストのプロパティ1件。</summary>
        private static SignatureRecord Listed(
            string declaringType, string memberName, string elementType)
        {
            return new SignatureRecord(
                declaringType + "." + memberName + "()",
                declaringType,
                MemberKind.Property,
                memberName,
                false,
                0,
                new ParameterRecord[0],
                "System.Collections.Generic.IList<" + elementType + ">",
                true,
                false,
                OperationDirection.Read);
        }
    }
}
