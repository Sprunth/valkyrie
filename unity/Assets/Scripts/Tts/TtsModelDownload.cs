using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using ValkyrieTools;

namespace Assets.Scripts.Tts
{
    // Downloads the Supertonic model file by file, resuming partial files and backing off when the server is busy
    public class TtsModelDownload
    {
        // Space-separated, tried in order: the pinned archive revision, then the identical files in the original publisher's repo
        public const string DefaultSources = "https://huggingface.co/supertone-oss-archive/supertonic-3/resolve/aafc6e32416a594460b32413efc49d7fe4ce6d46/ "
            + "https://huggingface.co/Supertone/supertonic-3/resolve/3cadd1ee6394adea1bd021217a0e650ede09a323/";
        public const long ApproxTotalBytes = 401000000;
        private const int MaxAttemptsWithoutProgress = 8;
        private const float StallTimeoutSeconds = 60;

        public static readonly string[] Files = new[] { "LICENSE", "onnx/tts.json", "onnx/unicode_indexer.json", "onnx/duration_predictor.onnx",
                "onnx/text_encoder.onnx", "onnx/vector_estimator.onnx", "onnx/vocoder.onnx" }
            .Concat(new[] { "M", "F" }.SelectMany(g => Enumerable.Range(1, 5).Select(i => "voice_styles/" + g + i + ".json")))
            .ToArray();

        public bool Running { get; private set; }
        public bool Failed { get; private set; }
        public int RetryInSeconds { get; private set; }
        public float Progress { get; private set; }

        public static string[] Sources(string configured)
        {
            return (configured.Trim().Length > 0 ? configured : DefaultSources).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static string FileUrl(string source, string file)
        {
            return source.TrimEnd('/') + "/" + file;
        }

        public static bool IsRetryable(long responseCode)
        {
            return responseCode == 0 || responseCode == 408 || responseCode == 429 || responseCode >= 500;
        }

        public static int RetryDelaySeconds(int attempt, string retryAfterHeader)
        {
            int seconds;
            if (int.TryParse(retryAfterHeader, out seconds) && seconds >= 0) return Math.Min(seconds, 300);
            return Math.Min(5 << Math.Min(attempt, 5), 120);
        }

        public IEnumerator Run(string[] sources, string stagingDirectory, string targetDirectory, Action onComplete)
        {
            Running = true;
            long completedBytes = 0;
            foreach (string file in Files)
            {
                string path = Path.Combine(stagingDirectory, file);
                foreach (string source in sources)
                {
                    if (File.Exists(path)) break;
                    Failed = false;
                    yield return DownloadFile(FileUrl(source, file), path, completedBytes);
                }
                if (!File.Exists(path))
                {
                    Failed = true;
                    Running = false;
                    yield break;
                }
                completedBytes += new FileInfo(path).Length;
            }
            Running = false;
            if (TryFileOperation(() => Directory.Move(stagingDirectory, targetDirectory))) onComplete();
        }

        private bool TryFileOperation(Action operation)
        {
            try
            {
                operation();
                return true;
            }
            catch (Exception e)
            {
                ValkyrieDebug.Log("Warning: Narration model download failed: " + e.Message);
                Failed = true;
                return false;
            }
        }

        private IEnumerator DownloadFile(string url, string path, long completedBytes)
        {
            if (!TryFileOperation(() => Directory.CreateDirectory(Path.GetDirectoryName(path)))) yield break;
            string partPath = path + ".part";
            int attempt = 0;
            while (true)
            {
                long existing = File.Exists(partPath) ? new FileInfo(partPath).Length : 0;
                using (var request = UnityWebRequest.Get(url))
                {
                    request.downloadHandler = new DownloadHandlerFile(partPath, existing > 0) { removeFileOnAbort = false };
                    if (existing > 0) request.SetRequestHeader("Range", "bytes=" + existing + "-");
                    request.SendWebRequest();

                    ulong lastBytes = 0;
                    float lastProgressTime = Time.realtimeSinceStartup;
                    while (!request.isDone)
                    {
                        if (request.downloadedBytes != lastBytes)
                        {
                            lastBytes = request.downloadedBytes;
                            lastProgressTime = Time.realtimeSinceStartup;
                        }
                        else if (Time.realtimeSinceStartup - lastProgressTime > StallTimeoutSeconds)
                        {
                            request.Abort();
                        }
                        Progress = Mathf.Min(0.99f, (float)(completedBytes + existing + (long)request.downloadedBytes) / ApproxTotalBytes);
                        yield return null;
                    }

                    if ((existing > 0 && request.responseCode == 200) || request.responseCode == 416)
                    {
                        if (TryFileOperation(() => File.Delete(partPath))) continue;
                        yield break;
                    }
                    if (!request.isNetworkError && (request.responseCode == 200 || request.responseCode == 206))
                    {
                        TryFileOperation(() => File.Move(partPath, path));
                        yield break;
                    }
                    if (request.downloadedBytes > 0) attempt = 0;
                    bool retryable = request.isNetworkError || IsRetryable(request.responseCode);
                    if (!retryable || ++attempt > MaxAttemptsWithoutProgress)
                    {
                        ValkyrieDebug.Log("Warning: Narration model download failed for " + url + ": " + request.responseCode + " " + request.error);
                        Failed = true;
                        yield break;
                    }
                    RetryInSeconds = RetryDelaySeconds(attempt - 1, request.GetResponseHeader("Retry-After"));
                }

                for (; RetryInSeconds > 0; RetryInSeconds--)
                {
                    yield return new WaitForSecondsRealtime(1);
                }
            }
        }
    }
}
