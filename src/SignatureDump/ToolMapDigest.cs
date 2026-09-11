using System;
using System.Security.Cryptography;
using System.Text;

namespace PmxEditorMcp.SignatureDump
{
    /// <summary>
    /// 能力対応表の中身から指紋を作る。ホストの中継もブリッジのツール定義もこの表から生成するので、
    /// 両方が同じ指紋を名乗れば同じ表から作られたことになる。接続の確立で照らし合わせる。
    /// 行キーだけでなく本文まるごとを材料にする——行キーが同じでも、種別や更新の指定が変われば
    /// 組み立てるツールが変わる。
    /// </summary>
    public static class ToolMapDigest
    {
        /// <summary>
        /// 能力対応表の本文の指紋。改行の綴りはそろえてから数える——同じ中身が置き場によって
        /// 別の指紋になると、食い違っていない組み合わせを断る。
        /// </summary>
        public static string Of(string toolMapText)
        {
            if (toolMapText == null)
            {
                throw new ArgumentNullException(nameof(toolMapText));
            }

            string canonical = toolMapText.Replace("\r\n", "\n").Replace("\r", "\n");
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(new UTF8Encoding(false).GetBytes(canonical));
                StringBuilder text = new StringBuilder(hash.Length * 2);
                foreach (byte value in hash)
                {
                    text.Append(value.ToString("x2"));
                }

                return text.ToString();
            }
        }
    }
}
