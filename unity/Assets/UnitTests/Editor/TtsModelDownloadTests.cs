using Assets.Scripts.Tts;
using NUnit.Framework;

namespace Valkyrie.UnitTests
{
    [TestFixture]
    public class TtsModelDownloadTests
    {
        [Test]
        public void Sources_NotConfigured_UsesDefaults()
        {
            Assert.AreEqual(2, TtsModelDownload.Sources("").Length);
            Assert.AreEqual(2, TtsModelDownload.Sources("   ").Length);
        }

        [Test]
        public void Sources_Configured_SplitsOnSpaces()
        {
            CollectionAssert.AreEqual(new[] { "https://a/", "https://b/" }, TtsModelDownload.Sources(" https://a/  https://b/ "));
        }

        [Test]
        public void FileUrl_JoinsWithSingleSlash()
        {
            Assert.AreEqual("https://host/model/onnx/tts.json", TtsModelDownload.FileUrl("https://host/model/", "onnx/tts.json"));
            Assert.AreEqual("https://host/model/onnx/tts.json", TtsModelDownload.FileUrl("https://host/model", "onnx/tts.json"));
        }

        [Test]
        public void IsRetryable_RateLimitAndServerErrors_AreRetried()
        {
            Assert.IsTrue(TtsModelDownload.IsRetryable(0));
            Assert.IsTrue(TtsModelDownload.IsRetryable(429));
            Assert.IsTrue(TtsModelDownload.IsRetryable(503));
        }

        [Test]
        public void IsRetryable_ClientErrors_AreNotRetried()
        {
            Assert.IsFalse(TtsModelDownload.IsRetryable(403));
            Assert.IsFalse(TtsModelDownload.IsRetryable(404));
        }

        [Test]
        public void RetryDelaySeconds_RetryAfterHeader_IsHonouredAndCapped()
        {
            Assert.AreEqual(30, TtsModelDownload.RetryDelaySeconds(0, "30"));
            Assert.AreEqual(300, TtsModelDownload.RetryDelaySeconds(0, "3600"));
        }

        [Test]
        public void RetryDelaySeconds_NoHeader_BacksOffExponentially()
        {
            Assert.AreEqual(5, TtsModelDownload.RetryDelaySeconds(0, null));
            Assert.AreEqual(10, TtsModelDownload.RetryDelaySeconds(1, ""));
            Assert.AreEqual(120, TtsModelDownload.RetryDelaySeconds(10, "Wed, 21 Oct 2026 07:28:00 GMT"));
        }

        [Test]
        public void Files_IncludeModelAndAllTenVoices()
        {
            CollectionAssert.Contains(TtsModelDownload.Files, "onnx/vector_estimator.onnx");
            CollectionAssert.Contains(TtsModelDownload.Files, "voice_styles/F5.json");
            Assert.AreEqual(17, TtsModelDownload.Files.Length);
        }
    }
}
