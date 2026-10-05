using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Assets.Scripts.Tts
{
    // Runs the four Supertonic ONNX models to turn text into mono float PCM samples
    public sealed class SupertonicTts : IDisposable
    {
        public const int DefaultTotalSteps = 5;
        public const float DefaultSpeed = 1.05f;
        private const float ChunkSilenceSeconds = 0.3f;

        private readonly InferenceSession durationPredictor;
        private readonly InferenceSession textEncoder;
        private readonly InferenceSession vectorEstimator;
        private readonly InferenceSession vocoder;
        private readonly long[] unicodeIndexer;
        private readonly int baseChunkSize;
        private readonly int chunkCompressFactor;
        private readonly int latentDim;
        private readonly Random random = new Random();

        public int SampleRate { get; private set; }

        public SupertonicTts(string onnxDirectory)
        {
            var config = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(Path.Combine(onnxDirectory, "tts.json")));
            var autoEncoder = (Dictionary<string, object>)config["ae"];
            var textToLatent = (Dictionary<string, object>)config["ttl"];
            SampleRate = (int)(double)autoEncoder["sample_rate"];
            baseChunkSize = (int)(double)autoEncoder["base_chunk_size"];
            chunkCompressFactor = (int)(double)textToLatent["chunk_compress_factor"];
            latentDim = (int)(double)textToLatent["latent_dim"];

            var indexer = (List<object>)MiniJson.Parse(File.ReadAllText(Path.Combine(onnxDirectory, "unicode_indexer.json")));
            unicodeIndexer = indexer.Select(v => (long)(double)v).ToArray();

            using (var options = new SessionOptions())
            {
                durationPredictor = new InferenceSession(Path.Combine(onnxDirectory, "duration_predictor.onnx"), options);
                textEncoder = new InferenceSession(Path.Combine(onnxDirectory, "text_encoder.onnx"), options);
                vectorEstimator = new InferenceSession(Path.Combine(onnxDirectory, "vector_estimator.onnx"), options);
                vocoder = new InferenceSession(Path.Combine(onnxDirectory, "vocoder.onnx"), options);
            }
        }

        public float[] Synthesize(string text, string lang, SupertonicVoice voice, int totalSteps = DefaultTotalSteps, float speed = DefaultSpeed)
        {
            var samples = new List<float>();
            foreach (string chunk in SupertonicText.ChunkText(text, SupertonicText.MaxChunkLength(lang)))
            {
                if (samples.Count > 0)
                {
                    samples.AddRange(new float[(int)(ChunkSilenceSeconds * SampleRate)]);
                }
                samples.AddRange(SynthesizeChunk(chunk, lang, voice, totalSteps, speed));
            }
            return samples.ToArray();
        }

        private float[] SynthesizeChunk(string text, string lang, SupertonicVoice voice, int totalSteps, float speed)
        {
            long[] textIds = SupertonicText.ToTextIds(SupertonicText.Preprocess(text, lang), unicodeIndexer);
            var textIdsTensor = new DenseTensor<long>(textIds, new[] { 1, textIds.Length });
            var textMaskTensor = new DenseTensor<float>(Ones(textIds.Length), new[] { 1, 1, textIds.Length });
            var styleTtlTensor = new DenseTensor<float>(voice.Ttl, voice.TtlShape);
            var styleDpTensor = new DenseTensor<float>(voice.Dp, voice.DpShape);

            float durationSeconds;
            using (var outputs = durationPredictor.Run(new[]
            {
                NamedOnnxValue.CreateFromTensor("text_ids", textIdsTensor),
                NamedOnnxValue.CreateFromTensor("style_dp", styleDpTensor),
                NamedOnnxValue.CreateFromTensor("text_mask", textMaskTensor)
            }))
            {
                durationSeconds = outputs.First(o => o.Name == "duration").AsTensor<float>().First() / speed;
            }

            int sampleCount = (int)(durationSeconds * SampleRate);
            int latentLength = (sampleCount + baseChunkSize * chunkCompressFactor - 1) / (baseChunkSize * chunkCompressFactor);
            int[] latentShape = { 1, latentDim * chunkCompressFactor, latentLength };
            float[] latent = GaussianNoise(latentShape[1] * latentLength);
            var latentMaskTensor = new DenseTensor<float>(Ones(latentLength), new[] { 1, 1, latentLength });
            var totalStepTensor = new DenseTensor<float>(new[] { (float)totalSteps }, new[] { 1 });

            using (var encoderOutputs = textEncoder.Run(new[]
            {
                NamedOnnxValue.CreateFromTensor("text_ids", textIdsTensor),
                NamedOnnxValue.CreateFromTensor("style_ttl", styleTtlTensor),
                NamedOnnxValue.CreateFromTensor("text_mask", textMaskTensor)
            }))
            {
                var textEmbedding = encoderOutputs.First(o => o.Name == "text_emb").AsTensor<float>();
                for (int step = 0; step < totalSteps; step++)
                {
                    using (var stepOutputs = vectorEstimator.Run(new[]
                    {
                        NamedOnnxValue.CreateFromTensor("noisy_latent", new DenseTensor<float>(latent, latentShape)),
                        NamedOnnxValue.CreateFromTensor("text_emb", textEmbedding),
                        NamedOnnxValue.CreateFromTensor("style_ttl", styleTtlTensor),
                        NamedOnnxValue.CreateFromTensor("text_mask", textMaskTensor),
                        NamedOnnxValue.CreateFromTensor("latent_mask", latentMaskTensor),
                        NamedOnnxValue.CreateFromTensor("total_step", totalStepTensor),
                        NamedOnnxValue.CreateFromTensor("current_step", new DenseTensor<float>(new[] { (float)step }, new[] { 1 }))
                    }))
                    {
                        latent = stepOutputs.First(o => o.Name == "denoised_latent").AsTensor<float>().ToArray();
                    }
                }
            }

            using (var vocoderOutputs = vocoder.Run(new[] { NamedOnnxValue.CreateFromTensor("latent", new DenseTensor<float>(latent, latentShape)) }))
            {
                return vocoderOutputs.First(o => o.Name == "wav_tts").AsTensor<float>().Take(sampleCount).ToArray();
            }
        }

        private float[] GaussianNoise(int count)
        {
            var noise = new float[count];
            for (int i = 0; i < count; i++)
            {
                double u1 = 1.0 - random.NextDouble();
                double u2 = 1.0 - random.NextDouble();
                noise[i] = (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
            }
            return noise;
        }

        private static float[] Ones(int count)
        {
            return Enumerable.Repeat(1f, count).ToArray();
        }

        public void Dispose()
        {
            durationPredictor.Dispose();
            textEncoder.Dispose();
            vectorEstimator.Dispose();
            vocoder.Dispose();
        }
    }
}
