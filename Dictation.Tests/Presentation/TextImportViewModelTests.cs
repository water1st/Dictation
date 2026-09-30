using Dictation.Core;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Tts;
using Dictation.DataAccess;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.ViewModels;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>
    /// 用真实词库和用户数据库验证文本候选确认不会绕过统一收件箱。
    /// </summary>
    public sealed class TextImportViewModelTests
    {
        [Fact]
        public async Task Load_MultipleSensesRequireSelectionAndUnknownLinesRemainVisible()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            var vm = CreateViewModel(provider);

            await vm.LoadTextAsync("食べます\n未知测试词", TestContext.Current.CancellationToken);

            Assert.Equal(2, vm.Lines.Count);
            Assert.Equal(2, vm.Lines[0].Candidates.Count);
            Assert.Null(vm.Lines[0].SelectedCandidate);
            Assert.False(vm.Lines[0].IsSelected);
            Assert.Empty(vm.Lines[1].Candidates);
            Assert.Contains("未匹配", vm.Lines[1].MatchLabel);
            Assert.False(vm.CanStage);
            Assert.Null(await vm.StageSelectedAsync(TestContext.Current.CancellationToken));
            Assert.Empty(await PendingAsync(provider, LearningLanguage.Japanese));
        }

        [Fact]
        public async Task Stage_ExplicitSenseCreatesOnlyThatPendingWholeGroup()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            var vm = CreateViewModel(provider);
            await vm.LoadTextAsync("食べます\n未知测试词", TestContext.Current.CancellationToken);
            vm.Lines[0].SelectedCandidate = vm.Lines[0].Candidates.Single(x => x.SourceSenseId == "jmdict:1:2");

            var result = await vm.StageSelectedAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(result);
            Assert.Equal(1, result.AddedCount);
            Assert.Equal(ImportSourceKind.Text, result.Batch.SourceKind);
            var item = Assert.Single(result.Batch.Items);
            Assert.Equal("jmdict:1:2", item.AnchorSourceSenseId);
            Assert.Equal("食べます", item.SourceText);
            Assert.Equal(ImportItemDecision.Pending, item.Decision);
            Assert.Equal(ImportWordGroupPlacement.Inbox, item.WordGroup!.Placement);
            var member = Assert.Single(item.WordGroup.Members);
            Assert.Contains("用餐", member.Meanings);
            Assert.Contains(member.Forms, form => form.Surface == "たべた");
            Assert.Single(await PendingAsync(provider, LearningLanguage.Japanese));
            Assert.Empty(await PendingAsync(provider, LearningLanguage.English));
            Assert.Contains("新增 1", vm.StatusMessage);
        }

        [Fact]
        public async Task Load_SingleSenseDefaultsSelectedAndDuplicateSourceLinesDoNotInflateBatch()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            var vm = CreateViewModel(provider);
            await vm.LoadTextAsync("食前\n食前\n# 注释\n\n", TestContext.Current.CancellationToken);

            var line = Assert.Single(vm.Lines);
            Assert.True(line.IsSelected);
            Assert.Equal("jmdict:2:1", line.SelectedCandidate!.SourceSenseId);
            Assert.Equal(1, vm.SelectedCount);
            var first = await vm.StageSelectedAsync(TestContext.Current.CancellationToken);
            var repeated = await vm.StageSelectedAsync(TestContext.Current.CancellationToken);

            Assert.Equal(1, first!.AddedCount);
            Assert.Equal(0, repeated!.AddedCount);
            Assert.Equal(1, repeated.DuplicateCount);
            Assert.Contains("重复 1", vm.StatusMessage);
        }

        [Fact]
        public async Task Stage_EnglishInflectionKeepsItsOwnParadigmAndRelatedMember()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            var vm = CreateViewModel(provider, LearningLanguage.English);
            await vm.LoadTextAsync("WENT", TestContext.Current.CancellationToken);
            var line = Assert.Single(vm.Lines);
            line.SelectedCandidate = line.Candidates.Single(x => x.SourceSenseId == "go%2:01:00::");

            var result = await vm.StageSelectedAsync(TestContext.Current.CancellationToken);

            Assert.Equal(LearningLanguage.English, result!.Batch.Language);
            var members = Assert.Single(result.Batch.Items).WordGroup!.Members;
            Assert.Contains(members, x => x.Headword == "go" && x.Forms.Any(f => f.Surface == "went"));
            Assert.Contains(members, x => x.Headword == "convert" && x.Forms.Any(f => f.Surface == "converts"));
            Assert.Empty(await PendingAsync(provider, LearningLanguage.Japanese));
        }

        [Fact]
        public async Task SelectAll_OnlySelectsChosenSensesAndCanDeselectEveryLine()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            var vm = CreateViewModel(provider);
            await vm.LoadTextAsync("食前\n食事\n食べる\n未知测试词", TestContext.Current.CancellationToken);

            vm.AreAllSelected = false;
            Assert.Equal(0, vm.SelectedCount);
            Assert.False(vm.StageSelectedCommand.CanExecute(null));
            vm.AreAllSelected = true;
            Assert.Equal(2, vm.SelectedCount);
            Assert.Null(vm.Lines[2].SelectedCandidate);
            Assert.False(vm.Lines[3].IsSelected);
            Assert.True(vm.StageSelectedCommand.CanExecute(null));
        }

        [Fact]
        public async Task Selection_RejectsCandidateOutsideTheLine()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            var vm = CreateViewModel(provider);
            await vm.LoadTextAsync("食前\n食事", TestContext.Current.CancellationToken);

            Assert.Throws<ArgumentException>(() => vm.Lines[0].SelectedCandidate = vm.Lines[1].Candidates[0]);
            Assert.Equal("jmdict:2:1", vm.Lines[0].SelectedCandidate!.SourceSenseId);
        }

        [Fact]
        public async Task Reload_ReplacesOldChoicesAndEditingInvalidatesPreparedRows()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            var vm = CreateViewModel(provider);
            await vm.LoadTextAsync("食前", TestContext.Current.CancellationToken);
            await vm.LoadTextAsync("食べる", TestContext.Current.CancellationToken);

            Assert.Single(vm.Lines);
            Assert.Null(vm.Lines[0].SelectedCandidate);
            vm.SourceText = "食事";
            Assert.Empty(vm.Lines);
            Assert.False(vm.CanStage);
            Assert.Empty(await PendingAsync(provider, LearningLanguage.Japanese));
        }

        [Fact]
        public async Task Stage_CanceledBeforeWriteLeavesInboxEmpty()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            var vm = CreateViewModel(provider);
            await vm.LoadTextAsync("食前", TestContext.Current.CancellationToken);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => vm.StageSelectedAsync(cancellation.Token));

            Assert.Empty(await PendingAsync(provider, LearningLanguage.Japanese));
            Assert.False(vm.IsBusy);
        }

        [Fact]
        public async Task Load_MissingLexiconShowsErrorAndDoesNotRetainOldChoices()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            var vm = CreateViewModel(provider);
            await vm.LoadTextAsync("食前", TestContext.Current.CancellationToken);
            File.Delete(provider.GetRequiredService<ApplicationPaths>().JapaneseLexiconDatabase);

            await vm.LoadTextAsync("食事", TestContext.Current.CancellationToken);

            Assert.Empty(vm.Lines);
            Assert.False(vm.CanStage);
            Assert.NotNull(vm.ErrorMessage);
            Assert.Empty(await PendingAsync(provider, LearningLanguage.Japanese));
            Assert.False(vm.IsBusy);
        }

        [Fact]
        public async Task Initialize_RequiresValidLanguageAndCannotChangeAfterward()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            var vm = provider.GetRequiredService<TextImportViewModel>();
            await Assert.ThrowsAsync<InvalidOperationException>(() => vm.LoadTextAsync("食前", TestContext.Current.CancellationToken));
            Assert.Throws<ArgumentOutOfRangeException>(() => vm.Initialize((LearningLanguage)99));
            vm.Initialize(LearningLanguage.Japanese);
            Assert.Throws<InvalidOperationException>(() => vm.Initialize(LearningLanguage.English));
            await Assert.ThrowsAsync<ArgumentNullException>(() => vm.LoadTextAsync(null!, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Navigation_HomeEntryAndInboxReturnPreserveLanguageAndChoices()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(directory.Path);
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(TestContext.Current.CancellationToken);
            await home.SelectLanguageAsync(LearningLanguage.English, TestContext.Current.CancellationToken);
            var navigation = provider.GetRequiredService<NavigationService>();
            navigation.InitializeHome(home);
            await home.OpenTextImportAsync(TestContext.Current.CancellationToken);
            var vm = Assert.IsType<TextImportViewModel>(navigation.CurrentPage);
            Assert.Equal(LearningLanguage.English, vm.SelectedLanguage);
            Assert.Equal("英语文本导入", navigation.PageTitle);
            navigation.CompleteTransition();
            await vm.LoadTextAsync("convert", TestContext.Current.CancellationToken);
            await vm.StageSelectedAsync(TestContext.Current.CancellationToken);
            await vm.OpenInboxAsync(TestContext.Current.CancellationToken);
            var inbox = Assert.IsType<ImportInboxListViewModel>(navigation.CurrentPage);
            Assert.Equal(LearningLanguage.English, inbox.SelectedLanguage);
            navigation.CompleteTransition();

            await navigation.GoBackAsync(TestContext.Current.CancellationToken);
            Assert.Same(vm, navigation.CurrentPage);
            Assert.Equal("convert", Assert.Single(vm.Lines).SourceText);
            navigation.CompleteTransition();
            await navigation.GoBackAsync(TestContext.Current.CancellationToken);
            Assert.Same(home, navigation.CurrentPage);
            navigation.CompleteTransition();
            Assert.False(navigation.CanGoBack);
        }

        private static TextImportViewModel CreateViewModel(
            ServiceProvider provider, LearningLanguage language = LearningLanguage.Japanese)
        {
            var vm = provider.GetRequiredService<TextImportViewModel>();
            vm.Initialize(language);
            return vm;
        }

        private static Task<IReadOnlyList<ImportBatchDto>> PendingAsync(
            ServiceProvider provider, LearningLanguage language)
        {
            return provider.GetRequiredService<IImportInboxQuery>().GetPendingBatchesAsync(
                language, TestContext.Current.CancellationToken);
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
