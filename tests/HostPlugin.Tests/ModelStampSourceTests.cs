using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using PEPlugin.Form;
using PEPlugin.Pmx;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class ModelStampSourceTests
    {
        public static IEnumerable<object[]> Names()
        {
            foreach (string name in new[]
            {
                "Updates", "HistoryMoves", "UndoCount", "RedoCount", "CurrentPath", "VertexItemsCount",
                "FaceItemsCount", "MaterialItemsCount", "BoneItemsCount", "MorphItemsCount",
                "NodeItemsCount", "BodyItemsCount", "JointItemsCount", "FileState",
            })
            {
                yield return new object[] { name };
            }
        }

        [Theory]
        [MemberData(nameof(Names))]
        public void AnyOneOfTheSourcesChangingChangesTheStamp(string changed)
        {
            Sources before = new Sources();
            ModelStamp first = before.Stamp();
            before.Change(changed);

            Assert.False(first.Equals(before.Stamp()));
        }

        [Fact]
        public void NothingChangingKeepsTheStamp()
        {
            Sources sources = new Sources();

            Assert.True(sources.Stamp().Equals(sources.Stamp()));
        }

        [Fact]
        public void ARealFileChangingSizeChangesTheStamp()
        {
            string path = System.IO.Path.GetTempFileName();
            try
            {
                System.IO.File.WriteAllText(path, "a");
                ModelStamp first = Read(path);
                System.IO.File.WriteAllText(path, "abc");

                Assert.False(first.Equals(Read(path)));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        [Fact]
        public void ARealFileChangingOnlyItsWriteTimeChangesTheStamp()
        {
            string path = System.IO.Path.GetTempFileName();
            try
            {
                System.IO.File.WriteAllText(path, "a");
                System.IO.File.SetLastWriteTimeUtc(path, new System.DateTime(2026, 1, 1, 0, 0, 0, System.DateTimeKind.Utc));
                ModelStamp first = Read(path);
                System.IO.File.SetLastWriteTimeUtc(path, new System.DateTime(2026, 1, 2, 0, 0, 0, System.DateTimeKind.Utc));

                Assert.False(first.Equals(Read(path)));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        [Fact]
        public void AnUnchangedRealFileKeepsTheStamp()
        {
            string path = System.IO.Path.GetTempFileName();
            try
            {
                Assert.True(Read(path).Equals(Read(path)));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        [Fact]
        public void AFileThatIsMissingOrUnsavedGivesAStableStamp()
        {
            string missing = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.Guid.NewGuid().ToString("N") + ".pmx");

            Assert.True(Read(missing).Equals(Read(missing)));
            Assert.True(Read(string.Empty).Equals(Read(string.Empty)));
        }

        [Fact]
        public void APathWithInvalidCharactersCannotBeStamped()
        {
            Assert.ThrowsAny<System.Exception>(() => Read("a<>|\"?.pmx"));
        }

        [Theory]
        [InlineData(@"\\server\share\a.pmx")]
        [InlineData("//server/share/a.pmx")]
        public void APathOnTheNetworkIsNotLookedUpAndCannotBeStamped(string path)
        {
            Assert.ThrowsAny<System.Exception>(() => Read(path));
        }

        private static ModelStamp Read(string path)
        {
            Dictionary<string, object> values = new Dictionary<string, object>
            {
                { "UndoCount", 0 },
                { "RedoCount", 0 },
                { "CurrentPath", path },
                { "VertexItemsCount", 0 },
                { "FaceItemsCount", 0 },
                { "MaterialItemsCount", 0 },
                { "BoneItemsCount", 0 },
                { "MorphItemsCount", 0 },
                { "NodeItemsCount", 0 },
                { "BodyItemsCount", 0 },
                { "JointItemsCount", 0 },
            };

            return ModelStampSource.Read(
                new Updates(),
                (IPEFormConnector)new Answers(typeof(IPEFormConnector), values).GetTransparentProxy(),
                (IPXPmxConnector)new Answers(typeof(IPXPmxConnector), values).GetTransparentProxy(),
                null);
        }

        private sealed class Sources
        {
            private readonly Dictionary<string, object> _values = new Dictionary<string, object>
            {
                { "UndoCount", 0 },
                { "RedoCount", 0 },
                { "CurrentPath", "a.pmx" },
                { "VertexItemsCount", 0 },
                { "FaceItemsCount", 0 },
                { "MaterialItemsCount", 0 },
                { "BoneItemsCount", 0 },
                { "MorphItemsCount", 0 },
                { "NodeItemsCount", 0 },
                { "BodyItemsCount", 0 },
                { "JointItemsCount", 0 },
            };

            private readonly Updates _updates = new Updates();

            private string _file = "1|100";

            public ModelStamp Stamp()
            {
                return ModelStampSource.Read(
                    _updates,
                    (IPEFormConnector)new Answers(typeof(IPEFormConnector), _values).GetTransparentProxy(),
                    (IPXPmxConnector)new Answers(typeof(IPXPmxConnector), _values).GetTransparentProxy(),
                    path => _file);
            }

            public void Change(string name)
            {
                if (name == "Updates")
                {
                    _updates.Count++;
                }
                else if (name == "HistoryMoves")
                {
                    _updates.HistoryMoves++;
                }
                else if (name == "FileState")
                {
                    _file = "2|100";
                }
                else if (name == "CurrentPath")
                {
                    _values[name] = "b.pmx";
                }
                else
                {
                    _values[name] = 1;
                }
            }
        }

        private sealed class Updates : IModelUpdates
        {
            public int Count { get; set; }

            public int HistoryMoves { get; set; }
        }

        private sealed class Answers : RealProxy
        {
            private readonly IDictionary<string, object> _values;

            public Answers(System.Type type, IDictionary<string, object> values)
                : base(type)
            {
                _values = values;
            }

            public override IMessage Invoke(IMessage message)
            {
                IMethodCallMessage call = (IMethodCallMessage)message;
                object value;
                if (call.MethodName.StartsWith("get_") && _values.TryGetValue(call.MethodName.Substring(4), out value))
                {
                    return new ReturnMessage(value, null, 0, call.LogicalCallContext, call);
                }

                return new ReturnMessage(new System.NotSupportedException(call.MethodName), call);
            }
        }
    }
}
