using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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

        private const string NarrowingLabel = "絞り方: ";

        /// <summary>
        /// 画像の種別。ホストが送り出す画像はPNGに決まっている(ImageTransfer が定める)ので、
        /// 包みからは読まずここで名乗る。
        /// </summary>
        private const string ImageMimeType = "image/png";

        /// <summary>
        /// 包みをツール結果へ写す。成功なら値を、失敗なら「コード: メッセージ」を本文にし、警告が
        /// あれば同じ本文の末尾へ行として足す。本文が <paramref name="budgetChars"/> を超えるときは
        /// 本文を返さず、大きすぎる旨の誤りにし、<paramref name="narrowing"/> を持つならその手立ても
        /// 添える。包みとして読めなければ
        /// <see cref="FormatException"/>——ホストの応答が契約から外れている。呼び出し側は、これを
        /// 受けたら接続を捨てて `BRIDGE_PROTOCOL_ERROR` にする。
        ///
        /// <paramref name="returnsImage"/> が真のツールは、成功した値を文字の本文へ入れず画像の
        /// 本文として返す。文字列で返すとMCPクライアントは中身を見られず、しかも詰めた文字がそのまま本文の
        /// 長さになって予算を超える。画像の大きさを抑えるのは長辺の上限で、本文の予算ではない。
        /// </summary>
        public static CallToolResult From(
            JsonNode result,
            string targetNotice,
            int budgetChars,
            bool returnsImage,
            string narrowing = null)
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
            bool drawn = ok && returnsImage;
            string image = drawn ? Image(envelope) : null;
            string body = ok ? (drawn ? string.Empty : Value(envelope)) : Failure(envelope);
            foreach (string warning in Warnings(envelope))
            {
                body += (body.Length == 0 ? string.Empty : "\n") + WarningPrefix + warning;
            }

            if (body.Length > budgetChars)
            {
                ok = false;
                drawn = false;
                image = null;
                body = TooLargeCode + ": 応答が応答サイズ予算 " + budgetChars
                    + " 文字に収まらない(" + body.Length + " 文字)。"
                    + (string.IsNullOrEmpty(narrowing)
                        ? string.Empty
                        : NarrowingLabel + narrowing + "。");
            }

            List<ContentBlock> content = new List<ContentBlock>
            {
                new TextContentBlock
                {
                    Text = body.Length == 0 ? targetNotice : targetNotice + "\n" + body,
                },
            };
            if (drawn)
            {
                // Data はBase64の綴りをUTF-8のバイトで持つ。ホストから届くのはBase64の文字列
                // なので、復号して詰め直さずそのまま写す。
                content.Add(new ImageContentBlock
                {
                    Data = Encoding.UTF8.GetBytes(image),
                    MimeType = ImageMimeType,
                });
            }

            return new CallToolResult
            {
                IsError = !ok,
                Content = content,
            };
        }

        /// <summary>
        /// 画像を返すツールの値。PNGを詰めた文字列でなければ契約から外れている——画像を返す
        /// ツールかどうかはビルド時に決まっていて、実行時の値では変わらない。
        /// </summary>
        private static string Image(JsonObject envelope)
        {
            if (!envelope.ContainsKey(ValueName))
            {
                throw Broken("ツールの応答が値を持たない。");
            }

            JsonValue value = envelope[ValueName] as JsonValue;
            string packed;
            if (value == null || !value.TryGetValue(out packed) || packed.Length == 0)
            {
                throw Broken("画像を返すツールの値が、空でない文字列でない。");
            }

            return packed;
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

            return value == null ? "null" : Written(value);
        }

        internal static string Written(JsonNode value)
        {
            StringBuilder written = new StringBuilder();
            Write(written, value);

            return written.ToString();
        }

        private static void Write(StringBuilder written, JsonNode node)
        {
            if (node == null)
            {
                written.Append("null");

                return;
            }

            JsonObject members = node as JsonObject;
            if (members != null)
            {
                written.Append('{');
                bool first = true;
                foreach (KeyValuePair<string, JsonNode> member in members)
                {
                    if (!first)
                    {
                        written.Append(',');
                    }

                    first = false;
                    Quote(written, member.Key);
                    written.Append(':');
                    Write(written, member.Value);
                }

                written.Append('}');

                return;
            }

            JsonArray items = node as JsonArray;
            if (items != null)
            {
                written.Append('[');
                for (int at = 0; at < items.Count; at++)
                {
                    if (at > 0)
                    {
                        written.Append(',');
                    }

                    Write(written, items[at]);
                }

                written.Append(']');

                return;
            }

            string text;
            if (((JsonValue)node).TryGetValue(out text))
            {
                Quote(written, text);

                return;
            }

            written.Append(node.ToJsonString());
        }

        private static void Quote(StringBuilder written, string text)
        {
            written.Append('"');
            for (int at = 0; at < text.Length; at++)
            {
                char letter = text[at];
                switch (letter)
                {
                    case '"':
                        written.Append("\\\"");
                        break;
                    case '\\':
                        written.Append("\\\\");
                        break;
                    case '\n':
                        written.Append("\\n");
                        break;
                    case '\r':
                        written.Append("\\r");
                        break;
                    case '\t':
                        written.Append("\\t");
                        break;
                    default:
                        if (letter < ' ' || Unpaired(text, at))
                        {
                            written.Append("\\u")
                                .Append(((int)letter).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            written.Append(letter);
                        }

                        break;
                }
            }

            written.Append('"');
        }

        private static bool Unpaired(string text, int at)
        {
            char letter = text[at];
            if (char.IsHighSurrogate(letter))
            {
                return at + 1 >= text.Length || !char.IsLowSurrogate(text[at + 1]);
            }

            return char.IsLowSurrogate(letter) && (at == 0 || !char.IsHighSurrogate(text[at - 1]));
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
