using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;
using ValkyrieTools;

namespace Assets.Scripts.Tts
{
    // Speaks quest text with Supertonic when its model is installed under <AppData>/tts/supertonic-3
    public class TtsNarrator : MonoBehaviour
    {
        private const float StorytellerExaggeration = 1.3f;
        private const float SentencePauseSeconds = 0.3f;
        private static readonly Dictionary<string, string[]> NarratorVoices = new Dictionary<string, string[]>
        {
            { "male", new[] { "M5", "M1" } },
            { "female", new[] { "F5", "F2" } }
        };

        private readonly object engineLock = new object();
        private readonly object pendingLock = new object();
        private readonly Queue<float[]> pendingSentences = new Queue<float[]>();
        private AudioSource audioSource;
        private string modelDirectory;
        private SupertonicTts engine;
        private Dictionary<string, SupertonicVoice> voices;
        private SupertonicVoice averageVoice;
        private volatile bool engineFailed;
        private volatile int latestRequest;

        public bool Available
        {
            get { return !engineFailed && Directory.Exists(Path.Combine(modelDirectory, "onnx")); }
        }

        // "male" or "female", stored in the user config
        public string Narrator
        {
            get { return Game.Get().config.data.Get("UserConfig", "narrator") == "female" ? "female" : "male"; }
            set
            {
                Game.Get().config.data.Add("UserConfig", "narrator", value);
                Game.Get().config.Save();
            }
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

            var paragraphs = SpeechText.Paragraphs(displayedText, GlyphWords(game.gameType.TypeName()));
            string[] voiceNames = NarratorVoices[Narrator];
            int request = latestRequest;
            ThreadPool.QueueUserWorkItem(delegate { Synthesize(paragraphs, voiceNames, lang, request); });
        }

        public void Stop()
        {
            lock (pendingLock)
            {
                latestRequest++;
                pendingSentences.Clear();
            }
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

        private void Synthesize(List<KeyValuePair<string, bool>> paragraphs, string[] voiceNames, string lang, int request)
        {
            try
            {
                lock (engineLock)
                {
                    if (engine == null) LoadEngine();
                    SupertonicVoice storyteller = voices[voiceNames[0]].Exaggerate(averageVoice, StorytellerExaggeration);
                    SupertonicVoice instructor = voices[voiceNames[1]];
                    foreach (var paragraph in paragraphs)
                    {
                        foreach (string sentence in SupertonicText.Sentences(paragraph.Key))
                        {
                            if (request != latestRequest) return;
                            float[] samples = engine.Synthesize(sentence, lang, paragraph.Value ? instructor : storyteller);
                            Array.Resize(ref samples, samples.Length + (int)(SentencePauseSeconds * engine.SampleRate));
                            lock (pendingLock)
                            {
                                if (request == latestRequest) pendingSentences.Enqueue(samples);
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                engineFailed = true;
                ValkyrieDebug.Log("Warning: Text to speech failed: " + e);
            }
        }

        private void LoadEngine()
        {
            engine = new SupertonicTts(Path.Combine(modelDirectory, "onnx"));
            voices = Directory.GetFiles(Path.Combine(modelDirectory, "voice_styles"), "*.json")
                .ToDictionary(Path.GetFileNameWithoutExtension, SupertonicVoice.Load);
            averageVoice = SupertonicVoice.Average(voices.Values.ToList());
        }

        void Update()
        {
            if (audioSource.isPlaying) return;
            float[] samples;
            lock (pendingLock)
            {
                if (pendingSentences.Count == 0) return;
                samples = pendingSentences.Dequeue();
            }

            if (audioSource.clip != null) Destroy(audioSource.clip);
            audioSource.clip = AudioClip.Create("tts", samples.Length, 1, engine.SampleRate, false);
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
