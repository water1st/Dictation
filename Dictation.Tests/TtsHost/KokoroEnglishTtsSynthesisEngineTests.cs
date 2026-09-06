using Dictation.Core.Tts;
using Dictation.Tests.Infrastructure;
using Dictation.TtsHost;

namespace Dictation.Tests.TtsHost
{
    public sealed class KokoroEnglishTtsSynthesisEngineTests
    {
        [Fact]
        public void Constructor_DoesNotLoadResourcesAndSupportsEnglishOnly()
        {
            using var directory = new TemporaryDirectory();

            using var engine = new KokoroEnglishTtsSynthesisEngine(directory.Path);

            Assert.Equal(new[] { TtsLanguage.English }, engine.SupportedLanguages);
        }

        [Fact]
        public void InternalConstructor_RejectsNullOrWhiteSpaceResourceDirectory()
        {
            Assert.Throws<ArgumentNullException>(
                () => new KokoroEnglishTtsSynthesisEngine(null!));
            Assert.Throws<ArgumentException>(
                () => new KokoroEnglishTtsSynthesisEngine(string.Empty));
            Assert.Throws<ArgumentException>(
                () => new KokoroEnglishTtsSynthesisEngine(" \t"));
        }

        [Fact]
        public async Task SynthesizeAsync_RejectsNullRequestBeforeLoadingResources()
        {
            using var engine = new KokoroEnglishTtsSynthesisEngine();

            await Assert.ThrowsAsync<ArgumentNullException>(() => engine.SynthesizeAsync(
                null!,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task SynthesizeAsync_RejectsNonEnglishBeforeLoadingResources()
        {
            using var directory = new TemporaryDirectory();
            using var engine = new KokoroEnglishTtsSynthesisEngine(directory.Path);

            var exception = await Assert.ThrowsAsync<ArgumentException>(() => engine.SynthesizeAsync(
                new TtsSynthesisRequestDto(TtsLanguage.Japanese, "食べる"),
                TestContext.Current.CancellationToken));

            Assert.Contains("英语", exception.Message);
        }

        [Fact]
        public async Task SynthesizeAsync_RejectsBlankEnglishTextBeforeLoadingResources()
        {
            using var directory = new TemporaryDirectory();
            using var engine = new KokoroEnglishTtsSynthesisEngine(directory.Path);

            var exception = await Assert.ThrowsAsync<ArgumentException>(() => engine.SynthesizeAsync(
                new TtsSynthesisRequestDto(TtsLanguage.English, " \t\r\n "),
                TestContext.Current.CancellationToken));

            Assert.Contains("不能为空", exception.Message);
        }

        [Fact]
        public async Task SynthesizeAsync_WhenAlreadyCancelledDoesNotLoadResources()
        {
            using var directory = new TemporaryDirectory();
            using var engine = new KokoroEnglishTtsSynthesisEngine(directory.Path);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => engine.SynthesizeAsync(
                new TtsSynthesisRequestDto(TtsLanguage.English, "go"),
                cancellation.Token));
        }

        [Fact]
        public async Task SynthesizeAsync_WhenModelIsMissingReturnsConcreteChinesePath()
        {
            using var directory = new TemporaryDirectory();
            using var engine = new KokoroEnglishTtsSynthesisEngine(directory.Path);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.SynthesizeAsync(
                new TtsSynthesisRequestDto(TtsLanguage.English, "go"),
                TestContext.Current.CancellationToken));

            Assert.Contains("英语 Kokoro 模型文件不存在", exception.Message);
            Assert.Contains(Path.Combine(directory.Path, "model.onnx"), exception.Message);
        }

        [Fact]
        public async Task SynthesizeAsync_WhenVoicesIsMissingReturnsConcreteChinesePath()
        {
            using var directory = CreateResourceDirectoryWithout("voices.bin");
            using var engine = new KokoroEnglishTtsSynthesisEngine(directory.Path);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.SynthesizeAsync(
                new TtsSynthesisRequestDto(TtsLanguage.English, "go"),
                TestContext.Current.CancellationToken));

            Assert.Contains("英语 Kokoro voices 文件不存在", exception.Message);
            Assert.Contains(Path.Combine(directory.Path, "voices.bin"), exception.Message);
        }

        [Fact]
        public async Task SynthesizeAsync_WhenTokensIsMissingReturnsConcreteChinesePath()
        {
            using var directory = CreateResourceDirectoryWithout("tokens.txt");
            using var engine = new KokoroEnglishTtsSynthesisEngine(directory.Path);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.SynthesizeAsync(
                new TtsSynthesisRequestDto(TtsLanguage.English, "go"),
                TestContext.Current.CancellationToken));

            Assert.Contains("英语 Kokoro tokens 文件不存在", exception.Message);
            Assert.Contains(Path.Combine(directory.Path, "tokens.txt"), exception.Message);
        }

        [Fact]
        public async Task SynthesizeAsync_WhenBritishLexiconIsMissingReturnsConcreteChinesePath()
        {
            using var directory = CreateResourceDirectoryWithout("lexicon-gb-en.txt");
            using var engine = new KokoroEnglishTtsSynthesisEngine(directory.Path);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.SynthesizeAsync(
                new TtsSynthesisRequestDto(TtsLanguage.English, "go"),
                TestContext.Current.CancellationToken));

            Assert.Contains("英语 Kokoro 英式词典文件不存在", exception.Message);
            Assert.Contains(Path.Combine(directory.Path, "lexicon-gb-en.txt"), exception.Message);
        }

        [Fact]
        public async Task SynthesizeAsync_WhenEspeakDataIsMissingReturnsConcreteChinesePath()
        {
            using var directory = CreateResourceDirectoryWithout("espeak-ng-data");
            using var engine = new KokoroEnglishTtsSynthesisEngine(directory.Path);

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => engine.SynthesizeAsync(
                new TtsSynthesisRequestDto(TtsLanguage.English, "go"),
                TestContext.Current.CancellationToken));

            Assert.Contains("英语 Kokoro eSpeak 数据目录不存在", exception.Message);
            Assert.Contains(Path.Combine(directory.Path, "espeak-ng-data"), exception.Message);
        }

        [Fact]
        public async Task SynthesizeAsync_WhenDisposedThrowsObjectDisposedException()
        {
            using var directory = new TemporaryDirectory();
            var engine = new KokoroEnglishTtsSynthesisEngine(directory.Path);
            engine.Dispose();

            await Assert.ThrowsAsync<ObjectDisposedException>(() => engine.SynthesizeAsync(
                new TtsSynthesisRequestDto(TtsLanguage.English, "go"),
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public void Dispose_IsIdempotentWithoutLoadingResources()
        {
            using var directory = new TemporaryDirectory();
            var engine = new KokoroEnglishTtsSynthesisEngine(directory.Path);

            engine.Dispose();
            engine.Dispose();
        }

        private static TemporaryDirectory CreateResourceDirectoryWithout(string missingName)
        {
            var directory = new TemporaryDirectory();
            foreach (var fileName in new[] { "model.onnx", "voices.bin", "tokens.txt", "lexicon-gb-en.txt" })
            {
                if (!string.Equals(fileName, missingName, StringComparison.Ordinal))
                {
                    File.WriteAllBytes(Path.Combine(directory.Path, fileName), Array.Empty<byte>());
                }
            }

            if (!string.Equals("espeak-ng-data", missingName, StringComparison.Ordinal))
            {
                Directory.CreateDirectory(Path.Combine(directory.Path, "espeak-ng-data"));
            }

            return directory;
        }
    }
}
