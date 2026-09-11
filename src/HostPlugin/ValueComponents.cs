using System;
using System.Collections.Generic;
using PEPlugin.Pmd;
using PEPlugin.SDX;

namespace PmxEditorMcp
{
    /// <summary>
    /// 成分を並べる型の読み書き。成分は型ごとに直接書くので、名前で引く経路を持たない。読みと
    /// 組み立ては同じ並びを使い、その並びは <see cref="TryNames"/> が渡す。
    /// </summary>
    internal static class ValueComponents
    {
        private static readonly string[] XY = { "X", "Y" };

        private static readonly string[] XYZ = { "X", "Y", "Z" };

        private static readonly string[] XYZW = { "X", "Y", "Z", "W" };

        private static readonly string[] RowMajor =
        {
            "M11", "M12", "M13", "M14",
            "M21", "M22", "M23", "M24",
            "M31", "M32", "M33", "M34",
            "M41", "M42", "M43", "M44",
        };

        private static readonly Dictionary<string, string[]> Names =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                { "PEPlugin.SDX.V2", XY },
                { "PEPlugin.SDX.V3", XYZ },
                { "PEPlugin.SDX.V4", XYZW },
                { "PEPlugin.SDX.Q", XYZW },
                { "PEPlugin.SDX.M", RowMajor },
                { "PEPlugin.Pmd.IPEVector3", XYZ },
                { "PEPlugin.Pmd.IPEQuaternion", XYZW },
                { "SlimDX.Vector3", XYZ },
                { "SlimDX.Quaternion", XYZW },
                { "SlimDX.Matrix", RowMajor },
            };

        /// <summary>成分を並べる型なら、並べる順の成分名を渡す。</summary>
        internal static bool TryNames(string typeName, out string[] names)
        {
            if (typeName == null)
            {
                names = null;

                return false;
            }

            return Names.TryGetValue(typeName, out names);
        }

        /// <summary>
        /// 成分を並べる順に読み出す。型は宣言型の綴りで渡す——インターフェースで受け取った値も
        /// その宣言の並びで写すためである。
        /// </summary>
        internal static object[] Read(string typeName, object value)
        {
            switch (typeName)
            {
                case "PEPlugin.SDX.V2":
                    return Components((V2)value);
                case "PEPlugin.SDX.V3":
                    return Components((V3)value);
                case "PEPlugin.SDX.V4":
                    return Components((V4)value);
                case "PEPlugin.SDX.Q":
                    return Components((Q)value);
                case "PEPlugin.SDX.M":
                    return Components((M)value);
                case "PEPlugin.Pmd.IPEVector3":
                    return Components((IPEVector3)value);
                case "PEPlugin.Pmd.IPEQuaternion":
                    return Components((IPEQuaternion)value);
                case "SlimDX.Vector3":
                    return Components((SlimDX.Vector3)value);
                case "SlimDX.Quaternion":
                    return Components((SlimDX.Quaternion)value);
                case "SlimDX.Matrix":
                    return Components((SlimDX.Matrix)value);
                default:
                    throw new ArgumentOutOfRangeException(nameof(typeName), typeName, "成分を持たない型。");
            }
        }

        /// <summary>
        /// 成分から値を組み立てる。インターフェースの綴りには、そのインターフェースを満たす型を
        /// 充てる。
        /// </summary>
        internal static object Build(string typeName, float[] numbers)
        {
            switch (typeName)
            {
                case "PEPlugin.SDX.V2":
                    return new V2 { X = numbers[0], Y = numbers[1] };
                case "PEPlugin.SDX.V3":
                case "PEPlugin.Pmd.IPEVector3":
                    return new V3 { X = numbers[0], Y = numbers[1], Z = numbers[2] };
                case "PEPlugin.SDX.V4":
                    return new V4 { X = numbers[0], Y = numbers[1], Z = numbers[2], W = numbers[3] };
                case "PEPlugin.SDX.Q":
                case "PEPlugin.Pmd.IPEQuaternion":
                    return new Q { X = numbers[0], Y = numbers[1], Z = numbers[2], W = numbers[3] };
                case "PEPlugin.SDX.M":
                    return BuildM(numbers);
                case "SlimDX.Vector3":
                    return new SlimDX.Vector3 { X = numbers[0], Y = numbers[1], Z = numbers[2] };
                case "SlimDX.Quaternion":
                    return new SlimDX.Quaternion
                    {
                        X = numbers[0],
                        Y = numbers[1],
                        Z = numbers[2],
                        W = numbers[3],
                    };
                case "SlimDX.Matrix":
                    return BuildDrawingMatrix(numbers);
                default:
                    throw new ArgumentOutOfRangeException(nameof(typeName), typeName, "成分を持たない型。");
            }
        }

        private static object[] Components(V2 read)
        {
            return new object[] { read.X, read.Y };
        }

        private static object[] Components(V3 read)
        {
            return new object[] { read.X, read.Y, read.Z };
        }

        private static object[] Components(V4 read)
        {
            return new object[] { read.X, read.Y, read.Z, read.W };
        }

        private static object[] Components(Q read)
        {
            return new object[] { read.X, read.Y, read.Z, read.W };
        }

        private static object[] Components(M read)
        {
            return new object[]
            {
                read.M11, read.M12, read.M13, read.M14,
                read.M21, read.M22, read.M23, read.M24,
                read.M31, read.M32, read.M33, read.M34,
                read.M41, read.M42, read.M43, read.M44,
            };
        }

        private static object[] Components(IPEVector3 read)
        {
            return new object[] { read.X, read.Y, read.Z };
        }

        private static object[] Components(IPEQuaternion read)
        {
            return new object[] { read.X, read.Y, read.Z, read.W };
        }

        private static object[] Components(SlimDX.Vector3 read)
        {
            return new object[] { read.X, read.Y, read.Z };
        }

        private static object[] Components(SlimDX.Quaternion read)
        {
            return new object[] { read.X, read.Y, read.Z, read.W };
        }

        private static object[] Components(SlimDX.Matrix read)
        {
            return new object[]
            {
                read.M11, read.M12, read.M13, read.M14,
                read.M21, read.M22, read.M23, read.M24,
                read.M31, read.M32, read.M33, read.M34,
                read.M41, read.M42, read.M43, read.M44,
            };
        }

        private static M BuildM(float[] numbers)
        {
            return new M
            {
                M11 = numbers[0],
                M12 = numbers[1],
                M13 = numbers[2],
                M14 = numbers[3],
                M21 = numbers[4],
                M22 = numbers[5],
                M23 = numbers[6],
                M24 = numbers[7],
                M31 = numbers[8],
                M32 = numbers[9],
                M33 = numbers[10],
                M34 = numbers[11],
                M41 = numbers[12],
                M42 = numbers[13],
                M43 = numbers[14],
                M44 = numbers[15],
            };
        }

        private static SlimDX.Matrix BuildDrawingMatrix(float[] numbers)
        {
            return new SlimDX.Matrix
            {
                M11 = numbers[0],
                M12 = numbers[1],
                M13 = numbers[2],
                M14 = numbers[3],
                M21 = numbers[4],
                M22 = numbers[5],
                M23 = numbers[6],
                M24 = numbers[7],
                M31 = numbers[8],
                M32 = numbers[9],
                M33 = numbers[10],
                M34 = numbers[11],
                M41 = numbers[12],
                M42 = numbers[13],
                M43 = numbers[14],
                M44 = numbers[15],
            };
        }
    }
}
