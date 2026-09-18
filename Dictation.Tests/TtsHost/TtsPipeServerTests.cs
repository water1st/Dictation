using System.Buffers.Binary;
using System.IO.Pipes;
using Dictation.Core.Tts;
using Dictation.TtsHost;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.TtsHost
{
    /// <summary>
    /// 验证命名管道协议、合成路由、错误响应及服务生命周期。
    /// </summary>
    public sealed class TtsPipeServerTests
    {
        [Fact]
        public async Task Server_RoutesJapaneseAndEnglishRequestsToMatchingEnginesAndReturnsWav()
        {
            var japanese = new RecordingEngine(TtsLanguage.Japanese, new byte[] { 1, 2, 3 });
            var english = new RecordingEngine(TtsLanguage.English, new byte[] { 4, 5, 6 });
            await using var server = await RunningServer.StartAsync(japanese, english);

            var japaneseResponse = await server.SendAsync(
                new TtsPipeRequest(TtsPipeMessageType.Synthesize, TtsLanguage.Japanese, "食べます"));
            var englishResponse = await server.SendAsync(
                new TtsPipeRequest(TtsPipeMessageType.Synthesize, TtsLanguage.English, "goes"));

            Assert.Equal(TtsPipeMessageType.Audio, japaneseResponse.Type);
            Assert.Equal(new byte[] { 1, 2, 3 }, japaneseResponse.Payload);
            Assert.Equal(TtsPipeMessageType.Audio, englishResponse.Type);
            Assert.Equal(new byte[] { 4, 5, 6 }, englishResponse.Payload);
            Assert.Equal("食べます", japanese.LastRequest!.Text);
            Assert.Equal(TtsLanguage.Japanese, japanese.LastRequest.Language);
            Assert.Equal("goes", english.LastRequest!.Text);
            Assert.Equal(TtsLanguage.English, english.LastRequest.Language);
        }

        [Fact]
        public async Task Server_WhenLanguageIsNotRegistered_ReturnsChineseError()
        {
            await using var server = await RunningServer.StartAsync(
                new RecordingEngine(TtsLanguage.Japanese, new byte[] { 1 }));

            var response = await server.SendAsync(
                new TtsPipeRequest(TtsPipeMessageType.Synthesize, TtsLanguage.English, "go"));

            Assert.Equal(TtsPipeMessageType.Error, response.Type);
            Assert.Contains("不支持", response.Error);
        }

        [Fact]
        public async Task Server_WhenEngineThrows_ReturnsErrorAndServesNextRequest()
        {
            var engine = new RecordingEngine(TtsLanguage.English, new byte[] { 7 })
            {
                Failure = new InvalidOperationException("模型故障")
            };
            await using var server = await RunningServer.StartAsync(engine);

            var failed = await server.SendAsync(
                new TtsPipeRequest(TtsPipeMessageType.Synthesize, TtsLanguage.English, "go"));
            engine.Failure = null;
            var succeeded = await server.SendAsync(
                new TtsPipeRequest(TtsPipeMessageType.Synthesize, TtsLanguage.English, "went"));

            Assert.Equal(TtsPipeMessageType.Error, failed.Type);
            Assert.Contains("模型故障", failed.Error);
            Assert.Equal(TtsPipeMessageType.Audio, succeeded.Type);
            Assert.Equal(new byte[] { 7 }, succeeded.Payload);
        }

        [Fact]
        public async Task Server_PingReturnsPong()
        {
            await using var server = await RunningServer.StartAsync();

            var response = await server.SendAsync(new TtsPipeRequest(TtsPipeMessageType.Ping));

            Assert.Equal(TtsPipeMessageType.Pong, response.Type);
        }

        [Fact]
        public async Task Server_ShutdownReturnsAcknowledgementAndStops()
        {
            await using var server = await RunningServer.StartAsync();

            var response = await server.SendAsync(new TtsPipeRequest(TtsPipeMessageType.Shutdown));

            Assert.Equal(TtsPipeMessageType.ShutdownAcknowledged, response.Type);
            await server.Completion.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        }

        [Fact]
        public async Task Server_ExternalCancellationStopsWithoutAClient()
        {
            var session = new TtsHostSession(CreatePipeName());
            using var cancellation = new CancellationTokenSource();
            using var server = new TtsPipeServer(session, Array.Empty<ITtsSynthesisEngine>());
            var running = server.RunAsync(cancellation.Token);

            cancellation.Cancel();

            await running.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
            Assert.True(running.IsCompletedSuccessfully);
        }

        [Fact]
        public async Task Provider_WhenServerIsUnavailableThrowsClearException()
        {
            var provider = new NamedPipeTtsProvider(new TtsHostSession(CreatePipeName()));

            var exception = await Assert.ThrowsAsync<TtsProviderException>(() => provider.SynthesizeAsync(
                new TtsSynthesisRequestDto(TtsLanguage.English, "go"),
                TestContext.Current.CancellationToken));

            Assert.Contains("连接", exception.Message);
        }

        [Fact]
        public async Task Provider_RoundTripsSynthesisThroughPipe()
        {
            var engine = new RecordingEngine(TtsLanguage.English, new byte[] { 9, 8, 7 });
            await using var server = await RunningServer.StartAsync(engine);
            var provider = new NamedPipeTtsProvider(server.Session);

            var result = await provider.SynthesizeAsync(
                new TtsSynthesisRequestDto(TtsLanguage.English, "doing"),
                TestContext.Current.CancellationToken);

            Assert.Equal(new byte[] { 9, 8, 7 }, result.WavData);
            Assert.Equal("doing", engine.LastRequest!.Text);
        }

        [Fact]
        public async Task Codec_RejectsUnknownVersion()
        {
            var frame = CreateRequestFrame(99, TtsPipeMessageType.Ping, 0xff, 0);

            var exception = await Assert.ThrowsAsync<TtsPipeProtocolException>(() =>
                TtsPipeCodec.ReadRequestAsync(new MemoryStream(frame), TestContext.Current.CancellationToken));

            Assert.Contains("版本", exception.Message);
        }

        [Fact]
        public async Task Codec_RejectsUnknownMessageType()
        {
            var frame = CreateRequestFrame(1, 99, 0xff, 0);

            var exception = await Assert.ThrowsAsync<TtsPipeProtocolException>(() =>
                TtsPipeCodec.ReadRequestAsync(new MemoryStream(frame), TestContext.Current.CancellationToken));

            Assert.Contains("消息类型", exception.Message);
        }

        [Fact]
        public async Task Codec_RejectsNegativeAndOversizedLengths()
        {
            var negativePayload = CreateRequestFrame(
                TtsPipeCodec.ProtocolVersion,
                TtsPipeMessageType.Synthesize,
                (byte)TtsLanguage.English,
                -1);
            var oversizedText = CreateRequestFrame(
                TtsPipeCodec.ProtocolVersion,
                TtsPipeMessageType.Synthesize,
                (byte)TtsLanguage.English,
                TtsPipeCodec.MaximumTextUtf8Length + 1);
            var oversizedAudio = CreateResponseFrame(
                TtsPipeCodec.ProtocolVersion,
                TtsPipeMessageType.Audio,
                TtsPipeCodec.MaximumAudioLength + 1);

            await Assert.ThrowsAsync<TtsPipeProtocolException>(() =>
                TtsPipeCodec.ReadRequestAsync(new MemoryStream(negativePayload), TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<TtsPipeProtocolException>(() =>
                TtsPipeCodec.ReadRequestAsync(new MemoryStream(oversizedText), TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<TtsPipeProtocolException>(() =>
                TtsPipeCodec.ReadResponseAsync(new MemoryStream(oversizedAudio), TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Codec_RejectsTruncatedMetadataAndPayload()
        {
            var requestFrame = CreateRequestFrame(1, TtsPipeMessageType.Ping, 0xff, 0);
            var truncatedHeader = requestFrame[..^1];
            var audioFrame = CreateResponseFrame(1, TtsPipeMessageType.Audio, 3, new byte[] { 1 });

            await Assert.ThrowsAsync<TtsPipeProtocolException>(() =>
                TtsPipeCodec.ReadRequestAsync(new MemoryStream(truncatedHeader), TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<TtsPipeProtocolException>(() =>
                TtsPipeCodec.ReadResponseAsync(new MemoryStream(audioFrame), TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Codec_RejectsInvalidUtf8AndInvalidMessagePayloads()
        {
            var invalidText = CreateRequestFrame(
                1,
                TtsPipeMessageType.Synthesize,
                (byte)TtsLanguage.English,
                1,
                new byte[] { 0xff });
            var invalidError = CreateResponseFrame(
                1,
                TtsPipeMessageType.Error,
                1,
                new byte[] { 0xff });
            var emptyAudio = CreateResponseFrame(1, TtsPipeMessageType.Audio, 0);
            var blankError = CreateResponseFrame(1, TtsPipeMessageType.Error, 1, new byte[] { (byte)' ' });
            var pingWithPayload = CreateRequestFrame(1, TtsPipeMessageType.Ping, 0xff, 1, new byte[] { 1 });
            var invalidLanguage = CreateRequestFrame(1, TtsPipeMessageType.Synthesize, 0xfe, 2, new byte[] { (byte)'g', (byte)'o' });

            await Assert.ThrowsAsync<TtsPipeProtocolException>(() =>
                TtsPipeCodec.ReadRequestAsync(new MemoryStream(invalidText), TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<TtsPipeProtocolException>(() =>
                TtsPipeCodec.ReadResponseAsync(new MemoryStream(invalidError), TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<TtsPipeProtocolException>(() =>
                TtsPipeCodec.ReadResponseAsync(new MemoryStream(emptyAudio), TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<TtsPipeProtocolException>(() =>
                TtsPipeCodec.ReadResponseAsync(new MemoryStream(blankError), TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<TtsPipeProtocolException>(() =>
                TtsPipeCodec.ReadRequestAsync(new MemoryStream(pingWithPayload), TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<TtsPipeProtocolException>(() =>
                TtsPipeCodec.ReadRequestAsync(new MemoryStream(invalidLanguage), TestContext.Current.CancellationToken));
        }

        [Fact]
        public void Session_RejectsInvalidPipeNames()
        {
            Assert.Throws<ArgumentException>(() => new TtsHostSession(null!));
            Assert.Throws<ArgumentException>(() => new TtsHostSession(""));
            Assert.Throws<ArgumentException>(() => new TtsHostSession(" "));
            Assert.Throws<ArgumentException>(() => new TtsHostSession("bad/name"));
            Assert.Throws<ArgumentException>(() => new TtsHostSession("bad\\name"));
            Assert.Throws<ArgumentException>(() => new TtsHostSession(new string('a', 129)));
        }

        [Fact]
        public void Server_RejectsDuplicateLanguageRegistration()
        {
            var session = new TtsHostSession(CreatePipeName());
            var japanese = new RecordingEngine(TtsLanguage.Japanese, new byte[] { 1 });

            Assert.Throws<ArgumentException>(() => new TtsPipeServer(session, new[] { japanese, japanese }));
        }

        [Fact]
        public void TtsHostServices_ResolvesServerThroughOneContainer()
        {
            var session = new TtsHostSession(CreatePipeName());
            using var services = new ServiceCollection()
                .AddSingleton(session)
                .AddSingleton<ITtsSynthesisEngine>(new RecordingEngine(TtsLanguage.Japanese, new byte[] { 1 }))
                .AddSingleton<TtsPipeServer>()
                .BuildServiceProvider();

            var first = services.GetRequiredService<TtsPipeServer>();
            var second = services.GetRequiredService<TtsPipeServer>();

            Assert.Same(first, second);
        }

        [Fact]
        public void ProgramRegistration_MapsJapaneseToPiperAndEnglishToKokoroOnce()
        {
            var options = new TtsHostOptions(
                new TtsHostSession(CreatePipeName()),
                Environment.ProcessId);
            using var services = Program.BuildServiceProvider(options);

            var engines = services.GetServices<ITtsSynthesisEngine>().ToArray();
            var languages = engines
                .SelectMany(engine => engine.SupportedLanguages)
                .ToArray();

            Assert.Equal(2, engines.Length);
            Assert.Equal(
                new[] { TtsLanguage.Japanese, TtsLanguage.English },
                languages.OrderBy(language => language));
            Assert.Contains(engines, engine => engine is PiperJapaneseTtsSynthesisEngine);
            Assert.Contains(engines, engine => engine is KokoroEnglishTtsSynthesisEngine);
        }

        [Fact]
        public void HostOptions_RequiresPipeAndParentProcessIdArguments()
        {
            var parentProcessId = Environment.ProcessId.ToString();
            Assert.True(TtsHostOptions.TryParse(
                new[] { "--pipe", CreatePipeName(), "--parent-process-id", parentProcessId },
                out var options));
            Assert.NotNull(options);
            Assert.Equal(Environment.ProcessId, options!.ParentProcessId);

            Assert.False(TtsHostOptions.TryParse(Array.Empty<string>(), out _));
            Assert.False(TtsHostOptions.TryParse(new[] { "--pipe" }, out _));
            Assert.False(TtsHostOptions.TryParse(new[] { "--pipe", CreatePipeName() }, out _));
            Assert.False(TtsHostOptions.TryParse(
                new[] { "--pipe", CreatePipeName(), "--parent-process-id" },
                out _));
            Assert.False(TtsHostOptions.TryParse(
                new[] { "--pipe", CreatePipeName(), "--parent-process-id", "0" },
                out _));
            Assert.False(TtsHostOptions.TryParse(
                new[] { "--pipe", CreatePipeName(), "--parent-process-id", "-1" },
                out _));
            Assert.False(TtsHostOptions.TryParse(
                new[] { "--pipe", CreatePipeName(), "--parent-process-id", "not-a-number" },
                out _));
            Assert.False(TtsHostOptions.TryParse(
                new[] { "--pipe", CreatePipeName(), "--parent-process-id", parentProcessId, "extra" },
                out _));
            Assert.False(TtsHostOptions.TryParse(new[] { "--other", CreatePipeName() }, out _));
        }

        private static string CreatePipeName()
        {
            return $"Dictation.Tests.Tts.{Guid.NewGuid():N}";
        }

        private static byte[] CreateRequestFrame(
            byte version,
            TtsPipeMessageType type,
            byte language,
            int payloadLength,
            byte[]? payload = null)
        {
            return CreateRequestFrame(version, (byte)type, language, payloadLength, payload);
        }

        private static byte[] CreateRequestFrame(
            byte version,
            byte type,
            byte language,
            int payloadLength,
            byte[]? payload = null)
        {
            var frame = new byte[7 + (payload?.Length ?? 0)];
            frame[0] = version;
            frame[1] = type;
            frame[2] = language;
            BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(3), payloadLength);
            payload?.CopyTo(frame, 7);
            return frame;
        }

        private static byte[] CreateResponseFrame(
            byte version,
            TtsPipeMessageType type,
            int payloadLength,
            byte[]? payload = null)
        {
            var frame = new byte[6 + (payload?.Length ?? 0)];
            frame[0] = version;
            frame[1] = (byte)type;
            BinaryPrimitives.WriteInt32LittleEndian(frame.AsSpan(2), payloadLength);
            payload?.CopyTo(frame, 6);
            return frame;
        }

        private sealed class RecordingEngine : ITtsSynthesisEngine
        {
            public RecordingEngine(TtsLanguage language, byte[] audio)
            {
                SupportedLanguages = new[] { language };
                Audio = audio;
            }

            public IReadOnlyCollection<TtsLanguage> SupportedLanguages { get; }

            public byte[] Audio { get; }

            public Exception? Failure { get; set; }

            public TtsSynthesisRequestDto? LastRequest { get; private set; }

            public Task<byte[]> SynthesizeAsync(
                TtsSynthesisRequestDto request,
                CancellationToken cancellationToken)
            {
                LastRequest = request;
                if (Failure is not null)
                {
                    return Task.FromException<byte[]>(Failure);
                }

                return Task.FromResult(Audio);
            }
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

            public Task Completion => running;

            public static async Task<RunningServer> StartAsync(params ITtsSynthesisEngine[] engines)
            {
                var session = new TtsHostSession(CreatePipeName());
                var server = new TtsPipeServer(session, engines);
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
