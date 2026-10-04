using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Assets.Scripts.Tts
{
    // Converts displayed quest text into text suitable for speech synthesis
    public static class SpeechText
    {
        private static readonly Dictionary<string, string> LanguageCodes = new Dictionary<string, string>
        {
            { "English", "en" },
            { "Spanish", "es" },
            { "French", "fr" },
            { "German", "de" },
            { "Italian", "it" },
            { "Portuguese", "pt" },
            { "Polish", "pl" },
            { "Russian", "ru" },
            { "Korean", "ko" },
            { "Czech", "cs" },
            { "Japanese", "ja" },
            { "Ukrainian", "uk" }
        };

        public static string ToSpeakable(string displayedText, Dictionary<string, string> glyphToWord)
        {
            string text = Regex.Replace(displayedText, "</?[a-zA-Z][^>]*>", "");
            foreach (var symbol in glyphToWord)
            {
                text = text.Replace(symbol.Key, symbol.Value);
            }
            return text;
        }

        public static Dictionary<string, string> GlyphWords(Dictionary<string, string> symbolTagToGlyph, Dictionary<string, string> silentTagToGlyph)
        {
            var glyphToWord = new Dictionary<string, string>();
            foreach (var symbol in symbolTagToGlyph)
            {
                glyphToWord[symbol.Value] = symbol.Key.Trim('{', '}');
            }
            foreach (var symbol in silentTagToGlyph)
            {
                glyphToWord[symbol.Value] = "";
            }
            return glyphToWord;
        }

        public static string LanguageCode(string valkyrieLanguage)
        {
            string code;
            return LanguageCodes.TryGetValue(valkyrieLanguage, out code) ? code : null;
        }
    }
}
