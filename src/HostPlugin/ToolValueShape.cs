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

        public string Name { get; }

        public Type Type { get; }

        public Action<object, object> Write { get; }
    }

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

        public IList<ToolValueMember> Members { get; }
    }
}
