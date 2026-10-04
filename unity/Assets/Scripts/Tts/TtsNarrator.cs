using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using ValkyrieTools;

namespace Assets.Scripts.Tts
{
    // Speaks quest text with Supertonic when its model is installed under <AppData>/tts/supertonic-3
    public class TtsNarrator : MonoBehaviour
    {
        private const string VoiceName = "M1";

        private readonly object engineLock = new object();
        private readonly object pendingLock = new object();
        private AudioSource audioSource;
        private string modelDirectory;
        private SupertonicTts engine;
        private SupertonicVoice voice;
        private volatile bool engineFailed;
        private volatile int latestRequest;
        private float[] pendingSamples;
        private int pendingRequest;
        private int sampleRate;

        public bool Available
        {
            get { return !engineFailed && Directory.Exists(Path.Combine(modelDirectory, "onnx")); }
        }

        void Start()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            modelDirectory = Path.Combine(Path.Combine(Game.AppData(), "tts"), "supertonic-3");
        }

        public void Speak(string displayedText)
        {
            Stop();
            Game game = Game.Get();
            string lang = SpeechText.LanguageCode(game.currentLang);
            if (!Available || lang == null) return;

            string text = SpeechText.ToSpeakable(displayedText, GlyphWords(game.gameType.TypeName()));
            int request = latestRequest;
            ThreadPool.QueueUserWorkItem(delegate { Synthesize(text, lang, request); });
        }

        public void Stop()
        {
            latestRequest++;
            if (audioSource != null) audioSource.Stop();
        }

        private static Dictionary<string, string> GlyphWords(string gameType)
        {
            Dictionary<string, string> symbols;
            Dictionary<string, string> packs;
            if (!EventManager.CHARS_MAP.TryGetValue(gameType, out symbols)) symbols = new Dictionary<string, string>();
            if (!EventManager.CHAR_PACKS_MAP.TryGetValue(gameType, out packs)) packs = new Dictionary<string, string>();
            return SpeechText.GlyphWords(symbols, packs);
        }

        private void Synthesize(string text, string lang, int request)
        {
            try
            {
                lock (engineLock)
                {
                    if (request != latestRequest) return;
                    if (engine == null)
                    {
                        engine = new SupertonicTts(Path.Combine(modelDirectory, "onnx"));
                        voice = SupertonicVoice.Load(Path.Combine(Path.Combine(modelDirectory, "voice_styles"), VoiceName + ".json"));
                    }
                    float[] samples = engine.Synthesize(text, lang, voice);
                    lock (pendingLock)
                    {
                        pendingSamples = samples;
                        pendingRequest = request;
                        sampleRate = engine.SampleRate;
                    }
                }
            }
            catch (Exception e)
            {
                engineFailed = true;
                ValkyrieDebug.Log("Warning: Text to speech failed: " + e);
            }
        }

        void Update()
        {
            float[] samples;
            lock (pendingLock)
            {
                samples = pendingSamples;
                pendingSamples = null;
            }
            if (samples == null || samples.Length == 0 || pendingRequest != latestRequest) return;

            if (audioSource.clip != null) Destroy(audioSource.clip);
            audioSource.clip = AudioClip.Create("tts", samples.Length, 1, sampleRate, false);
            audioSource.clip.SetData(samples, 0);
            audioSource.volume = Game.Get().audioControl.effectVolume;
            audioSource.Play();
        }

        void OnDestroy()
        {
            lock (engineLock)
            {
                if (engine != null) engine.Dispose();
            }
        }
    }
}
