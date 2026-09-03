using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;

namespace PmxEditorMcp.Bridge
{
    /// <summary>ホストが返したツールの包みを、MCPクライアントへ返す結果へ写す。</summary>
    public static class ToolEnvelopeResult
    {
        private const string OkName = "ok";

        private const string ValueName = "value";

        private const string ErrorName = "error";

        private const string CodeName = "code";

        private const string MessageName = "message";

        private const string WarningsName = "warnings";

        private const string WarningPrefix = "警告: ";

        /// <summary>予算を超えた本文の代わりに返す誤り。</summary>
        private const string TooLargeCode = "TOOL_RESPONSE_TOO_LARGE";

        /// <summary>
        /// 包みをツール結果へ写す。成功なら値を、失敗なら「コード: メッセージ」を本文にし、警告が
        /// あれば同じ本文の末尾へ行として足す。本文が <paramref name="budgetChars"/> を超えるときは
        /// 本文を返さず、大きすぎる旨の誤りにする。包みとして読めなければ
        /// <see cref="FormatException"/>——ホストの応答が契約から外れている。呼び出し側は、これを
        /// 受けたら接続を捨てて `BRIDGE_PROTOCOL_ERROR` にする。
        /// </summary>
        public static CallToolResult From(JsonNode result, string targetNotice, int budgetChars)
        {
            if (targetNotice == null)
            {
                throw new ArgumentNullException(nameof(targetNotice));
            }

            if (budgetChars < BridgeBudget.MinimumChars)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(budgetChars),
                    budgetChars,
                    BridgeBudget.MinimumChars + " 以上でなければならない。");
            }

            JsonObject envelope = result as JsonObject;
            if (envelope == null)
            {
                throw Broken("ツールの応答が包みの形でない。");
            }

            bool ok = Flag(envelope);
            string body = ok ? Value(envelope) : Failure(envelope);
            foreach (string warning in Warnings(envelope))
            {
                body += "\n" + WarningPrefix + warning;
            }

            if (body.Length > budgetChars)
            {
                ok = false;
                body = TooLargeCode + ": 応答が応答サイズ予算 " + budgetChars
                    + " 文字に収まらない(" + body.Length + " 文字)。";
            }

            return new CallToolResult
            {
                IsError = !ok,
                Content = new List<ContentBlock>
                {
                    new TextContentBlock { Text = targetNotice + "\n" + body },
                },
            };
        }

        /// <summary>ホストの応答が契約から外れているときの誤り。</summary>
        private static FormatException Broken(string message)
        {
            return new FormatException(message);
        }

        private static bool Flag(JsonObject envelope)
        {
            JsonNode node = envelope[OkName];
            bool ok;
            if (node == null || !(node is JsonValue) || !((JsonValue)node).TryGetValue(out ok))
            {
                throw Broken("ツールの応答が成否を持たない。");
            }

            return ok;
        }

        /// <summary>成功の本文。値はJSONの表記にする——ツールの値は構造を持つ。</summary>
        private static string Value(JsonObject envelope)
        {
            if (!envelope.ContainsKey(ValueName))
            {
                throw Broken("ツールの応答が値を持たない。");
            }

            JsonNode value = envelope[ValueName];

            return value == null ? "null" : value.ToJsonString();
        }

        private static string Failure(JsonObject envelope)
        {
            JsonObject error = envelope[ErrorName] as JsonObject;
            if (error == null)
            {
                throw Broken("ツールの応答が誤りの内容を持たない。");
            }

            return Text(error, CodeName) + ": " + Text(error, MessageName);
        }

        private static string Text(JsonObject error, string name)
        {
            JsonNode node = error[name];
            string text;
            if (node == null || !(node is JsonValue) || !((JsonValue)node).TryGetValue(out text)
                || string.IsNullOrEmpty(text) || text.Trim().Length == 0)
            {
                throw Broken("ツールの誤りの内容に " + name + " が無い。");
            }

            return text;
        }

        private static IEnumerable<string> Warnings(JsonObject envelope)
        {
            if (!envelope.ContainsKey(WarningsName))
            {
                yield break;
            }

            JsonArray warnings = envelope[WarningsName] as JsonArray;
            if (warnings == null)
            {
                throw Broken("ツールの応答の警告が並びでない。");
            }

            foreach (JsonNode node in warnings)
            {
                string text;
                if (node == null || !(node is JsonValue) || !((JsonValue)node).TryGetValue(out text)
                    || string.IsNullOrEmpty(text) || text.Trim().Length == 0)
                {
                    throw Broken("ツールの応答の警告が空でない文字列でない。");
                }

                yield return text;
            }
        }
    }
}
