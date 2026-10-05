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
        private const int PredictedDialogLimit = 8;
        private const int CachedSentenceLimit = 50;

        private readonly object workLock = new object();
        private readonly Queue<KeyValuePair<string, bool>> liveSentences = new Queue<KeyValuePair<string, bool>>();
        private readonly Queue<KeyValuePair<string, bool>> predictedSentences = new Queue<KeyValuePair<string, bool>>();
        private readonly Queue<float[]> pendingPlayback = new Queue<float[]>();
        private readonly Dictionary<string, float[]> cache = new Dictionary<string, float[]>();
        private readonly Queue<string> cacheOrder = new Queue<string>();
        private AudioSource audioSource;
        private Thread worker;
        private KeyValuePair<string, bool> inFlightSentence;
        private bool inFlightIsLive;
        private CancellationTokenSource inFlightCancellation;
        private bool shuttingDown;
        private string modelDirectory;
        private SupertonicTts engine;
        private Dictionary<string, SupertonicVoice> voices;
        private SupertonicVoice averageVoice;
        private string lang;
        private string voiceName;
        private int latestRequest;
        private volatile bool engineFailed;

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
                lock (workLock)
                {
                    predictedSentences.Clear();
                    cache.Clear();
                    cacheOrder.Clear();
                }
            }
        }

        void Start()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            modelDirectory = Path.Combine(Path.Combine(Game.AppData(), "tts"), "supertonic-3");
            if (!Available) return;
            worker = new Thread(Work) { IsBackground = true };
            worker.Start();
        }

        public void Speak(string displayedText, QuestData.Event qEvent)
        {
            Stop();
            Game game = Game.Get();
            string speechLang = SpeechText.LanguageCode(game.currentLang);
            if (!Available || speechLang == null) return;

            var glyphWords = GlyphWords(game.gameType.TypeName());
            List<string> predictedTexts = PredictTexts(qEvent);
            lock (workLock)
            {
                lang = speechLang;
                voiceName = Narrator == "female" ? "F5" : "M5";
                if (!SpeechText.IsRecordedNarration(qEvent.audio)) Enqueue(liveSentences, displayedText, glyphWords);
                predictedSentences.Clear();
                foreach (string text in predictedTexts)
                {
                    Enqueue(predictedSentences, text, glyphWords);
                }
                bool stillNeeded = liveSentences.Contains(inFlightSentence) || predictedSentences.Contains(inFlightSentence);
                if (inFlightCancellation != null && !stillNeeded) inFlightCancellation.Cancel();
                Monitor.Pulse(workLock);
            }
        }

        public void Stop()
        {
            lock (workLock)
            {
                latestRequest++;
                liveSentences.Clear();
                pendingPlayback.Clear();
                if (inFlightIsLive && inFlightCancellation != null) inFlightCancellation.Cancel();
            }
            if (audioSource != null) audioSource.Stop();
        }

        private static void Enqueue(Queue<KeyValuePair<string, bool>> queue, string displayedText, Dictionary<string, string> glyphWords)
        {
            foreach (var paragraph in SpeechText.Paragraphs(displayedText, glyphWords))
            {
                foreach (string sentence in SupertonicText.Sentences(paragraph.Key))
                {
                    queue.Enqueue(new KeyValuePair<string, bool>(sentence, paragraph.Value));
                }
            }
        }

        // Texts of upcoming dialogs reachable through buttons, hidden events and added tokens, read without changing game state
        private static List<string> PredictTexts(QuestData.Event start)
        {
            var events = Game.Get().CurrentQuest.eManager.events;
            var texts = new List<string>();
            var visited = new HashSet<string> { start.sectionName };
            var toVisit = new Queue<QuestData.Event>(new[] { start });
            while (toVisit.Count > 0 && texts.Count < PredictedDialogLimit)
            {
                QuestData.Event qEvent = toVisit.Dequeue();
                foreach (string name in qEvent.buttons.SelectMany(b => b.EventNames).Concat(qEvent.addComponents))
                {
                    EventManager.Event next;
                    if (!visited.Add(name) || !events.TryGetValue(name, out next)) continue;
                    if (next.GetType() != typeof(EventManager.Event) && !(next is EventManager.Token)) continue;
                    toVisit.Enqueue(next.qEvent);

                    if (!next.qEvent.display || SpeechText.IsRecordedNarration(next.qEvent.audio)) continue;
                    string text = next.qEvent.text.Translate(true);
                    if (text.Length == 0 || text.Contains("{rnd:")) continue;
                    texts.Add(EventManager.OutputSymbolReplace(EventManager.Event.ReplaceComponentText(text)).Replace("\\n", "\n"));
                }
            }
            return texts;
        }

        private static Dictionary<string, string> GlyphWords(string gameType)
        {
            Dictionary<string, string> symbols;
            Dictionary<string, string> packs;
            if (!EventManager.CHARS_MAP.TryGetValue(gameType, out symbols)) symbols = new Dictionary<string, string>();
            if (!EventManager.CHAR_PACKS_MAP.TryGetValue(gameType, out packs)) packs = new Dictionary<string, string>();
            return SpeechText.GlyphWords(symbols, packs);
        }

        // Background worker: live sentences first, then predicted ones while idle
        private void Work()
        {
            try
            {
                LoadEngine();
                while (true)
                {
                    KeyValuePair<string, bool> sentence;
                    bool live;
                    int request;
                    string sentenceLang;
                    string sentenceVoice;
                    lock (workLock)
                    {
                        while (!shuttingDown && liveSentences.Count == 0 && predictedSentences.Count == 0) Monitor.Wait(workLock);
                        if (shuttingDown) return;
                        live = liveSentences.Count > 0;
                        sentence = live ? liveSentences.Dequeue() : predictedSentences.Dequeue();
                        request = latestRequest;
                        sentenceLang = lang;
                        sentenceVoice = voiceName;
                        inFlightSentence = sentence;
                        inFlightIsLive = live;
                        inFlightCancellation = new CancellationTokenSource();
                    }

                    float[] samples = null;
                    try
                    {
                        samples = Synthesize(sentence.Key, sentence.Value, sentenceLang, sentenceVoice, inFlightCancellation.Token);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    lock (workLock)
                    {
                        inFlightCancellation.Dispose();
                        inFlightCancellation = null;
                        if (samples != null && live && request == latestRequest) pendingPlayback.Enqueue(samples);
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
            engine.Synthesize("The house is silent, yet something waits in the darkness beyond the door.", "en", averageVoice);
        }

        private float[] Synthesize(string sentence, bool isInstruction, string sentenceLang, string sentenceVoice, CancellationToken cancellation)
        {
            string key = sentenceVoice + isInstruction + sentenceLang + sentence;
            float[] samples;
            lock (workLock)
            {
                if (cache.TryGetValue(key, out samples)) return samples;
            }

            SupertonicVoice voice = isInstruction ? voices[sentenceVoice] : voices[sentenceVoice].Exaggerate(averageVoice, StorytellerExaggeration);
            samples = engine.Synthesize(sentence, sentenceLang, voice, cancellation: cancellation);
            Array.Resize(ref samples, samples.Length + (int)(SentencePauseSeconds * engine.SampleRate));
            lock (workLock)
            {
                cache[key] = samples;
                cacheOrder.Enqueue(key);
                if (cacheOrder.Count > CachedSentenceLimit) cache.Remove(cacheOrder.Dequeue());
            }
            return samples;
        }

        void Update()
        {
            if (audioSource.isPlaying) return;
            float[] samples;
            lock (workLock)
            {
                if (pendingPlayback.Count == 0) return;
                samples = pendingPlayback.Dequeue();
            }

            if (audioSource.clip != null) Destroy(audioSource.clip);
            audioSource.clip = AudioClip.Create("tts", samples.Length, 1, engine.SampleRate, false);
            audioSource.clip.SetData(samples, 0);
            audioSource.volume = Game.Get().audioControl.effectVolume;
            audioSource.Play();
        }

        void OnDestroy()
        {
            lock (workLock)
            {
                shuttingDown = true;
                if (inFlightCancellation != null) inFlightCancellation.Cancel();
                Monitor.Pulse(workLock);
            }
            if (worker != null) worker.Join();
            if (engine != null) engine.Dispose();
            if (audioSource != null && audioSource.clip != null) Destroy(audioSource.clip);
        }
    }
}
