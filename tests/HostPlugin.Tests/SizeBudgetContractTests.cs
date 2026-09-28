using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using Xunit;

namespace PmxEditorMcp.Tests
{
    public sealed class SizeBudgetContractTests
    {
        [Fact]
        public void TheDefaultResponseBudgetIsTheContracts()
        {
            Assert.Equal(ResponseBudget.DefaultChars, Budget("responseDefaultChars"));
        }

        [Fact]
        public void TheWarningRoomIsTheContracts()
        {
            Assert.Equal(ResponseSize.WarningChars, Budget("warningRoomChars"));
        }

        [Fact]
        public void TheRequestBudgetIsTheContracts()
        {
            Assert.Equal(RequestBudget.Bytes, Budget("requestBytes"));
        }

        [Fact]
        public void TheStructureTokenLimitIsTheContracts()
        {
            Assert.Equal(JsonRpcCodec.ParseStructureTokenLimit, Budget("structureTokenLimit"));
        }

        private static int Budget(string name)
        {
            IDictionary<string, object> contract = (IDictionary<string, object>)
                new JavaScriptSerializer().DeserializeObject(File.ReadAllText(Authored()));

            return (int)((IDictionary<string, object>)contract["budgets"])[name];
        }

        private static string Authored()
        {
            for (DirectoryInfo at = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                at != null;
                at = at.Parent)
            {
                string path = Path.Combine(at.FullName, "catalog", "authored", "common-contract.json");
                if (File.Exists(path))
                {
                    return path;
                }
            }

            throw new FileNotFoundException("共通契約の正本が見つからない。");
        }
    }
}
