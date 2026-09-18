using Dictation.Core.Tts;
using DotNetG2P;
using DotNetG2P.MeCab;
using DotNetG2P.MeCab.Dictionary;
using Microsoft.ML.OnnxRuntime;
using PiperPlus.Core.Config;
using PiperPlus.Core.Inference;
using PiperPlus.Core.Phonemize;

namespace Dictation.TtsHost
{
    /// <summary>
    /// 使用 Piper 与日语 G2P 资源按需生成内存 WAV 音频。
    /// </summary>
    internal sealed class PiperJapaneseTtsSynthesisEngine : ITtsSynthesisEngine, IDisposable
    {
        private static readonly IReadOnlyCollection<TtsLanguage> Languages =
            Array.AsReadOnly(new[] { TtsLanguage.Japanese });

        private readonly string resourceDirectory;
        private readonly Lazy<PiperJapaneseRuntime> runtime;
        private int disposed;

        /// <summary>
        /// 使用应用发布目录内的日语资源；构造时不检查文件或加载模型。
        /// </summary>
        public PiperJapaneseTtsSynthesisEngine()
            : this(Path.Combine(
                AppContext.BaseDirectory,
                "Resources",
                "Tts",
                "Japanese",
                "PiperPlus"))
        {
        }

        /// <summary>
        /// 固定日语资源目录，首次合成时才创建模型及 G2P 运行时。
        /// </summary>
        /// <param name="resourceDirectory">包含 Piper 模型、配置及 OpenJTalk 词典的目录。</param>
        internal PiperJapaneseTtsSynthesisEngine(string resourceDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(resourceDirectory);
            this.resourceDirectory = Path.GetFullPath(
                resourceDirectory);
            runtime = new Lazy<PiperJapaneseRuntime>(
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

            if (request.Language != TtsLanguage.Japanese)
            {
                throw new ArgumentException(
                    "Piper 日语引擎只支持日语请求。",
                    nameof(request.Language));
            }

            if (string.IsNullOrWhiteSpace(request.Text))
            {
                throw new ArgumentException("日语 TTS 文本不能为空。", nameof(request.Text));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var audio = runtime.Value.Synthesize(request.Text);
            return Task.FromResult(audio);
        }

        /// <inheritdoc />
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

        private PiperJapaneseRuntime LoadRuntime()
        {
            var modelPath = RequireFile(
                "css10-ja-6lang-fp16.onnx",
                "日语 Piper Plus 模型文件不存在");
            var configPath = RequireFile(
                "config.json",
                "日语 Piper Plus 配置文件不存在");
            var dictionaryDirectory = Path.Combine(
                resourceDirectory,
                "OpenJTalk",
                "open_jtalk_dic_utf_8-1.11");
            RequireDictionaryFiles(dictionaryDirectory);

            PiperConfig config;
            try
            {
                config = PiperConfig.LoadFromFile(configPath);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"无法加载日语 Piper Plus 配置文件：{exception.Message}",
                    exception);
            }

            if (config.LanguageIdMap is null
                || !config.LanguageIdMap.TryGetValue("ja", out var languageId))
            {
                throw new InvalidOperationException(
                    "日语 Piper Plus 配置缺少语言 id ja。模型必须提供日语语言映射。");
            }

            DotNetG2PJapaneseAdapter? phonemizer = null;
            InferenceSession? session = null;
            PiperModel? model = null;
            try
            {
                phonemizer = new DotNetG2PJapaneseAdapter(dictionaryDirectory);
                session = CreateCpuSession(modelPath);
                model = new PiperModel(session, config);
                var piperSession = new PiperSession(model);
                return new PiperJapaneseRuntime(
                    config,
                    languageId,
                    phonemizer,
                    model,
                    piperSession);
            }
            catch (Exception exception)
            {
                phonemizer?.Dispose();
                model?.Dispose();
                if (model is null)
                {
                    session?.Dispose();
                }
                throw new InvalidOperationException(
                    $"无法加载日语 Piper Plus 模型或 OpenJTalk 词典：{exception.Message}",
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

        private static void RequireDictionaryFiles(string dictionaryDirectory)
        {
            foreach (var fileName in new[] { "sys.dic", "matrix.bin", "char.bin", "unk.dic" })
            {
                var path = Path.Combine(dictionaryDirectory, fileName);
                if (!File.Exists(path))
                {
                    throw new InvalidOperationException(
                        $"OpenJTalk 日语词典文件不存在：{path}");
                }
            }
        }

        private static InferenceSession CreateCpuSession(string modelPath)
        {
            var previousCacheSetting = Environment.GetEnvironmentVariable("PIPER_DISABLE_CACHE");
            try
            {
                Environment.SetEnvironmentVariable("PIPER_DISABLE_CACHE", "1");
                return SessionFactory.Create(modelPath, useCuda: false);
            }
            finally
            {
                Environment.SetEnvironmentVariable("PIPER_DISABLE_CACHE", previousCacheSetting);
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed != 0, this);
        }

        private sealed class PiperJapaneseRuntime : IDisposable
        {
            private readonly PiperConfig config;
            private readonly int languageId;
            private readonly DotNetG2PJapaneseAdapter phonemizer;
            private readonly JapanesePhonemizer japanesePhonemizer;
            private readonly PiperModel model;
            private readonly PiperSession session;
            private readonly object synthesisGate = new();
            private int disposed;

            public PiperJapaneseRuntime(
                PiperConfig config,
                int languageId,
                DotNetG2PJapaneseAdapter phonemizer,
                PiperModel model,
                PiperSession session)
            {
                this.config = config;
                this.languageId = languageId;
                this.phonemizer = phonemizer;
                japanesePhonemizer = new JapanesePhonemizer(phonemizer);
                this.model = model;
                this.session = session;
            }

            public byte[] Synthesize(string text)
            {
                ObjectDisposedException.ThrowIf(disposed != 0, this);

                lock (synthesisGate)
                {
                    (long[] phonemeIds, long[]? prosodyFeatures) = PhonemeEncoder.EncodeDirect(
                        japanesePhonemizer,
                        text,
                        japanesePhonemizer.GetPhonemeIdMap() ?? config.PhonemeIdMap);
                    if (!model.HasProsody)
                    {
                        prosodyFeatures = null;
                    }

                    var input = new SynthesisInput(
                        phonemeIds,
                        SpeakerId: 0,
                        LanguageId: languageId,
                        ProsodyFeatures: prosodyFeatures,
                        NoiseScale: config.Inference.NoiseScale,
                        LengthScale: config.Inference.LengthScale,
                        NoiseW: config.Inference.NoiseW,
                        SpeakerEmbedding: null);
                    var audio = session.Synthesize(input);
                    if (audio.Length == 0)
                    {
                        throw new InvalidOperationException("日语 Piper Plus 引擎未生成音频。");
                    }

                    using var stream = new MemoryStream();
                    WavWriter.Write(stream, audio, model.SampleRate);
                    return stream.ToArray();
                }
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) != 0)
                {
                    return;
                }

                model.Dispose();
                phonemizer.Dispose();
            }
        }

        private sealed class DotNetG2PJapaneseAdapter : IJapaneseG2PEngine, IDisposable
        {
            private readonly DictionaryBundle dictionary;
            private readonly MeCabTokenizer tokenizer;
            private readonly G2PEngine engine;

            public DotNetG2PJapaneseAdapter(string dictionaryDirectory)
            {
                var resources = LoadResources(dictionaryDirectory);
                dictionary = resources.Dictionary;
                tokenizer = resources.Tokenizer;
                engine = resources.Engine;
            }

            public G2PResult Convert(string text)
            {
                var features = engine.ToProsodyFeatures(text);
                return new G2PResult(
                    features.Phonemes.ToArray(),
                    features.A1.ToArray(),
                    features.A2.ToArray(),
                    features.A3.ToArray());
            }

            public void Dispose()
            {
                tokenizer.Dispose();
                dictionary.Dispose();
            }

            private static (DictionaryBundle Dictionary, MeCabTokenizer Tokenizer, G2PEngine Engine) LoadResources(
                string dictionaryDirectory)
            {
                var loadedDictionary = DictionaryBundle.Load(dictionaryDirectory);
                MeCabTokenizer? loadedTokenizer = null;
                try
                {
                    loadedTokenizer = new MeCabTokenizer(loadedDictionary);
                    var loadedEngine = new G2PEngine(loadedTokenizer);
                    return (loadedDictionary, loadedTokenizer, loadedEngine);
                }
                catch
                {
                    loadedTokenizer?.Dispose();
                    loadedDictionary.Dispose();
                    throw;
                }
            }
        }
    }
}
