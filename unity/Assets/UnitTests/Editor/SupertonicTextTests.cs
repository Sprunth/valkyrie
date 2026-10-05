using System;
using Assets.Scripts.Tts;
using NUnit.Framework;

namespace Valkyrie.UnitTests
{
    [TestFixture]
    public class SupertonicTextTests
    {
        [Test]
        public void Preprocess_PlainText_AddsPeriodAndLanguageTags()
        {
            Assert.AreEqual("<en>Hello world.</en>", SupertonicText.Preprocess("Hello world", "en"));
        }

        [Test]
        public void Preprocess_EndingPunctuation_IsKept()
        {
            Assert.AreEqual("<de>Hallo!</de>", SupertonicText.Preprocess("Hallo!", "de"));
        }

        [Test]
        public void Preprocess_Whitespace_IsCollapsed()
        {
            Assert.AreEqual("<en>Hello world!</en>", SupertonicText.Preprocess("  Hello \n\n  world! ", "en"));
        }

        [Test]
        public void Preprocess_SpaceBeforePunctuation_IsRemoved()
        {
            Assert.AreEqual("<en>Wait, what?</en>", SupertonicText.Preprocess("Wait , what ?", "en"));
        }

        [Test]
        public void Preprocess_CurlyQuotes_BecomeStraight()
        {
            Assert.AreEqual("<en>\"Run,\" she said.</en>", SupertonicText.Preprocess("\u201CRun,\u201D she said", "en"));
        }

        [Test]
        public void Preprocess_AtSign_IsSpoken()
        {
            Assert.AreEqual("<en>Meet me at home.</en>", SupertonicText.Preprocess("Meet me @ home", "en"));
        }

        [Test]
        public void Preprocess_Emoji_IsRemoved()
        {
            Assert.AreEqual("<en>Boo.</en>", SupertonicText.Preprocess("Boo \U0001F47B", "en"));
        }

        [Test]
        public void Preprocess_AccentedLetter_IsDecomposed()
        {
            Assert.AreEqual("<fr>cafe\u0301.</fr>", SupertonicText.Preprocess("caf\u00E9", "fr"));
        }

        [Test]
        public void Preprocess_UnknownLanguage_Throws()
        {
            Assert.Throws<ArgumentException>(() => SupertonicText.Preprocess("Hello", "xx"));
        }

        [Test]
        public void ToTextIds_MapsCodeUnitsThroughIndexer()
        {
            long[] indexer = { -1, 5, 7 };

            CollectionAssert.AreEqual(new long[] { 5, 7, -1 }, SupertonicText.ToTextIds("\u0001\u0002\u0000", indexer));
        }

        [Test]
        public void ToTextIds_CodeUnitOutsideIndexer_IsZero()
        {
            CollectionAssert.AreEqual(new long[] { 0 }, SupertonicText.ToTextIds("A", new long[] { 1, 2 }));
        }

        [Test]
        public void MaxChunkLength_DependsOnLanguage()
        {
            Assert.AreEqual(120, SupertonicText.MaxChunkLength("ja"));
            Assert.AreEqual(120, SupertonicText.MaxChunkLength("ko"));
            Assert.AreEqual(300, SupertonicText.MaxChunkLength("en"));
        }

        [Test]
        public void ChunkText_ShortText_IsSingleChunk()
        {
            CollectionAssert.AreEqual(new[] { "One. Two." }, SupertonicText.ChunkText(" One. Two. ", 300));
        }

        [Test]
        public void ChunkText_LongText_SplitsOnSentences()
        {
            CollectionAssert.AreEqual(
                new[] { "First sentence.", "Second sentence." },
                SupertonicText.ChunkText("First sentence. Second sentence.", 20));
        }

        [Test]
        public void ChunkText_Paragraphs_AreSeparateChunks()
        {
            CollectionAssert.AreEqual(new[] { "Para one.", "Para two." }, SupertonicText.ChunkText("Para one.\n\nPara two.", 300));
        }

        [Test]
        public void ChunkText_Abbreviation_DoesNotSplit()
        {
            CollectionAssert.AreEqual(
                new[] { "Dr. Smith arrived.", "He sat." },
                SupertonicText.ChunkText("Dr. Smith arrived. He sat.", 15));
        }

        [Test]
        public void Sentences_SplitsOnSentenceEnds()
        {
            CollectionAssert.AreEqual(new[] { "Dr. Smith waits.", "Run!", "Why?" }, SupertonicText.Sentences("Dr. Smith waits. Run! Why?"));
        }
    }
}
