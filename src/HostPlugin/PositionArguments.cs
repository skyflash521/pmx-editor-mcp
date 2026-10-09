using System;
using System.Collections.Generic;
using System.Linq;
using PEPlugin.Pmx;
using PXCPlugin.UIModel;

namespace PmxEditorMcp
{
    public static class PositionArguments
    {
        private enum Source
        {
            Material,
            Vertex,
            Face,
            Bone,
            Body,
            Joint,
            UiBone,
            UiVertex,
            UiMorph,
            UiMaterial,
            Unbounded,
            Row,
        }

        private static readonly IDictionary<string, Rule[]> Current =
            new Dictionary<string, Rule[]>(StringComparer.Ordinal)
            {
                { "SetSelectedMaterialIndices", One("indices", "材質の数", Source.Material) },
                { "SetSelectedVertexIndices", One("indices", "頂点の数", Source.Vertex) },
                { "SetVertexIndices", One("indices", "頂点の数", Source.Vertex) },
                { "SetSelectedFaceIndices", One("indices", "面の数", Source.Face) },
                { "SetSelectedBoneIndices", One("indices", "ボーンの数", Source.Bone) },
                { "SetSelectedBodyIndices", One("indices", "剛体の数", Source.Body) },
                { "SetSelectedJointIndices", One("indices", "Jointの数", Source.Joint) },
                { "SetVertexMemory", One("indices", "頂点の数", Source.Vertex) },
                { "UpdateModel_Material", One("index", "材質の数", Source.Material) },
            };

        public const string PathPointsRow = "PEPlugin.Vme.IPEVmePath.GetDistanceAtPoint(System.Int32)";

        private static readonly IDictionary<string, Rule[]> Later = BuildLater();

        public static bool TracksCurrent(string rowKey)
        {
            return Current.ContainsKey(MemberOf(rowKey));
        }

        public static bool TracksLater(string rowKey)
        {
            return Later.ContainsKey(Declared(rowKey));
        }

        public static bool TryCurrent(
            string rowKey,
            IList<string> names,
            object[] values,
            IPXPmx model,
            out string code,
            out string message)
        {
            return Check(Current[MemberOf(rowKey)], names, values, model, null, null, out code, out message);
        }

        public static bool TryLater(
            string rowKey,
            IList<string> names,
            object[] values,
            IPXPmx model,
            object receiver,
            Func<string, object, int?> count,
            out string code,
            out string message)
        {
            return Check(Later[Declared(rowKey)], names, values, model, receiver, count, out code, out message);
        }

        private static bool Check(
            IEnumerable<Rule> rules,
            IList<string> names,
            object[] values,
            IPXPmx model,
            object receiver,
            Func<string, object, int?> count,
            out string code,
            out string message)
        {
            code = null;
            message = null;
            foreach (Rule rule in rules)
            {
                int at = names.IndexOf(rule.Argument);
                if (at < 0 || at >= values.Length || values[at] == null)
                {
                    continue;
                }

                int? ceiling = Ceiling(rule, names, values, model, receiver, count);
                if (ceiling == null)
                {
                    continue;
                }

                foreach (int index in rule.Pick == null ? Indices(values[at]) : rule.Pick(values[at]))
                {
                    if (!PositionInput.TryWithin(
                        index, ceiling.Value, rule.Label ?? rule.Argument, out code, out message, rule.CountName))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static IEnumerable<int> Indices(object value)
        {
            int[] many = value as int[];
            if (many != null)
            {
                return many;
            }

            return value is int ? new[] { (int)value } : new int[0];
        }

        private static int? Ceiling(
            Rule rule,
            IList<string> names,
            object[] values,
            IPXPmx model,
            object receiver,
            Func<string, object, int?> count)
        {
            if (rule.Source == Source.Unbounded)
            {
                return PositionInput.Unbounded;
            }

            if (rule.Source >= Source.UiBone && rule.Source <= Source.UiMaterial)
            {
                object holder = receiver;
                if (rule.CountFrom != null)
                {
                    int held = names.IndexOf(rule.CountFrom);
                    holder = held < 0 || held >= values.Length ? null : values[held];
                }

                UiModelCounts.Counts counts;
                if (!UiModelCounts.TryGet(holder, out counts))
                {
                    return null;
                }

                return rule.Source == Source.UiBone ? counts.Bone
                    : rule.Source == Source.UiVertex ? counts.Vertex
                    : rule.Source == Source.UiMorph ? counts.Morph
                    : counts.Material;
            }

            if (rule.Source != Source.Row)
            {
                if (model == null)
                {
                    return null;
                }

                switch (rule.Source)
                {
                    case Source.Material:
                        return model.Material.Count;
                    case Source.Face:
                        return model.Material.Sum(material => material.Faces.Count);
                    case Source.Bone:
                        return model.Bone.Count;
                    case Source.Body:
                        return model.Body.Count;
                    case Source.Joint:
                        return model.Joint.Count;
                    default:
                        return model.Vertex.Count;
                }
            }

            object owner = receiver;
            if (rule.CountFrom != null)
            {
                int from = names.IndexOf(rule.CountFrom);
                if (from < 0 || from >= values.Length || values[from] == null)
                {
                    return null;
                }

                owner = values[from];
            }

            int? counted = count == null ? null : count(rule.CountRow, owner);

            return counted == null ? null : (int?)Math.Max(0, counted.Value);
        }

        private static IDictionary<string, Rule[]> BuildLater()
        {
            Dictionary<string, Rule[]> rules = new Dictionary<string, Rule[]>(StringComparer.Ordinal);
            string[] builders = { "AddBox", "AddCylinder", "AddPlane", "AddSphere", "AddText", "AddTorus" };
            foreach (string builder in builders)
            {
                rules["PEPlugin.Pmx.IPXPrimitiveBuilder." + builder] = One("material", "材質の数", Source.Material);
                rules["PXCPlugin.IPXCPrimitiveBuilder." + builder] = One("material", "材質の数", Source.Material);
            }

            foreach (string member in new[] { "GetPluginInfo", "RunPlugin" })
            {
                rules["PEPlugin.IPESystemConnector." + member] =
                    Row("n", "登録されているプラグインの数", "PEPlugin.IPESystemConnector.RegisteredPluginCount()");
            }

            foreach (string member in new[] { "GetCPluginInfo", "RunCPlugin" })
            {
                rules["PEPlugin.IPESystemConnector." + member] =
                    Row("n", "登録されているCプラグインの数", "PEPlugin.IPESystemConnector.RegisteredCPluginCount()");
                rules["PXCPlugin.IPXSystemControl." + member] =
                    Row("n", "Cプラグインの数", "PXCPlugin.IPXSystemControl.CPluginCount()");
            }

            foreach (string member in new[] { "GetPEPluginInfo", "RunPEPlugin" })
            {
                rules["PXCPlugin.IPXSystemControl." + member] =
                    Row("n", "PEプラグインの数", "PXCPlugin.IPXSystemControl.PEPluginCount()");
            }

            rules["PEPlugin.Vmd.IPEVmd.GetBoneName"] =
                Row("index", "VMDのボーン名の数", "PEPlugin.Vmd.IPEVmd.GetBoneNames()");
            rules["PEPlugin.Vmd.IPEVmd.GetMorphName"] =
                Row("index", "VMDのモーフ名の数", "PEPlugin.Vmd.IPEVmd.GetMorphNames()");
            rules["PEPlugin.Vme.IPEVmeGroupBone.GetBone"] =
                Row("boneIndex", "グループの要素の数", "PEPlugin.Vme.IPEVmeGroup.GroupCount()");
            rules["PEPlugin.Vme.IPEVmeGroupMorph.GetMorph"] =
                Row("boneIndex", "グループの要素の数", "PEPlugin.Vme.IPEVmeGroup.GroupCount()");
            rules["PEPlugin.Vme.IPEVmeGroup.GetElement"] =
                Row("index", "グループの要素の数", "PEPlugin.Vme.IPEVmeGroup.GroupCount()");
            rules["PEPlugin.Vme.IPEVme.CreateGroupBone"] =
                Row("index", "VMEのボーンの数", "PEPlugin.Vme.IPEVmeObject.Bone()");
            rules["PEPlugin.Vme.IPEVme.CreateGroupMorph"] =
                Row("index", "VMEのモーフの数", "PEPlugin.Vme.IPEVmeObject.Morph()");
            rules["PEPlugin.Vme.IPEVmeResult.Bone"] =
                Row("boneIndex", "結果のボーンの数", "PEPlugin.Vme.IPEVmeResult.BoneCount()");
            rules["PEPlugin.Vme.IPEVmeResult.GetBoneResult"] =
                Row("boneIndex", "結果のボーンの数", "PEPlugin.Vme.IPEVmeResult.BoneCount()");
            rules["PEPlugin.Vme.IPEVmeResult.Morph"] =
                Row("morphIndex", "結果のモーフの数", "PEPlugin.Vme.IPEVmeResult.MorphCount()");
            rules["PEPlugin.Vme.IPEVmeResult.GetMorphResult"] =
                Row("morphIndex", "結果のモーフの数", "PEPlugin.Vme.IPEVmeResult.MorphCount()");
            string ui = "PXCPlugin.UIModel.IPXUIModel.";
            foreach (string member in new[] { "GetTransformedBoneMatrix", "GetTransformedBonePosition", "SetBoneRotate", "SetBoneScale", "SetBoneTranslate", "SetBone" })
            {
                rules[ui + member] = new[] { new Rule("bx", "UIモデルのボーンの数", Source.UiBone, null, null) };
            }

            foreach (string member in new[] { "GetTransformedVertexNormal", "GetTransformedVertexPosition" })
            {
                rules[ui + member] = new[] { new Rule("vx", "UIモデルの頂点の数", Source.UiVertex, null, null) };
            }

            rules[ui + "SetMorph"] = new[] { new Rule("mx", "UIモデルのモーフの数", Source.UiMorph, null, null) };
            foreach (string member in new[] { "SetBitmapTexture", "UpdateBitmapTexture", "UpdateMaterialColor", "UpdateMaterialEdge", "UpdateMaterialFlags" })
            {
                rules[ui + member] = new[] { new Rule("mx", "UIモデルの材質の数", Source.UiMaterial, null, null) };
            }

            rules[ui + "CreateEventListener"] = new[] { new Rule("materials", "UIモデルの材質の数", Source.UiMaterial, null, null) };
            rules["PXCPlugin.UIModel.PXUIModelHelper.CreateTextControl"] =
                new[] { new Rule("mx", "UIモデルの材質の数", Source.UiMaterial, null, "uim") };
            rules["PXCPlugin.UIModel.PXUIModelHelper.SetMouseOverColor"] = new[]
            {
                new Rule("para", "UIモデルの材質の数", Source.UiMaterial, null, "uim")
                {
                    Label = "para[].materialIndex",
                    Pick = value => ((PXUIModelHelper.MaterialColorEvPara[])value).Select(p => p.MaterialIndex),
                },
            };
            rules["PEPlugin.Vmd.IPEVmdVisibleIKKey.SetIK"] = new[] { new Rule("indices", null, Source.Unbounded, null, null) };
            rules["PEPlugin.Vmd.IPEVmdBonePoseState.ToBoneKeyArray"] =
                new[] { new Rule("boneIndex", null, Source.Unbounded, null, null) };
            rules["PEPlugin.Vme.IPEVmePath.GetDistanceAtPoint"] =
                Row("pos", "パスの点の数", PathPointsRow);
            rules["PEPlugin.IPEBuilder.CreateVmdBonePoseState"] = new[]
            {
                FromArgument("boneIndex", "vmd"),
                FromArgument("boneIndices", "vmd"),
            };
            rules["PEPlugin.Vmd.IPEVmdBonePoseState.FromVmd"] = new[] { FromArgument("boneIndex", "vmd") };

            return rules;
        }

        private static Rule FromArgument(string argument, string owner)
        {
            return new Rule(argument, "VMDのボーン名の数", Source.Row, "PEPlugin.Vmd.IPEVmd.GetBoneNames()", owner);
        }

        private static Rule[] One(string argument, string countName, Source source)
        {
            return new[] { new Rule(argument, countName, source, null, null) };
        }

        private static Rule[] Row(string argument, string countName, string countRow)
        {
            return new[] { new Rule(argument, countName, Source.Row, countRow, null) };
        }

        private static string MemberOf(string rowKey)
        {
            string declared = Declared(rowKey);

            return declared.Substring(declared.LastIndexOf('.') + 1);
        }

        private static string Declared(string rowKey)
        {
            int open = rowKey.IndexOf('(');

            return open < 0 ? rowKey : rowKey.Substring(0, open);
        }

        private sealed class Rule
        {
            public Rule(string argument, string countName, Source source, string countRow, string countFrom)
            {
                Argument = argument;
                CountName = countName;
                Source = source;
                CountRow = countRow;
                CountFrom = countFrom;
            }

            public string Argument { get; }

            public string CountName { get; }

            public Source Source { get; }

            public string CountRow { get; }

            public string CountFrom { get; }

            public string Label { get; set; }

            public Func<object, IEnumerable<int>> Pick { get; set; }
        }
    }
}
