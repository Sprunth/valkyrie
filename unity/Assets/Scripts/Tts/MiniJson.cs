using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Assets.Scripts.Tts
{
    // Minimal JSON reader: objects become Dictionary<string, object>, arrays List<object>, numbers double
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            int position = 0;
            object value = ParseValue(json, ref position);
            SkipWhitespace(json, ref position);
            if (position != json.Length)
            {
                throw new FormatException("Unexpected trailing content at position " + position);
            }
            return value;
        }

        private static object ParseValue(string json, ref int position)
        {
            SkipWhitespace(json, ref position);
            if (position >= json.Length)
            {
                throw new FormatException("Unexpected end of JSON");
            }

            char current = json[position];
            if (current == '{') return ParseObject(json, ref position);
            if (current == '[') return ParseArray(json, ref position);
            if (current == '"') return ParseString(json, ref position);
            if (MatchLiteral(json, ref position, "true")) return true;
            if (MatchLiteral(json, ref position, "false")) return false;
            if (MatchLiteral(json, ref position, "null")) return null;
            return ParseNumber(json, ref position);
        }

        private static Dictionary<string, object> ParseObject(string json, ref int position)
        {
            var result = new Dictionary<string, object>();
            position++;
            SkipWhitespace(json, ref position);
            if (json[position] == '}')
            {
                position++;
                return result;
            }

            while (true)
            {
                SkipWhitespace(json, ref position);
                string key = ParseString(json, ref position);
                SkipWhitespace(json, ref position);
                Expect(json, ref position, ':');
                result[key] = ParseValue(json, ref position);
                SkipWhitespace(json, ref position);
                if (json[position] == '}')
                {
                    position++;
                    return result;
                }
                Expect(json, ref position, ',');
            }
        }

        private static List<object> ParseArray(string json, ref int position)
        {
            var result = new List<object>();
            position++;
            SkipWhitespace(json, ref position);
            if (json[position] == ']')
            {
                position++;
                return result;
            }

            while (true)
            {
                result.Add(ParseValue(json, ref position));
                SkipWhitespace(json, ref position);
                if (json[position] == ']')
                {
                    position++;
                    return result;
                }
                Expect(json, ref position, ',');
            }
        }

        private static string ParseString(string json, ref int position)
        {
            Expect(json, ref position, '"');
            var builder = new StringBuilder();
            while (json[position] != '"')
            {
                char current = json[position++];
                if (current != '\\')
                {
                    builder.Append(current);
                    continue;
                }

                char escaped = json[position++];
                switch (escaped)
                {
                    case 'b': builder.Append('\b'); break;
                    case 'f': builder.Append('\f'); break;
                    case 'n': builder.Append('\n'); break;
                    case 'r': builder.Append('\r'); break;
                    case 't': builder.Append('\t'); break;
                    case 'u':
                        builder.Append((char)int.Parse(json.Substring(position, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        position += 4;
                        break;
                    default: builder.Append(escaped); break;
                }
            }
            position++;
            return builder.ToString();
        }

        private static double ParseNumber(string json, ref int position)
        {
            int start = position;
            while (position < json.Length && "+-0123456789.eE".IndexOf(json[position]) >= 0)
            {
                position++;
            }
            if (start == position)
            {
                throw new FormatException("Unexpected character '" + json[position] + "' at position " + position);
            }
            return double.Parse(json.Substring(start, position - start), NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static bool MatchLiteral(string json, ref int position, string literal)
        {
            if (string.CompareOrdinal(json, position, literal, 0, literal.Length) != 0)
            {
                return false;
            }
            position += literal.Length;
            return true;
        }

        private static void Expect(string json, ref int position, char expected)
        {
            if (position >= json.Length || json[position] != expected)
            {
                throw new FormatException("Expected '" + expected + "' at position " + position);
            }
            position++;
        }

        private static void SkipWhitespace(string json, ref int position)
        {
            while (position < json.Length && char.IsWhiteSpace(json[position]))
            {
                position++;
            }
        }
    }
}
