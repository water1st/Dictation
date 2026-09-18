using Dictation.Core.Tts;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Core
{
    /// <summary>
    /// 验证合成请求校验、Provider调用及返回WAV数据完整性。
    /// </summary>
    public sealed class TtsServiceTests
    {
        [Fact]
        public async Task SynthesizeAsync_ValidJapaneseText_PreservesRequestAndReturnsWav()
        {
            var provider = new FakeTtsProvider();
            var expectedAudio = ValidWav();
            provider.Handler = (_, _) => Task.FromResult(new TtsAudioDto(expectedAudio));
            var service = new TtsService(provider);
            var request = new TtsSynthesisRequestDto(TtsLanguage.Japanese, "  食べます、。  ");

            var result = await service.SynthesizeAsync(request, TestContext.Current.CancellationToken);

            Assert.Same(request, provider.LastRequest);
            Assert.Equal(request.Text, provider.LastRequest!.Text);
            Assert.Equal(expectedAudio, result.WavData);
            Assert.Equal(1, provider.CallCount);
        }

        [Fact]
        public async Task SynthesizeAsync_ValidEnglishText_PassesEnglishLanguageToProvider()
        {
            var provider = new FakeTtsProvider();
            provider.Handler = (_, _) => Task.FromResult(new TtsAudioDto(ValidWav()));
            var service = new TtsService(provider);
            var request = new TtsSynthesisRequestDto(TtsLanguage.English, "goes");

            await service.SynthesizeAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(TtsLanguage.English, provider.LastRequest!.Language);
            Assert.Equal("goes", provider.LastRequest.Text);
        }

        [Fact]
        public async Task SynthesizeAsync_NullRequest_ThrowsAndDoesNotCallProvider()
        {
            var provider = new FakeTtsProvider();
            var service = new TtsService(provider);

            await Assert.ThrowsAsync<ArgumentNullException>(() => service.SynthesizeAsync(
                null!, TestContext.Current.CancellationToken));

            Assert.Equal(0, provider.CallCount);
        }

        [Fact]
        public async Task SynthesizeAsync_NullOrWhitespaceText_ThrowsAndDoesNotCallProvider()
        {
            var provider = new FakeTtsProvider();
            var service = new TtsService(provider);

            await Assert.ThrowsAsync<ArgumentException>(() => service.SynthesizeAsync(
                new TtsSynthesisRequestDto(TtsLanguage.Japanese, null!),
                TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(() => service.SynthesizeAsync(
                new TtsSynthesisRequestDto(TtsLanguage.Japanese, " \t\r\n "),
                TestContext.Current.CancellationToken));

            Assert.Equal(0, provider.CallCount);
        }

        [Fact]
        public async Task SynthesizeAsync_UndefinedLanguage_ThrowsAndDoesNotCallProvider()
        {
            var provider = new FakeTtsProvider();
            var service = new TtsService(provider);

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.SynthesizeAsync(
                new TtsSynthesisRequestDto((TtsLanguage)99, "text"),
                TestContext.Current.CancellationToken));

            Assert.Equal(0, provider.CallCount);
        }

        [Fact]
        public async Task SynthesizeAsync_TextLongerThan500Characters_ThrowsAndDoesNotCallProvider()
        {
            var provider = new FakeTtsProvider();
            var service = new TtsService(provider);

            await Assert.ThrowsAsync<ArgumentException>(() => service.SynthesizeAsync(
                new TtsSynthesisRequestDto(TtsLanguage.English, new string('a', 501)),
                TestContext.Current.CancellationToken));

            Assert.Equal(0, provider.CallCount);
        }

        [Fact]
        public async Task SynthesizeAsync_PreCanceledToken_DoesNotCallProvider()
        {
            var provider = new FakeTtsProvider();
            var service = new TtsService(provider);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => service.SynthesizeAsync(
                new TtsSynthesisRequestDto(TtsLanguage.Japanese, "食べる"), cancellation.Token));

            Assert.Equal(0, provider.CallCount);
        }

        [Fact]
        public async Task SynthesizeAsync_ProviderCancellation_IsPropagated()
        {
            var provider = new FakeTtsProvider();
            var expected = new OperationCanceledException("外部 TTS 已取消");
            provider.Handler = (_, _) => Task.FromException<TtsAudioDto>(expected);
            var service = new TtsService(provider);

            var actual = await Assert.ThrowsAsync<OperationCanceledException>(() => service.SynthesizeAsync(
                new TtsSynthesisRequestDto(TtsLanguage.English, "go"),
                TestContext.Current.CancellationToken));

            Assert.Same(expected, actual);
        }

        [Fact]
        public async Task SynthesizeAsync_ProviderFailure_IsWrappedWithChineseTtsExceptionAndInnerException()
        {
            var provider = new FakeTtsProvider();
            var expected = new InvalidOperationException("provider failed");
            provider.Handler = (_, _) => Task.FromException<TtsAudioDto>(expected);
            var service = new TtsService(provider);

            var actual = await Assert.ThrowsAsync<TtsSynthesisException>(() => service.SynthesizeAsync(
                new TtsSynthesisRequestDto(TtsLanguage.Japanese, "食べる"),
                TestContext.Current.CancellationToken));

            Assert.Contains("TTS", actual.Message);
            Assert.Contains("失败", actual.Message);
            Assert.Same(expected, actual.InnerException);
        }

        [Fact]
        public async Task SynthesizeAsync_NullProviderAudio_ThrowsInvalidWavException()
        {
            var provider = new FakeTtsProvider
            {
                Handler = (_, _) => Task.FromResult<TtsAudioDto>(null!)
            };
            var service = new TtsService(provider);

            var exception = await Assert.ThrowsAsync<TtsSynthesisException>(() => service.SynthesizeAsync(
                new TtsSynthesisRequestDto(TtsLanguage.Japanese, "食べる"),
                TestContext.Current.CancellationToken));

            Assert.Contains("WAV", exception.Message);
        }

        [Fact]
        public async Task SynthesizeAsync_TooShortProviderAudio_ThrowsInvalidWavException()
        {
            var provider = new FakeTtsProvider
            {
                Handler = (_, _) => Task.FromResult(new TtsAudioDto(new byte[11]))
            };
            var service = new TtsService(provider);

            var exception = await Assert.ThrowsAsync<TtsSynthesisException>(() => service.SynthesizeAsync(
                new TtsSynthesisRequestDto(TtsLanguage.English, "go"),
                TestContext.Current.CancellationToken));

            Assert.Contains("WAV", exception.Message);
        }

        [Fact]
        public async Task SynthesizeAsync_InvalidWavHeader_ThrowsInvalidWavException()
        {
            var provider = new FakeTtsProvider
            {
                Handler = (_, _) => Task.FromResult(new TtsAudioDto(
                    new byte[] { (byte)'N', (byte)'O', (byte)'P', (byte)'E', 0, 0, 0, 0, (byte)'N', (byte)'O', (byte)'P', (byte)'E' }))
            };
            var service = new TtsService(provider);

            var exception = await Assert.ThrowsAsync<TtsSynthesisException>(() => service.SynthesizeAsync(
                new TtsSynthesisRequestDto(TtsLanguage.English, "go"),
                TestContext.Current.CancellationToken));

            Assert.Contains("WAV", exception.Message);
        }

        [Fact]
        public void Constructor_NullProvider_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new TtsService(null!));
        }

        [Fact]
        public void AddDictationCore_RegistersTtsServiceAsSingleton()
        {
            var provider = new FakeTtsProvider();
            using var services = new ServiceCollection()
                .AddSingleton<ITtsProvider>(provider)
                .AddDictationCore()
                .BuildServiceProvider();

            var first = services.GetRequiredService<TtsService>();
            var second = services.GetRequiredService<TtsService>();

            Assert.Same(first, second);
        }

        private static byte[] ValidWav()
        {
            return new byte[]
            {
                (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0,
                (byte)'W', (byte)'A', (byte)'V', (byte)'E'
            };
        }

        private sealed class FakeTtsProvider : ITtsProvider
        {
            public Func<TtsSynthesisRequestDto, CancellationToken, Task<TtsAudioDto>> Handler { get; set; }
                = (_, _) => Task.FromResult(new TtsAudioDto(ValidWav()));

            public int CallCount { get; private set; }

            public TtsSynthesisRequestDto? LastRequest { get; private set; }

            public Task<TtsAudioDto> SynthesizeAsync(
                TtsSynthesisRequestDto request,
                CancellationToken cancellationToken = default)
            {
                CallCount++;
                LastRequest = request;
                return Handler(request, cancellationToken);
            }
        }
    }
}
