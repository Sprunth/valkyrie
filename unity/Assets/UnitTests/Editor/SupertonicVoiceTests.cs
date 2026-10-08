using Assets.Scripts.Tts;
using NUnit.Framework;

namespace Valkyrie.UnitTests
{
    [TestFixture]
    public class SupertonicVoiceTests
    {
        private const string VoiceJson =
            "{\"style_ttl\": {\"data\": [[[1, 2], [3, 4]]], \"dims\": [1, 2, 2], \"type\": \"float32\"}," +
            " \"style_dp\": {\"data\": [[[5], [6]]], \"dims\": [1, 2, 1], \"type\": \"float32\"}," +
            " \"metadata\": {\"source_file\": \"voice.wav\"}}";

        [Test]
        public void Parse_FlattensTtlData()
        {
            var voice = SupertonicVoice.Parse(VoiceJson);

            CollectionAssert.AreEqual(new[] { 1f, 2f, 3f, 4f }, voice.Ttl);
            CollectionAssert.AreEqual(new[] { 1, 2, 2 }, voice.TtlShape);
        }

        [Test]
        public void Parse_FlattensDpData()
        {
            var voice = SupertonicVoice.Parse(VoiceJson);

            CollectionAssert.AreEqual(new[] { 5f, 6f }, voice.Dp);
            CollectionAssert.AreEqual(new[] { 1, 2, 1 }, voice.DpShape);
        }

        [Test]
        public void Average_AveragesEachValue()
        {
            var other = SupertonicVoice.Parse(VoiceJson.Replace("[[[1, 2], [3, 4]]]", "[[[3, 4], [5, 6]]]"));

            var average = SupertonicVoice.Average(new[] { SupertonicVoice.Parse(VoiceJson), other });

            CollectionAssert.AreEqual(new[] { 2f, 3f, 4f, 5f }, average.Ttl);
            CollectionAssert.AreEqual(new[] { 1, 2, 2 }, average.TtlShape);
        }

        [Test]
        public void Exaggerate_ScalesDifferenceFromAverage()
        {
            var voice = SupertonicVoice.Parse(VoiceJson);
            var average = SupertonicVoice.Parse(VoiceJson.Replace("[[[1, 2], [3, 4]]]", "[[[0, 0], [0, 0]]]"));

            var result = voice.Exaggerate(average, 1.5f);

            CollectionAssert.AreEqual(new[] { 1.5f, 3f, 4.5f, 6f }, result.Ttl);
            CollectionAssert.AreEqual(new[] { 5f, 6f }, result.Dp);
        }
    }
}
