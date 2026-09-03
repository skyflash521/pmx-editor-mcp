using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>型をどう写像するかを決める役割。判定はこの順に評価し、最初に当たったものにする。</summary>
    public enum TypeRole
    {
        /// <summary>呼び出し側が実体を用意せずに呼べる、機能への接続点。</summary>
        Connector,

        /// <summary>公開イベントのハンドラの型引数に現れる型。</summary>
        EventArgs,

        /// <summary>ハンドルでのみ操作する型。</summary>
        HandleTarget,

        /// <summary>現在のPMX状態・エディタ状態の一部として編集・取得できる型。</summary>
        OperationTarget,

        /// <summary>上記以外で、入力または結果にのみ現れる型。</summary>
        Dto,
    }

    /// <summary>型役割表の型ごとの項目1件。</summary>
    public sealed class TypeRoleRecord
    {
        public TypeRoleRecord(
            string typeName,
            TypeRole role,
            string basis,
            string elementNoun = "",
            string elementNounPlural = "",
            string connectionPath = "")
        {
            PropertyRecord.RequireText(typeName, nameof(typeName));
            PropertyRecord.RequireText(basis, nameof(basis));
            if (elementNoun == null)
            {
                throw new ArgumentNullException(nameof(elementNoun));
            }

            if (elementNounPlural == null)
            {
                throw new ArgumentNullException(nameof(elementNounPlural));
            }

            if (connectionPath == null)
            {
                throw new ArgumentNullException(nameof(connectionPath));
            }

            TypeName = typeName;
            Role = role;
            Basis = basis;
            ElementNoun = elementNoun;
            ElementNounPlural = elementNounPlural;
            ConnectionPath = connectionPath;
        }

        public string TypeName { get; }

        public TypeRole Role { get; }

        /// <summary>その役割と判じた根拠の一文。</summary>
        public string Basis { get; }

        /// <summary>ツール名と説明文が対象を指すのに使う名詞。持たない役割では空。</summary>
        public string ElementNoun { get; }

        /// <summary>集合を扱うツール名が使う複数形。持たない役割では空。</summary>
        public string ElementNounPlural { get; }

        /// <summary>接続の根からその型へ至る経路。根と、経路を持たない型では空。</summary>
        public string ConnectionPath { get; }
    }

    /// <summary>ハンドルをどこから発行するか。</summary>
    public enum HandleIssuanceKind
    {
        /// <summary>公開コンストラクタ。レシーバーを持たない。</summary>
        Constructor,

        /// <summary>コネクタ型のメソッド。</summary>
        Factory,

        /// <summary>ハンドル操作型のメソッド。レシーバーのハンドルに対して発行する。</summary>
        ReceiverBound,
    }

    /// <summary>ハンドルを返しうるシグネチャ1件の判定。</summary>
    public sealed class HandleIssuanceRecord
    {
        public HandleIssuanceRecord(
            string signatureKey, bool issues, HandleIssuanceKind? kind, string basis)
        {
            PropertyRecord.RequireText(signatureKey, nameof(signatureKey));
            PropertyRecord.RequireText(basis, nameof(basis));
            if (issues != kind.HasValue)
            {
                throw new ArgumentException(
                    "発行するときだけ種別を持つ。", issues ? nameof(kind) : nameof(issues));
            }

            SignatureKey = signatureKey;
            Issues = issues;
            Kind = kind;
            Basis = basis;
        }

        public string SignatureKey { get; }

        /// <summary>新しいハンドルを発行するか。既にあるものを返すだけなら偽。</summary>
        public bool Issues { get; }

        /// <summary><see cref="Issues"/> が偽のときは持たない。</summary>
        public HandleIssuanceKind? Kind { get; }

        /// <summary>そう判じた根拠の一文。</summary>
        public string Basis { get; }
    }

    /// <summary>型役割表の正本。型ごとの役割と、ハンドル発行の判定からなる。</summary>
    public sealed class TypeRoleTable
    {
        public TypeRoleTable(IList<TypeRoleRecord> types, IList<HandleIssuanceRecord> issuances)
        {
            if (types == null)
            {
                throw new ArgumentNullException(nameof(types));
            }

            if (issuances == null)
            {
                throw new ArgumentNullException(nameof(issuances));
            }

            Types = new ReadOnlyCollection<TypeRoleRecord>(types);
            Issuances = new ReadOnlyCollection<HandleIssuanceRecord>(issuances);
        }

        public IList<TypeRoleRecord> Types { get; }

        public IList<HandleIssuanceRecord> Issuances { get; }
    }

    /// <summary>日本語名をどう決めたか。</summary>
    public enum NameDecision
    {
        /// <summary>配布物のドキュメントXMLの記載を採る。</summary>
        Quoted,

        /// <summary>使える記載が無いので名前を起こす。</summary>
        Authored,
    }

    /// <summary>起こした名前が拠る意味の根拠の種類。</summary>
    public enum NameBasisKind
    {
        /// <summary>配布物の資料が意味を説明している。</summary>
        DocumentSection,

        /// <summary>資料が意味を説明しておらず、宣言型・メンバー名・プロパティの型に拠る。</summary>
        MemberShape,
    }

    /// <summary>起こした名前が拠る意味の根拠。種類ごとのファクトリメソッドで作る。</summary>
    public sealed class NameBasis
    {
        private NameBasis(NameBasisKind kind, string path, int firstLine, int lastLine)
        {
            Kind = kind;
            Path = path;
            FirstLine = firstLine;
            LastLine = lastLine;
        }

        public NameBasisKind Kind { get; }

        /// <summary>配布物からの相対パス。<see cref="NameBasisKind.MemberShape"/> では空。</summary>
        public string Path { get; }

        /// <summary>1から数える開始行。<see cref="NameBasisKind.MemberShape"/> では0。</summary>
        public int FirstLine { get; }

        /// <summary>1から数える終了行。両端を含む。<see cref="NameBasisKind.MemberShape"/> では0。</summary>
        public int LastLine { get; }

        /// <summary>配布物の資料の範囲を根拠とする1件を作る。</summary>
        public static NameBasis FromDocumentSection(string path, int firstLine, int lastLine)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (path.Trim().Length == 0)
            {
                throw new ArgumentException("空にも空白だけにもできない。", nameof(path));
            }

            if (firstLine < 1)
            {
                throw new ArgumentException("行は1から数える。", nameof(firstLine));
            }

            if (lastLine < firstLine)
            {
                throw new ArgumentException("終了行が開始行より前にある。", nameof(lastLine));
            }

            return new NameBasis(NameBasisKind.DocumentSection, path, firstLine, lastLine);
        }

        /// <summary>宣言型・メンバー名・プロパティの型を根拠とする1件を作る。</summary>
        public static NameBasis FromMemberShape()
        {
            return new NameBasis(NameBasisKind.MemberShape, string.Empty, 0, 0);
        }
    }

    /// <summary>型役割表が日本語名を持つ、読み取り可能な公開プロパティ1件。</summary>
    public sealed class PropertyRecord
    {
        public PropertyRecord(string declaringType, string memberName, string propertyType)
        {
            RequireText(declaringType, nameof(declaringType));
            RequireText(memberName, nameof(memberName));
            RequireText(propertyType, nameof(propertyType));

            DeclaringType = declaringType;
            MemberName = memberName;
            PropertyType = propertyType;
        }

        public string DeclaringType { get; }

        public string MemberName { get; }

        public string PropertyType { get; }

        /// <summary>列挙結果と表の項目を突き合わせる鍵。</summary>
        public string Key
        {
            get { return DeclaringType + "|" + MemberName + "|" + PropertyType; }
        }

        internal static void RequireText(string value, string name)
        {
            if (value == null)
            {
                throw new ArgumentNullException(name);
            }

            if (value.Trim().Length == 0)
            {
                throw new ArgumentException("空にも空白だけにもできない。", name);
            }
        }
    }

    /// <summary>型役割表の日本語名1件。決め方ごとのファクトリメソッドで作る。</summary>
    public sealed class PropertyNameRecord
    {
        private PropertyNameRecord(
            PropertyRecord property,
            string japaneseName,
            NameDecision decision,
            NameBasis basis,
            string origin)
        {
            Property = property;
            JapaneseName = japaneseName;
            Decision = decision;
            Basis = basis;
            Origin = origin;
        }

        public PropertyRecord Property { get; }

        public string JapaneseName { get; }

        public NameDecision Decision { get; }

        /// <summary><see cref="NameDecision.Quoted"/> では null。</summary>
        public NameBasis Basis { get; }

        /// <summary>名前の由来の一文。<see cref="NameDecision.Quoted"/> では空。</summary>
        public string Origin { get; }

        /// <summary>ドキュメントXMLの記載を採った1件を作る。</summary>
        public static PropertyNameRecord FromQuoted(PropertyRecord property, string japaneseName)
        {
            RequireProperty(property);
            PropertyRecord.RequireText(japaneseName, nameof(japaneseName));

            return new PropertyNameRecord(
                property, japaneseName, NameDecision.Quoted, null, string.Empty);
        }

        /// <summary>名前を起こした1件を作る。</summary>
        public static PropertyNameRecord FromAuthored(
            PropertyRecord property, string japaneseName, NameBasis basis, string origin)
        {
            RequireProperty(property);
            PropertyRecord.RequireText(japaneseName, nameof(japaneseName));
            if (basis == null)
            {
                throw new ArgumentNullException(nameof(basis));
            }

            PropertyRecord.RequireText(origin, nameof(origin));

            return new PropertyNameRecord(
                property, japaneseName, NameDecision.Authored, basis, origin);
        }

        private static void RequireProperty(PropertyRecord property)
        {
            if (property == null)
            {
                throw new ArgumentNullException(nameof(property));
            }
        }
    }
}
