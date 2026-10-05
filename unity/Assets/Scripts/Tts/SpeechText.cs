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

        private static readonly Regex InstructionStart = new Regex(
            @"^(Place|Remove|Replace|Discard|Take|Gain|Suffer|Draw|Shuffle|Spawn|Move|Flip|Search|Set aside|Each investigator)\b");

        // Splits text into paragraphs, flagging those that highlight game terms or start with a game action as instructions
        public static List<KeyValuePair<string, bool>> Paragraphs(string displayedText, Dictionary<string, string> glyphToWord)
        {
            var paragraphs = new List<KeyValuePair<string, bool>>();
            foreach (string paragraph in Regex.Split(displayedText, @"\n\s*\n"))
            {
                string speakable = ToSpeakable(paragraph, glyphToWord).Trim();
                if (speakable.Length == 0) continue;
                bool isInstruction = paragraph.Contains("<color") || InstructionStart.IsMatch(speakable);
                paragraphs.Add(new KeyValuePair<string, bool>(speakable, isInstruction));
            }
            return paragraphs;
        }

        // Official voice-over clips are named after the scenario prologue or epilogue; other event audio is a sound effect
        public static bool IsRecordedNarration(string audio)
        {
            return Regex.IsMatch(audio, "Prologue|Epilogue", RegexOptions.IgnoreCase);
        }

        public static string ToSpeakable(string displayedText, Dictionary<string, string> glyphToWord)
        {
            string text = Regex.Replace(displayedText, "</?[a-zA-Z][^>]*>", "");
            foreach (var symbol in glyphToWord)
            {
                text = Regex.Replace(text, @"(\b" + Regex.Escape(symbol.Value) + @"\w*)\s*" + Regex.Escape(symbol.Key), "$1", RegexOptions.IgnoreCase);
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
