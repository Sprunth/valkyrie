using System.Collections.Generic;
using Assets.Scripts.Tts;
using NUnit.Framework;

namespace Valkyrie.UnitTests
{
    [TestFixture]
    public class SpeechTextTests
    {
        [Test]
        public void ToSpeakable_RichTextTags_AreRemoved()
        {
            string result = SpeechText.ToSpeakable("<b>Run</b>, <i>now</i>! <color=red>Hide</color>", new Dictionary<string, string>());

            Assert.AreEqual("Run, now! Hide", result);
        }

        [Test]
        public void ToSpeakable_LessThanInText_IsKept()
        {
            Assert.AreEqual("If 3 < 5 then", SpeechText.ToSpeakable("If 3 < 5 then", new Dictionary<string, string>()));
        }

        [Test]
        public void ToSpeakable_Glyphs_AreReplacedByWords()
        {
            var glyphToWord = new Dictionary<string, string> { { "X", "will" }, { "Y", "" } };

            Assert.AreEqual("Test will now.", SpeechText.ToSpeakable("Test X nowY.", glyphToWord));
        }

        [Test]
        public void GlyphWords_SymbolTags_BecomeWords()
        {
            var symbols = new Dictionary<string, string> { { "{will}", "\uE001" }, { "{clue}", "\uE002" } };

            var result = SpeechText.GlyphWords(symbols, new Dictionary<string, string>());

            Assert.AreEqual("will", result["\uE001"]);
            Assert.AreEqual("clue", result["\uE002"]);
        }

        [Test]
        public void GlyphWords_SilentTags_BecomeEmpty()
        {
            var silent = new Dictionary<string, string> { { "{MAD20}", "\uE010" } };

            var result = SpeechText.GlyphWords(new Dictionary<string, string>(), silent);

            Assert.AreEqual("", result["\uE010"]);
        }

        [Test]
        public void LanguageCode_KnownLanguage_ReturnsCode()
        {
            Assert.AreEqual("en", SpeechText.LanguageCode("English"));
            Assert.AreEqual("uk", SpeechText.LanguageCode("Ukrainian"));
        }

        [Test]
        public void LanguageCode_UnsupportedLanguage_ReturnsNull()
        {
            Assert.IsNull(SpeechText.LanguageCode("Chinese"));
        }
    }
}
