using Dictation.DataAccess.Lexicon;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation.ViewModels;
using Dictation.Services.Configuration;
using Dictation.Services.Lexicon;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.Presentation
{
    public sealed class LexiconLookupViewModelTests
    {
        [Fact]
        public void Initialize_OnlyAcceptsSupportedLanguageAndCannotRepeat()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var viewModel = CreateViewModel(new ApplicationPaths(temporaryDirectory.Path));

            var undefinedLanguage = Assert.Throws<ArgumentOutOfRangeException>(() =>
                viewModel.Initialize((LearningLanguage)99));
            Assert.Contains("语言", undefinedLanguage.Message, StringComparison.Ordinal);

            viewModel.Initialize(LearningLanguage.English);
            var duplicateInitialization = Assert.Throws<InvalidOperationException>(() =>
                viewModel.Initialize(LearningLanguage.Japanese));
            Assert.Contains("初始化", duplicateInitialization.Message, StringComparison.Ordinal);
            Assert.Equal(LearningLanguage.English, viewModel.SelectedLanguage);
            Assert.Equal("英语查词", viewModel.WindowTitle);
        }

        [Fact]
        public async Task SearchAsync_BeforeInitialize_ThrowsClearStateException()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var viewModel = CreateViewModel(new ApplicationPaths(temporaryDirectory.Path));

            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                viewModel.SearchAsync(TestContext.Current.CancellationToken));

            Assert.Contains("尚未初始化", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SearchAsync_JapaneseUsesFixedLanguageAndReturnsFoodEntry()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateBoth(paths);
            var viewModel = CreateViewModel(paths);
            viewModel.Initialize(LearningLanguage.Japanese);
            viewModel.Query = " 食べます ";

            await viewModel.SearchAsync(TestContext.Current.CancellationToken);

            Assert.Equal(LearningLanguage.Japanese, viewModel.SelectedLanguage);
            Assert.Equal("日语查词", viewModel.WindowTitle);
            Assert.Equal(2, viewModel.Results.Count);
            Assert.All(viewModel.Results, result => Assert.Equal("食べる", result.Headword));
        }

        [Fact]
        public async Task SearchAsync_EnglishUsesFixedLanguageAndNormalizesWent()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateBoth(paths);
            var viewModel = CreateViewModel(paths);
            viewModel.Initialize(LearningLanguage.English);
            viewModel.Query = " WENT ";

            await viewModel.SearchAsync(TestContext.Current.CancellationToken);

            Assert.Equal(LearningLanguage.English, viewModel.SelectedLanguage);
            Assert.Equal("英语查词", viewModel.WindowTitle);
            Assert.Equal(2, viewModel.Results.Count);
            Assert.All(viewModel.Results, result => Assert.Equal("go", result.Headword));
        }

        [Fact]
        public async Task SearchAsync_BlankQueryWithMissingDatabaseOnlyShowsPrompt()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var viewModel = CreateViewModel(new ApplicationPaths(temporaryDirectory.Path));
            viewModel.Initialize(LearningLanguage.English);
            viewModel.Query = " \t\r\n ";

            await viewModel.SearchAsync(TestContext.Current.CancellationToken);

            Assert.Empty(viewModel.Results);
            Assert.Null(viewModel.SelectedResult);
            Assert.False(viewModel.IsResultDropDownOpen);
            Assert.Null(viewModel.ErrorMessage);
            Assert.Contains("请输入", viewModel.EmptyStateText, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SearchAsync_SuccessSelectsFirstResultAndOpensDropDown()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateBoth(paths);
            var viewModel = CreateViewModel(paths);
            viewModel.Initialize(LearningLanguage.English);
            viewModel.Query = "went";

            await viewModel.SearchAsync(TestContext.Current.CancellationToken);

            Assert.NotEmpty(viewModel.Results);
            Assert.Same(viewModel.Results[0], viewModel.SelectedResult);
            Assert.True(viewModel.IsResultDropDownOpen);
            Assert.Null(viewModel.ErrorMessage);
            Assert.Equal(string.Empty, viewModel.EmptyStateText);
        }

        [Fact]
        public async Task SearchAsync_NoResultsClearsSelectionAndShowsEmptyState()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateBoth(paths);
            var viewModel = CreateViewModel(paths);
            viewModel.Initialize(LearningLanguage.English);
            viewModel.Query = "went";
            await viewModel.SearchAsync(TestContext.Current.CancellationToken);

            viewModel.Query = "not-in-dictionary";
            await viewModel.SearchAsync(TestContext.Current.CancellationToken);

            Assert.Empty(viewModel.Results);
            Assert.Null(viewModel.SelectedResult);
            Assert.False(viewModel.IsResultDropDownOpen);
            Assert.Contains("未找到", viewModel.EmptyStateText, StringComparison.Ordinal);
        }

        [Fact]
        public async Task SearchAsync_MissingDatabaseShowsChineseErrorAndKeepsWindowUsable()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var viewModel = CreateViewModel(new ApplicationPaths(temporaryDirectory.Path));
            viewModel.Initialize(LearningLanguage.English);
            viewModel.Query = "went";

            await viewModel.SearchAsync(TestContext.Current.CancellationToken);

            Assert.Contains("查词失败：", viewModel.ErrorMessage, StringComparison.Ordinal);
            Assert.Empty(viewModel.Results);
            Assert.Null(viewModel.SelectedResult);
            Assert.False(viewModel.IsResultDropDownOpen);
        }

        [Fact]
        public async Task SearchAsync_PreCanceledTokenPropagatesCancellation()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateBoth(paths);
            var viewModel = CreateViewModel(paths);
            viewModel.Initialize(LearningLanguage.English);
            viewModel.Query = "went";
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                viewModel.SearchAsync(cancellation.Token));
        }

        [Fact]
        public async Task SearchAsync_SecondQueryReplacesFirstResultSet()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);
            LexiconTestDatabases.CreateBoth(paths);
            var viewModel = CreateViewModel(paths);
            viewModel.Initialize(LearningLanguage.English);
            viewModel.Query = "went";
            await viewModel.SearchAsync(TestContext.Current.CancellationToken);

            viewModel.Query = "take off";
            await viewModel.SearchAsync(TestContext.Current.CancellationToken);

            var result = Assert.Single(viewModel.Results);
            Assert.Equal("take off", result.Headword);
            Assert.Equal("take off%v", result.SourceLexemeId);
            Assert.Same(result, viewModel.SelectedResult);
            Assert.True(viewModel.IsResultDropDownOpen);
        }

        private static LexiconLookupViewModel CreateViewModel(ApplicationPaths paths)
        {
            var factory = new LexiconSqliteConnectionFactory(paths);
            var service = new LexiconLookupService(
                new JapaneseLexiconDataAccessor(factory),
                new EnglishLexiconDataAccessor(factory));
            return new LexiconLookupViewModel(service);
        }
    }
}
