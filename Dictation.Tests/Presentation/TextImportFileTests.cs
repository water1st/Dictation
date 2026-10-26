using System.Text;
using System.Windows;
using Dictation.Core;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Tts;
using Dictation.DataAccess;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation;
using Dictation.Presentation.Importing;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证文件、拖放和启动参数都只准备同一页面，不绕过义项与收件箱确认。</summary>
    public sealed class TextImportFileTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task Reader_Utf8WithOrWithoutBomPreservesSourceText(bool bom)
        {
            using var directory = new TemporaryDirectory();
            var path = Path.Combine(directory.Path, "词表.TXT");
            var encoding = new UTF8Encoding(bom, true);
            await File.WriteAllTextAsync(path, "食べます\ngo\n", encoding, TestContext.Current.CancellationToken);

            var text = await new Utf8TextFileReader().ReadAsync(path, TestContext.Current.CancellationToken);

            Assert.Equal("食べます\ngo\n", text);
        }

        [Fact]
        public async Task Reader_EmptyFileReturnsEmptyText()
        {
            using var directory = new TemporaryDirectory();
            var path = Path.Combine(directory.Path, "empty.txt");
            await File.WriteAllBytesAsync(path, Array.Empty<byte>(), TestContext.Current.CancellationToken);
            Assert.Equal(string.Empty, await new Utf8TextFileReader().ReadAsync(path, TestContext.Current.CancellationToken));
        }

        [Theory]
        [InlineData(new byte[] { 0xc3, 0x28 })]
        [InlineData(new byte[] { 0xff, 0xfe, 0x67, 0x98 })]
        public async Task Reader_InvalidUtf8AndUtf16DoNotProduceReplacementCharacters(byte[] bytes)
        {
            using var directory = new TemporaryDirectory();
            var path = Path.Combine(directory.Path, "invalid.txt");
            await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<DecoderFallbackException>(() =>
                new Utf8TextFileReader().ReadAsync(path, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Reader_RejectsNonTextMissingAndCanceledFiles()
        {
            using var directory = new TemporaryDirectory();
            var reader = new Utf8TextFileReader();
            await Assert.ThrowsAsync<ArgumentException>(() => reader.ReadAsync(
                Path.Combine(directory.Path, "words.pdf"), TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<FileNotFoundException>(() => reader.ReadAsync(
                Path.Combine(directory.Path, "missing.txt"), TestContext.Current.CancellationToken));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(
                Path.Combine(directory.Path, "missing.txt"), cancellation.Token));
        }

        [Fact]
        public void Drop_AcceptsExactlyOneTextFileAndRejectsOtherPayloads()
        {
            Assert.Equal("words.TXT", TextImportPage.GetDroppedTextFile(
                new DataObject(DataFormats.FileDrop, new[] { "words.TXT" })));
            Assert.Null(TextImportPage.GetDroppedTextFile(new DataObject(DataFormats.FileDrop, new[] { "words.pdf" })));
            Assert.Null(TextImportPage.GetDroppedTextFile(new DataObject(DataFormats.FileDrop, new[] { "one.txt", "two.txt" })));
            Assert.Null(TextImportPage.GetDroppedTextFile(new DataObject(DataFormats.Text, "go")));
        }

        [Fact]
        public async Task FileNavigation_LoadsCandidatesInCurrentLanguageWithoutWritingInbox()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            var path = Path.Combine(directory.Path, "words.txt");
            await File.WriteAllTextAsync(path, "食前\n食べます\n未知测试词", TestContext.Current.CancellationToken);
            var navigation = await InitializeHomeAsync(provider);

            await navigation.OpenTextImportAsync(LearningLanguage.Japanese, path, TestContext.Current.CancellationToken);

            var vm = Assert.IsType<TextImportViewModel>(navigation.CurrentPage);
            Assert.Equal(3, vm.Lines.Count);
            Assert.Equal(1, vm.SelectedCount);
            Assert.Null(vm.ErrorMessage);
            Assert.Empty(await provider.GetRequiredService<IImportInboxQuery>().GetPendingBatchesAsync(
                LearningLanguage.Japanese, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task FileLoad_ErrorClearsStaleChoicesAndShowsErrorOnSamePage()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            var navigation = await InitializeHomeAsync(provider);
            await navigation.OpenTextImportAsync(LearningLanguage.English, cancellationToken: TestContext.Current.CancellationToken);
            navigation.CompleteTransition();
            var vm = Assert.IsType<TextImportViewModel>(navigation.CurrentPage);
            await vm.LoadTextAsync("convert", TestContext.Current.CancellationToken);

            await vm.LoadFileAsync(Path.Combine(directory.Path, "missing.txt"), TestContext.Current.CancellationToken);

            Assert.Same(vm, navigation.CurrentPage);
            Assert.Empty(vm.Lines);
            Assert.NotNull(vm.ErrorMessage);
            Assert.False(vm.IsBusy);
            Assert.False(vm.CanStage);
        }

        [Fact]
        public async Task Startup_OnlyExplicitSingleTextArgumentOpensConfirmationPage()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            var navigation = await InitializeHomeAsync(provider);
            var home = navigation.CurrentPage;
            var startup = provider.GetRequiredService<StartupTextImport>();
            await startup.OpenAsync(Array.Empty<string>(), LearningLanguage.English, TestContext.Current.CancellationToken);
            await startup.OpenAsync(new[] { "--flag" }, LearningLanguage.English, TestContext.Current.CancellationToken);
            await startup.OpenAsync(new[] { "one.txt", "two.txt" }, LearningLanguage.English, TestContext.Current.CancellationToken);
            Assert.Same(home, navigation.CurrentPage);
            var path = Path.Combine(directory.Path, "English words.txt");
            await File.WriteAllTextAsync(path, "convert", TestContext.Current.CancellationToken);

            await startup.OpenAsync(new[] { path }, LearningLanguage.English, TestContext.Current.CancellationToken);

            var vm = Assert.IsType<TextImportViewModel>(navigation.CurrentPage);
            Assert.Equal(LearningLanguage.English, vm.SelectedLanguage);
            Assert.Equal("convert", Assert.Single(vm.Lines).SourceText);
            Assert.Empty(await provider.GetRequiredService<IImportInboxQuery>().GetPendingBatchesAsync(
                LearningLanguage.English, TestContext.Current.CancellationToken));
        }

        private static async Task<NavigationService> InitializeHomeAsync(ServiceProvider provider)
        {
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(TestContext.Current.CancellationToken);
            var navigation = provider.GetRequiredService<NavigationService>();
            navigation.InitializeHome(provider.GetRequiredService<LandingViewModel>());
            return navigation;
        }

        private static async Task<ServiceProvider> CreateProviderAsync(string root)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddDictationDataAccess(root).AddDictationCore();
            services.AddSingleton<ITtsProvider>(new LearningPresentationTestEnvironment.ControlledTtsProvider());
            services.AddDictationPresentation();
            var provider = services.BuildServiceProvider();
            var paths = provider.GetRequiredService<ApplicationPaths>();
            paths.EnsureRuntimeDirectories();
            LexiconTestDatabases.CreateBoth(paths);
            await provider.GetRequiredService<SchemaMigrationRunner>().MigrateAsync(TestContext.Current.CancellationToken);
            return provider;
        }
    }
}
