using System.IO.Pipes;
using Dictation.Core.Tts;
using Dictation.TtsHost;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.TtsHost
{
    /// <summary>
    /// 验证日语合成引擎参数、资源边界和内存音频输出。
    /// </summary>
    public sealed class PiperJapaneseTtsSynthesisEngineTests
    {
        [Fact]
        public void Constructor_DoesNotLoadResources()
        {
            using var directory = new TemporaryDirectory();

            using var engine = new PiperJapaneseTtsSynthesisEngine(directory.Path);

            Assert.Equal(new[] { TtsLanguage.Japanese }, engine.SupportedLanguages);
        }

        [Fact]
        public void InternalConstructor_RejectsNullOrWhiteSpaceResourceDirectory()
        {
            Assert.Throws<ArgumentNullException>(
                () => new PiperJapaneseTtsSynthesisEngine(null!));
            Assert.Throws<ArgumentException>(
                () => new PiperJapaneseTtsSynthesisEngine(string.Empty));
            Assert.Throws<ArgumentException>(
                () => new PiperJapaneseTtsSynthesisEngine(" \t"));
        }

        [Fact]
        public async Task SynthesizeAsync_RejectsNullRequest()
        {
            using var engine = new PiperJapaneseTtsSynthesisEngine();

            await Assert.ThrowsAsync<ArgumentNullException>(() => engine.SynthesizeAsync(
                null!,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task SynthesizeAsync_RejectsEnglishRequestBeforeLoadingResources()
        {
            using var directory = new TemporaryDirectory();
            using var engine = new PiperJapaneseTtsSynthesisEngine(directory.Path);

            var exception = await Assert.ThrowsAsync<ArgumentException>(() => engine.SynthesizeAsync(
                new TtsSynthesisRequestDto(TtsLanguage.English, "go"),
                TestContext.Current.CancellationToken));

            Assert.Contains("日语", exception.Message);
        }

        [Fact]
        public async Task SynthesizeAsync_RejectsBlankJapaneseTextBeforeLoadingResources()
        {
            using var directory = new TemporaryDirectory();
            using var engine = new PiperJapaneseTtsSynthesisEngine(directory.Path);

            var exception = await Assert.ThrowsAsync<ArgumentException>(() => engine.SynthesizeAsync(
                new TtsSynthesisRequestDto(TtsLanguage.Japanese, " \t\r\n "),
                TestContext.Current.CancellationToken));

            Assert.Contains("不能为空", exception.Message);
        }

        [Fact]
        public async Task SynthesizeAsync_WhenAlreadyCancelledDoesNotLoadResources()
        {
            using var directory = new TemporaryDirectory();
            using var engine = new PiperJapaneseTtsSynthesisEngine(directory.Path);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => engine.SynthesizeAsync(
                new TtsSynthesisRequestDto(TtsLanguage.Japanese, "食べる"),
                cancellation.Token));
        }

        [Fact]
        public async Task SynthesizeAsync_WhenModelIsMissingReturnsChineseError()
        {
            using var directory = new TemporaryDirectory();
            var resourceDirectory = CreateResourceDirectory(directory.Path);
            using var engine = new PiperJapaneseTtsSynthesisEngine(resourceDirectory);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.SynthesizeAsync(
                new TtsSynthesisRequestDto(TtsLanguage.Japanese, "食べる"),
                TestContext.Current.CancellationToken));

            Assert.Contains("日语 Piper Plus 模型文件不存在", exception.Message);
        }

        [Fact]
        public async Task SynthesizeAsync_WhenConfigIsMissingReturnsChineseError()
        {
            using var directory = new TemporaryDirectory();
            var resourceDirectory = CreateResourceDirectory(directory.Path);
            File.WriteAllBytes(GetModelPath(resourceDirectory), new byte[] { 1 });
            using var engine = new PiperJapaneseTtsSynthesisEngine(resourceDirectory);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.SynthesizeAsync(
                new TtsSynthesisRequestDto(TtsLanguage.Japanese, "食べる"),
                TestContext.Current.CancellationToken));

            Assert.Contains("日语 Piper Plus 配置文件不存在", exception.Message);
        }

        [Fact]
        public async Task SynthesizeAsync_WhenDictionaryIsMissingReturnsChineseError()
        {
            using var directory = new TemporaryDirectory();
            var resourceDirectory = CreateResourceDirectory(directory.Path);
            File.WriteAllBytes(GetModelPath(resourceDirectory), new byte[] { 1 });
            File.WriteAllText(GetConfigPath(resourceDirectory), CreateConfigJson());
            using var engine = new PiperJapaneseTtsSynthesisEngine(resourceDirectory);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.SynthesizeAsync(
                new TtsSynthesisRequestDto(TtsLanguage.Japanese, "食べる"),
                TestContext.Current.CancellationToken));

            Assert.Contains("OpenJTalk 日语词典文件不存在", exception.Message);
        }

        [Fact]
        public async Task SynthesizeAsync_WhenConfigHasNoJapaneseLanguageIdReturnsChineseError()
        {
            using var directory = new TemporaryDirectory();
            var resourceDirectory = CreateResourceDirectory(directory.Path);
            File.WriteAllBytes(GetModelPath(resourceDirectory), new byte[] { 1 });
            File.WriteAllText(GetConfigPath(resourceDirectory), CreateConfigJson(includeJapaneseLanguage: false));
            CreateDictionaryFiles(resourceDirectory);
            using var engine = new PiperJapaneseTtsSynthesisEngine(resourceDirectory);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.SynthesizeAsync(
                new TtsSynthesisRequestDto(TtsLanguage.Japanese, "食べる"),
                TestContext.Current.CancellationToken));

            Assert.Contains("配置缺少语言 id ja", exception.Message);
        }

        [Fact]
        public async Task Server_PingWorksWithoutJapaneseResources()
        {
            using var directory = new TemporaryDirectory();
            using var engine = new PiperJapaneseTtsSynthesisEngine(directory.Path);
            await using var server = await RunningServer.StartAsync(engine);

            var response = await server.SendAsync(new TtsPipeRequest(TtsPipeMessageType.Ping));

            Assert.Equal(TtsPipeMessageType.Pong, response.Type);
        }

        private static string CreateResourceDirectory(string root)
        {
            var resourceDirectory = Path.Combine(root, "Resources", "Tts", "Japanese", "PiperPlus");
            Directory.CreateDirectory(resourceDirectory);
            return resourceDirectory;
        }

        private static void CreateDictionaryFiles(string resourceDirectory)
        {
            var dictionaryDirectory = Path.Combine(
                resourceDirectory,
                "OpenJTalk",
                "open_jtalk_dic_utf_8-1.11");
            Directory.CreateDirectory(dictionaryDirectory);
            foreach (var name in new[] { "sys.dic", "matrix.bin", "char.bin", "unk.dic" })
            {
                File.WriteAllBytes(Path.Combine(dictionaryDirectory, name), new byte[] { 1 });
            }
        }

        private static string GetModelPath(string resourceDirectory)
        {
            return Path.Combine(resourceDirectory, "css10-ja-6lang-fp16.onnx");
        }

        private static string GetConfigPath(string resourceDirectory)
        {
            return Path.Combine(resourceDirectory, "config.json");
        }

        private static string CreateConfigJson(bool includeJapaneseLanguage = true)
        {
            var languageMap = includeJapaneseLanguage
                ? "\"ja\": 0"
                : "\"en\": 1";
            return $$"""
                {
                  "num_speakers": 1,
                  "phoneme_id_map": {"_": [0], "^": [1], "$": [2]},
                  "audio": {"sample_rate": 22050},
                  "inference": {"noise_scale": 0.667, "length_scale": 1, "noise_w": 0.8},
                  "language_id_map": { {{languageMap}} }
                }
                """;
        }

        private sealed class RunningServer : IAsyncDisposable
        {
            private readonly TtsPipeServer server;
            private readonly Task running;

            private RunningServer(TtsHostSession session, TtsPipeServer server, Task running)
            {
                Session = session;
                this.server = server;
                this.running = running;
            }

            public TtsHostSession Session { get; }

            public static async Task<RunningServer> StartAsync(ITtsSynthesisEngine engine)
            {
                var session = new TtsHostSession($"Dictation.Tests.Tts.{Guid.NewGuid():N}");
                var server = new TtsPipeServer(session, new[] { engine });
                var running = server.RunAsync(TestContext.Current.CancellationToken);
                await Task.Delay(20, TestContext.Current.CancellationToken);
                return new RunningServer(session, server, running);
            }

            public async Task<TtsPipeResponse> SendAsync(TtsPipeRequest request)
            {
                await using var client = new NamedPipeClientStream(
                    ".",
                    Session.PipeName,
                    PipeDirection.InOut,
                    PipeOptions.Asynchronous);
                await client.ConnectAsync(2000, TestContext.Current.CancellationToken);
                await TtsPipeCodec.WriteRequestAsync(client, request, TestContext.Current.CancellationToken);
                return await TtsPipeCodec.ReadResponseAsync(client, TestContext.Current.CancellationToken);
            }

            public async ValueTask DisposeAsync()
            {
                if (!running.IsCompleted)
                {
                    try
                    {
                        await SendAsync(new TtsPipeRequest(TtsPipeMessageType.Shutdown));
                    }
                    catch
                    {
                        server.Dispose();
                    }
                }

                await running.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
                server.Dispose();
            }
        }
    }
}
