using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PmxEditorMcp
{
    /// <summary>ツールが受け取る引数1件。並びはSDKのシグネチャの引数の並びと同じ。</summary>
    public sealed class ToolArgument
    {
        public ToolArgument(string name, Type type)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            Name = name;
            Type = type;
        }

        /// <summary>要求の引数の名前。</summary>
        public string Name { get; }

        /// <summary>その引数の宣言型。</summary>
        public Type Type { get; }
    }

    /// <summary>項目を集めるツールが持つ項目1件。</summary>
    public sealed class ToolField
    {
        public ToolField(string name, string rowKey, string receiverType, Type type)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (rowKey == null)
            {
                throw new ArgumentNullException(nameof(rowKey));
            }

            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            Name = name;
            RowKey = rowKey;
            ReceiverType = receiverType;
            Type = type;
        }

        /// <summary>応答と要求に現れる項目の名前。</summary>
        public string Name { get; }

        /// <summary>その項目を読み書きする行キー。</summary>
        public string RowKey { get; }

        /// <summary>受け手を引く鍵。静的なメンバーでは null。</summary>
        public string ReceiverType { get; }

        /// <summary>その項目の宣言型。</summary>
        public Type Type { get; }
    }

    /// <summary>SDKのメンバーへ中継するツール1件。</summary>
    public sealed class ToolCall
    {
        public ToolCall(
            string rowKey,
            string receiverType,
            DangerKind danger,
            IList<ToolArgument> arguments,
            Type result)
        {
            if (rowKey == null)
            {
                throw new ArgumentNullException(nameof(rowKey));
            }

            if (arguments == null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            RowKey = rowKey;
            ReceiverType = receiverType;
            Danger = danger;
            Arguments = new ReadOnlyCollection<ToolArgument>(arguments);
            Result = result;
        }

        /// <summary>呼ぶ行のキー。</summary>
        public string RowKey { get; }

        /// <summary>受け手を引く鍵。静的なメンバーでは null。</summary>
        public string ReceiverType { get; }

        /// <summary>取り返しの付かなさの種別。</summary>
        public DangerKind Danger { get; }

        /// <summary>受け取る引数。</summary>
        public IList<ToolArgument> Arguments { get; }

        /// <summary>返す値の宣言型。値を返さないメンバーでは null。</summary>
        public Type Result { get; }
    }

    /// <summary>項目を集めるツール1件。取得と更新のどちらかを受け持つ。</summary>
    public sealed class ToolFields
    {
        public ToolFields(bool writes, IList<ToolField> fields)
        {
            if (fields == null)
            {
                throw new ArgumentNullException(nameof(fields));
            }

            Writes = writes;
            Fields = new ReadOnlyCollection<ToolField>(fields);
        }

        /// <summary>項目を書き込む側か。偽なら読み取る側。</summary>
        public bool Writes { get; }

        /// <summary>集める項目。</summary>
        public IList<ToolField> Fields { get; }
    }
}
