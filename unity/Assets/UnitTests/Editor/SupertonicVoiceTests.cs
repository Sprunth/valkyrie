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
    }
}
