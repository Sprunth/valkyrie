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
        public void ToSpeakable_GlyphAfterItsWord_IsDropped()
        {
            var glyphToWord = new Dictionary<string, string> { { "X", "clue" } };

            Assert.AreEqual("Gain 2 Clues.", SpeechText.ToSpeakable("Gain 2 Clues X.", glyphToWord));
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
        public void IsRecordedNarration_PrologueAndEpilogueClips_AreNarration()
        {
            Assert.IsTrue(SpeechText.IsRecordedNarration("{import}/audio/ToaP_Prologue_EN.ogg"));
            Assert.IsTrue(SpeechText.IsRecordedNarration("AudioToFGEpilogue"));
        }

        [Test]
        public void IsRecordedNarration_SoundEffects_AreNotNarration()
        {
            Assert.IsFalse(SpeechText.IsRecordedNarration("AudioSelect"));
            Assert.IsFalse(SpeechText.IsRecordedNarration("Sound_Effect_RocksFalling.ogg"));
            Assert.IsFalse(SpeechText.IsRecordedNarration(""));
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

        [Test]
        public void Paragraphs_StoryAndHighlightedInstruction_AreSeparated()
        {
            var result = SpeechText.Paragraphs("A bench stands here.\n\nPlace a <color=Cyan>Sight Token</color> as indicated.", new Dictionary<string, string>());

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(new KeyValuePair<string, bool>("A bench stands here.", false), result[0]);
            Assert.AreEqual(new KeyValuePair<string, bool>("Place a Sight Token as indicated.", true), result[1]);
        }

        [Test]
        public void Paragraphs_GameVerbWithoutHighlight_IsInstruction()
        {
            Assert.IsTrue(SpeechText.Paragraphs("Each investigator suffers 1 Horror.", new Dictionary<string, string>())[0].Value);
        }

        [Test]
        public void Paragraphs_EmptyParagraphs_AreSkipped()
        {
            Assert.AreEqual(1, SpeechText.Paragraphs("Darkness.\n\n<b></b>\n\n", new Dictionary<string, string>()).Count);
        }
    }
}
