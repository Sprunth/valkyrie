using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Assets.Scripts.Tts
{
    // Text normalisation and chunking, ported from the Supertonic reference implementation
    public static class SupertonicText
    {
        public static readonly string[] Languages =
        {
            "en", "ko", "ja", "ar", "bg", "cs", "da", "de", "el", "es", "et", "fi", "fr", "hi", "hr", "hu",
            "id", "it", "lt", "lv", "nl", "pl", "pt", "ro", "ru", "sk", "sl", "sv", "tr", "uk", "vi", "na"
        };

        private static readonly KeyValuePair<string, string>[] SymbolReplacements =
        {
            new KeyValuePair<string, string>("\u2013", "-"),
            new KeyValuePair<string, string>("\u2011", "-"),
            new KeyValuePair<string, string>("\u2014", "-"),
            new KeyValuePair<string, string>("_", " "),
            new KeyValuePair<string, string>("\u201C", "\""),
            new KeyValuePair<string, string>("\u201D", "\""),
            new KeyValuePair<string, string>("\u2018", "'"),
            new KeyValuePair<string, string>("\u2019", "'"),
            new KeyValuePair<string, string>("\u00B4", "'"),
            new KeyValuePair<string, string>("`", "'"),
            new KeyValuePair<string, string>("[", " "),
            new KeyValuePair<string, string>("]", " "),
            new KeyValuePair<string, string>("|", " "),
            new KeyValuePair<string, string>("/", " "),
            new KeyValuePair<string, string>("#", " "),
            new KeyValuePair<string, string>("\u2192", " "),
            new KeyValuePair<string, string>("\u2190", " "),
            new KeyValuePair<string, string>("@", " at "),
            new KeyValuePair<string, string>("e.g.,", "for example, "),
            new KeyValuePair<string, string>("i.e.,", "that is, ")
        };

        private static readonly Regex SentenceSplit = new Regex(
            @"(?<!Mr\.|Mrs\.|Ms\.|Dr\.|Prof\.|Sr\.|Jr\.|Ph\.D\.|etc\.|e\.g\.|i\.e\.|vs\.|Inc\.|Ltd\.|Co\.|Corp\.|St\.|Ave\.|Blvd\.)(?<!\b[A-Z]\.)(?<=[.!?])\s+");

        public static string Preprocess(string text, string lang)
        {
            if (!Languages.Contains(lang))
            {
                throw new ArgumentException("Invalid language: " + lang);
            }

            text = RemoveEmojis(text.Normalize(NormalizationForm.FormKD));
            foreach (var replacement in SymbolReplacements)
            {
                text = text.Replace(replacement.Key, replacement.Value);
            }
            text = Regex.Replace(text, @"[\u2665\u2606\u2661\u00A9\\]", "");
            text = Regex.Replace(text, @" ([,.!?;:'])", "$1");
            text = Regex.Replace(text, "\"{2,}", "\"");
            text = Regex.Replace(text, "'{2,}", "'");
            text = Regex.Replace(text, @"\s+", " ").Trim();

            if (!Regex.IsMatch(text, @"[.!?;:,'""\u201C\u201D\u2018\u2019)\]}\u2026\u3002\u300D\u300F\u3011\u3009\u300B\u203A\u00BB]$"))
            {
                text += ".";
            }
            return "<" + lang + ">" + text + "</" + lang + ">";
        }

        public static long[] ToTextIds(string preprocessedText, long[] unicodeIndexer)
        {
            var ids = new long[preprocessedText.Length];
            for (int i = 0; i < preprocessedText.Length; i++)
            {
                int codeUnit = preprocessedText[i];
                if (codeUnit < unicodeIndexer.Length)
                {
                    ids[i] = unicodeIndexer[codeUnit];
                }
            }
            return ids;
        }

        public static int MaxChunkLength(string lang)
        {
            return lang == "ko" || lang == "ja" ? 120 : 300;
        }

        public static IEnumerable<string> Sentences(string paragraph)
        {
            return SentenceSplit.Split(paragraph).Where(s => s.Length > 0);
        }

        public static List<string> ChunkText(string text, int maxLength)
        {
            var chunks = new List<string>();
            var paragraphs = Regex.Split(text.Trim(), @"\n\s*\n+")
                .Select(p => p.Trim())
                .Where(p => p.Length > 0);

            foreach (string paragraph in paragraphs)
            {
                string currentChunk = "";
                foreach (string sentence in Sentences(paragraph))
                {
                    if (currentChunk.Length + sentence.Length + 1 <= maxLength)
                    {
                        currentChunk = currentChunk.Length > 0 ? currentChunk + " " + sentence : sentence;
                    }
                    else
                    {
                        if (currentChunk.Length > 0)
                        {
                            chunks.Add(currentChunk.Trim());
                        }
                        currentChunk = sentence;
                    }
                }

                if (currentChunk.Length > 0)
                {
                    chunks.Add(currentChunk.Trim());
                }
            }

            if (chunks.Count == 0)
            {
                chunks.Add(text.Trim());
            }
            return chunks;
        }

        private static string RemoveEmojis(string text)
        {
            var result = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                bool isPair = char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]);
                int codePoint = isPair ? char.ConvertToUtf32(text[i], text[i + 1]) : text[i];
                if (!IsEmoji(codePoint))
                {
                    result.Append(text[i]);
                    if (isPair) result.Append(text[i + 1]);
                }
                if (isPair) i++;
            }
            return result.ToString();
        }

        private static bool IsEmoji(int codePoint)
        {
            return (codePoint >= 0x1F300 && codePoint <= 0x1FAFF)
                || (codePoint >= 0x2600 && codePoint <= 0x27BF)
                || (codePoint >= 0x1F1E6 && codePoint <= 0x1F1FF);
        }
    }
}
