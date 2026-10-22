using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TodoTool
{
    /// <summary>
    /// 极简 JSON 读写（零外部依赖）。
    /// 读取：解析为 Dictionary&lt;string,object&gt; / List&lt;object&gt; / string / long / double / bool / null；
    /// 写入：仅提供字符串转义（本应用手工拼装 JSON，字段全部为标量）。
    /// </summary>
    internal static class MiniJson
    {
        public static object Parse(string json)
        {
            if (json == null) throw new ArgumentNullException("json");
            int pos = 0;
            object v = ParseValue(json, ref pos);
            SkipWs(json, ref pos);
            if (pos < json.Length) throw new FormatException("JSON 末尾存在多余字符");
            return v;
        }

        private static void SkipWs(string s, ref int pos)
        {
            while (pos < s.Length)
            {
                char c = s[pos];
                if (c == ' ' || c == '\t' || c == '\n' || c == '\r') pos++;
                else break;
            }
        }

        private static object ParseValue(string s, ref int pos)
        {
            SkipWs(s, ref pos);
            if (pos >= s.Length) throw new FormatException("JSON 意外结束");
            char c = s[pos];
            if (c == '{') return ParseObject(s, ref pos);
            if (c == '[') return ParseArray(s, ref pos);
            if (c == '"') return ParseString(s, ref pos);
            if (c == 't') { Expect(s, ref pos, "true"); return true; }
            if (c == 'f') { Expect(s, ref pos, "false"); return false; }
            if (c == 'n') { Expect(s, ref pos, "null"); return null; }
            if (c == '-' || (c >= '0' && c <= '9')) return ParseNumber(s, ref pos);
            throw new FormatException("无法识别的 JSON 字符: " + c);
        }

        private static void Expect(string s, ref int pos, string word)
        {
            if (string.CompareOrdinal(s, pos, word, 0, word.Length) != 0)
                throw new FormatException("JSON 字面量解析失败");
            pos += word.Length;
        }

        private static Dictionary<string, object> ParseObject(string s, ref int pos)
        {
            Dictionary<string, object> obj = new Dictionary<string, object>(StringComparer.Ordinal);
            pos++; // {
            SkipWs(s, ref pos);
            if (pos < s.Length && s[pos] == '}') { pos++; return obj; }
            while (true)
            {
                SkipWs(s, ref pos);
                string key = ParseString(s, ref pos);
                SkipWs(s, ref pos);
                if (pos >= s.Length || s[pos] != ':') throw new FormatException("JSON 对象缺少 ':'");
                pos++;
                object val = ParseValue(s, ref pos);
                obj[key] = val;
                SkipWs(s, ref pos);
                if (pos >= s.Length) throw new FormatException("JSON 对象意外结束");
                if (s[pos] == ',') { pos++; continue; }
                if (s[pos] == '}') { pos++; return obj; }
                throw new FormatException("JSON 对象缺少 ',' 或 '}'");
            }
        }

        private static List<object> ParseArray(string s, ref int pos)
        {
            List<object> arr = new List<object>();
            pos++; // [
            SkipWs(s, ref pos);
            if (pos < s.Length && s[pos] == ']') { pos++; return arr; }
            while (true)
            {
                arr.Add(ParseValue(s, ref pos));
                SkipWs(s, ref pos);
                if (pos >= s.Length) throw new FormatException("JSON 数组意外结束");
                if (s[pos] == ',') { pos++; continue; }
                if (s[pos] == ']') { pos++; return arr; }
                throw new FormatException("JSON 数组缺少 ',' 或 ']'");
            }
        }

        private static string ParseString(string s, ref int pos)
        {
            if (pos >= s.Length || s[pos] != '"') throw new FormatException("JSON 字符串缺少引号");
            pos++;
            StringBuilder sb = new StringBuilder();
            while (pos < s.Length)
            {
                char c = s[pos];
                if (c == '"') { pos++; return sb.ToString(); }
                if (c == '\\')
                {
                    pos++;
                    if (pos >= s.Length) break;
                    char e = s[pos];
                    switch (e)
                    {
                        case '"': sb.Append('"'); pos++; break;
                        case '\\': sb.Append('\\'); pos++; break;
                        case '/': sb.Append('/'); pos++; break;
                        case 'b': sb.Append('\b'); pos++; break;
                        case 'f': sb.Append('\f'); pos++; break;
                        case 'n': sb.Append('\n'); pos++; break;
                        case 'r': sb.Append('\r'); pos++; break;
                        case 't': sb.Append('\t'); pos++; break;
                        case 'u':
                            if (pos + 4 >= s.Length) throw new FormatException("\\u 转义不完整");
                            sb.Append((char)int.Parse(s.Substring(pos + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            pos += 5;
                            break;
                        default: throw new FormatException("非法转义: \\" + e);
                    }
                }
                else
                {
                    sb.Append(c);
                    pos++;
                }
            }
            throw new FormatException("JSON 字符串未闭合");
        }

        private static object ParseNumber(string s, ref int pos)
        {
            int start = pos;
            if (pos < s.Length && s[pos] == '-') pos++;
            while (pos < s.Length && ((s[pos] >= '0' && s[pos] <= '9') || s[pos] == '.' ||
                   s[pos] == 'e' || s[pos] == 'E' || s[pos] == '+' || s[pos] == '-')) pos++;
            string num = s.Substring(start, pos - start);
            if (num.IndexOf('.') < 0 && num.IndexOf('e') < 0 && num.IndexOf('E') < 0)
                return long.Parse(num, CultureInfo.InvariantCulture);
            return double.Parse(num, CultureInfo.InvariantCulture);
        }

        /// <summary>JSON 字符串转义（不含外层引号）。</summary>
        public static string Escape(string s)
        {
            if (s == null) return "";
            StringBuilder sb = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            return sb.ToString();
        }
    }
}
