using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PmxEditorMcp
{
    /// <summary>運搬用の型の項目1件。組から受け取った値を、その項目へ書き込む。</summary>
    public sealed class ToolValueMember
    {
        public ToolValueMember(string name, Type type, Action<object, object> write)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (write == null)
            {
                throw new ArgumentNullException(nameof(write));
            }

            Name = name;
            Type = type;
            Write = write;
        }

        /// <summary>要求の組に現れる項目の名前。</summary>
        public string Name { get; }

        /// <summary>その項目の宣言型。</summary>
        public Type Type { get; }

        /// <summary>作った実体へその項目を書き込む。</summary>
        public Action<object, object> Write { get; }
    }

    /// <summary>
    /// 呼び出す側から組で受け取り、SDKへ渡す実体へ組み立てる型。作り方と項目の書き込み方は開発時に
    /// 組み立てるので、配布物は実行時リフレクションを使わない。
    /// </summary>
    public sealed class ToolValueShape
    {
        public ToolValueShape(Func<object> create, IList<ToolValueMember> members)
        {
            if (create == null)
            {
                throw new ArgumentNullException(nameof(create));
            }

            if (members == null)
            {
                throw new ArgumentNullException(nameof(members));
            }

            Create = create;
            Members = new ReadOnlyCollection<ToolValueMember>(members);
        }

        /// <summary>何も書き込んでいない実体を作る。</summary>
        public Func<object> Create { get; }

        /// <summary>受け取る項目。</summary>
        public IList<ToolValueMember> Members { get; }
    }
}
