using Dictation.Core.Tts;
using PiperPlus.Core.Inference;
using SherpaOnnx;

namespace Dictation.TtsHost
{
    /// <summary>
    /// 使用 Kokoro CPU 模型按需生成内存 WAV 音频。
    /// </summary>
    internal sealed class KokoroEnglishTtsSynthesisEngine : ITtsSynthesisEngine, IDisposable
    {
        private const int SpeakerId = 21;
        private const int ThreadCount = 2;
        private const float SpeechSpeed = 1.0f;

        private static readonly IReadOnlyCollection<TtsLanguage> Languages =
            Array.AsReadOnly(new[] { TtsLanguage.English });

        private readonly string resourceDirectory;
        private readonly Lazy<KokoroEnglishRuntime> runtime;
        private int disposed;

        public KokoroEnglishTtsSynthesisEngine()
            : this(Path.Combine(
                AppContext.BaseDirectory,
                "Resources",
                "Tts",
                "English",
                "Kokoro"))
        {
        }

        internal KokoroEnglishTtsSynthesisEngine(string resourceDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(resourceDirectory);
            this.resourceDirectory = Path.GetFullPath(resourceDirectory);
            runtime = new Lazy<KokoroEnglishRuntime>(
                LoadRuntime,
                LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public IReadOnlyCollection<TtsLanguage> SupportedLanguages => Languages;

        /// <inheritdoc />
        public Task<byte[]> SynthesizeAsync(
            TtsSynthesisRequestDto request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            ThrowIfDisposed();

            if (request.Language != TtsLanguage.English)
            {
                throw new ArgumentException(
                    "Kokoro 英语引擎只支持英语请求。",
                    nameof(request.Language));
            }

            if (string.IsNullOrWhiteSpace(request.Text))
            {
                throw new ArgumentException("英语 TTS 文本不能为空。", nameof(request.Text));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var audio = runtime.Value.Synthesize(request.Text);
            return Task.FromResult(audio);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            if (runtime.IsValueCreated)
            {
                runtime.Value.Dispose();
            }
        }

        private KokoroEnglishRuntime LoadRuntime()
        {
            var modelPath = RequireFile(
                "model.onnx",
                "英语 TTS 模型尚未配置；英语 Kokoro 模型文件不存在");
            var voicesPath = RequireFile(
                "voices.bin",
                "英语 Kokoro voices 文件不存在");
            var tokensPath = RequireFile(
                "tokens.txt",
                "英语 Kokoro tokens 文件不存在");
            var lexiconPath = RequireFile(
                "lexicon-gb-en.txt",
                "英语 Kokoro 英式词典文件不存在");
            var espeakDataDirectory = Path.Combine(resourceDirectory, "espeak-ng-data");
            if (!Directory.Exists(espeakDataDirectory))
            {
                throw new InvalidOperationException(
                    $"英语 Kokoro eSpeak 数据目录不存在：{espeakDataDirectory}");
            }

            try
            {
                var config = new OfflineTtsConfig();
                config.Model.NumThreads = ThreadCount;
                config.Model.Debug = 0;
                config.Model.Provider = "cpu";
                config.Model.Kokoro.Model = modelPath;
                config.Model.Kokoro.Voices = voicesPath;
                config.Model.Kokoro.Tokens = tokensPath;
                config.Model.Kokoro.DataDir = espeakDataDirectory;
                config.Model.Kokoro.Lexicon = lexiconPath;

                return new KokoroEnglishRuntime(new OfflineTts(config));
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"无法加载英语 Kokoro 模型或 eSpeak 数据：{exception.Message}",
                    exception);
            }
        }

        private string RequireFile(string fileName, string message)
        {
            var path = Path.Combine(resourceDirectory, fileName);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"{message}：{path}");
            }

            return path;
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed != 0, this);
        }

        private sealed class KokoroEnglishRuntime : IDisposable
        {
            private readonly OfflineTts tts;
            private readonly object synthesisGate = new();
            private int disposed;

            public KokoroEnglishRuntime(OfflineTts tts)
            {
                this.tts = tts;
            }

            public byte[] Synthesize(string text)
            {
                lock (synthesisGate)
                {
                    ObjectDisposedException.ThrowIf(disposed != 0, this);

                    var generated = tts.Generate(text, SpeechSpeed, SpeakerId);
                    if (generated is null)
                    {
                        throw new InvalidOperationException("英语 Kokoro 引擎未生成音频。生成结果为空。");
                    }

                    try
                    {
                        var samples = generated.Samples;
                        if (samples is null || generated.NumSamples <= 0 || generated.SampleRate <= 0)
                        {
                            throw new InvalidOperationException("英语 Kokoro 引擎未生成有效音频。");
                        }

                        var sampleCount = Math.Min(generated.NumSamples, samples.Length);
                        if (sampleCount == 0)
                        {
                            throw new InvalidOperationException("英语 Kokoro 引擎未生成有效音频。");
                        }

                        var pcm = new short[sampleCount];
                        for (var index = 0; index < sampleCount; index++)
                        {
                            var sample = samples[index];
                            if (float.IsNaN(sample))
                            {
                                sample = 0;
                            }

                            sample = Math.Clamp(sample, -1.0f, 1.0f);
                            pcm[index] = (short)Math.Round(sample * short.MaxValue);
                        }

                        using var stream = new MemoryStream();
                        WavWriter.Write(stream, pcm, generated.SampleRate);
                        return stream.ToArray();
                    }
                    finally
                    {
                        generated.Dispose();
                    }
                }
            }

            public void Dispose()
            {
                lock (synthesisGate)
                {
                    if (Interlocked.Exchange(ref disposed, 1) != 0)
                    {
                        return;
                    }

                    tts.Dispose();
                }
            }
        }
    }
}
