using System;

namespace PmxEditorMcp.Bridge
{
    internal sealed class GeneratedToolDefinition
    {
        internal GeneratedToolDefinition(
            string name, string description, string inputSchema, bool returnsImage, bool destructive)
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
            ReturnsImage = returnsImage;
            Destructive = destructive;
        }

        internal string Name { get; }

        internal string Description { get; }

        internal string InputSchema { get; }

        /// <summary>値が画像かどうか。真なら結果を画像の本文として返す。</summary>
        internal bool ReturnsImage { get; }

        internal bool Destructive { get; }
    }
}
