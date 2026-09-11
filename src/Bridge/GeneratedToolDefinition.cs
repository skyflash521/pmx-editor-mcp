using System;

namespace PmxEditorMcp.Bridge
{
    /// <summary>
    /// ビルド時に組み立てたツール定義の1件。ブリッジは本文を持つだけで、定義を実行時に組み立てない。
    /// </summary>
    internal sealed class GeneratedToolDefinition
    {
        internal GeneratedToolDefinition(string name, string description, string inputSchema)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (description == null)
            {
                throw new ArgumentNullException(nameof(description));
            }

            if (inputSchema == null)
            {
                throw new ArgumentNullException(nameof(inputSchema));
            }

            Name = name;
            Description = description;
            InputSchema = inputSchema;
        }

        internal string Name { get; }

        internal string Description { get; }

        /// <summary>入力の形をJSON Schemaで綴ったもの。</summary>
        internal string InputSchema { get; }
    }
}
