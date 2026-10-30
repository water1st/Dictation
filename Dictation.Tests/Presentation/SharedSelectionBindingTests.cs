using System.Windows.Controls;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Presentation.Theme;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>用真实业务与 SQLite 检验可选集合变化会同步到页面全选状态。</summary>
    public sealed class SharedSelectionBindingTests
    {
        [Fact]
        public Task ReviewSelection_RangeAndLibraryChanges_UpdateEnabledAndMixedState()
        {
            return ReviewPlanPageTests.RunStaAsync(async () =>
            {
                using var directory = new TemporaryDirectory();
                using var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path);
                using var scope = provider.CreateScope();
                var vm = scope.ServiceProvider.GetRequiredService<ReviewPlanViewModel>();
                vm.Initialize(LearningLanguage.Japanese);
                await vm.RefreshAsync(TestContext.Current.CancellationToken);
                var page = new ReviewPlanPage { DataContext = vm };
                var host = FormControlThemeTests.CreateHost(page);
                ThemePalette.Apply(host.Resources, ResolvedTheme.Light);
                FormControlThemeTests.Layout(host);
                var checkbox = (CheckBox)page.FindName("SelectAllGroups");
                vm.IsSelectedRange = true;
                FormControlThemeTests.Layout(host);
                Assert.False(checkbox.IsEnabled);
                await ReviewPlanPresentationTestHost.ImportAsync(provider);
                await ReviewPlanPresentationTestHost.ImportAsync(provider, "食前", "jmdict:2:1");
                await vm.RefreshAsync(TestContext.Current.CancellationToken);
                vm.IsSelectedRange = true;
                FormControlThemeTests.Layout(host);
                Assert.True(checkbox.IsEnabled);
                vm.IsAllSelected = true;
                FormControlThemeTests.Layout(host);
                Assert.True(checkbox.IsChecked);
                vm.Groups[0].IsSelected = false;
                FormControlThemeTests.Layout(host);
                Assert.Null(checkbox.IsChecked);
                vm.IsAllWords = true;
                FormControlThemeTests.Layout(host);
                Assert.False(checkbox.IsEnabled);
                vm.IsSelectedRange = true;
                FormControlThemeTests.Layout(host);
                Assert.True(checkbox.IsEnabled);
            });
        }

        [Fact]
        public Task InboxSelection_PendingItemsThenProcessedBatch_UpdateAvailability()
        {
            return ReviewPlanPageTests.RunStaAsync(async () =>
            {
                using var directory = new TemporaryDirectory();
                using var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path);
                using var scope = provider.CreateScope();
                var vm = scope.ServiceProvider.GetRequiredService<ImportInboxListViewModel>();
                vm.Initialize(LearningLanguage.Japanese);
                await vm.RefreshAsync(TestContext.Current.CancellationToken);
                var page = new ImportInboxListPage { DataContext = vm };
                var host = FormControlThemeTests.CreateHost(page);
                ThemePalette.Apply(host.Resources, ResolvedTheme.Dark);
                FormControlThemeTests.Layout(host);
                var checkbox = Assert.Single(FormControlThemeTests.Children<CheckBox>(page), item => Equals(item.Content, "全选"));
                Assert.False(checkbox.IsEnabled);
                var staged = await provider.GetRequiredService<IImportStaging>().StageAsync(new StageImportBatchRequestDto(
                    LearningLanguage.Japanese, ImportSourceKind.Manual, new[] { new ImportSelectionDto("食べる", "jmdict:1:1") }), TestContext.Current.CancellationToken);
                await vm.RefreshAsync(TestContext.Current.CancellationToken);
                FormControlThemeTests.Layout(host);
                Assert.True(checkbox.IsEnabled);
                vm.IsAllSelected = true;
                FormControlThemeTests.Layout(host);
                Assert.True(checkbox.IsChecked);
                await provider.GetRequiredService<IImportInboxActions>().DecideAsync(new DecideImportBatchRequestDto(
                    staged.Batch.BatchId, staged.Batch.Items.Select(item => item.ItemId).ToArray(), ImportDecision.Learning), TestContext.Current.CancellationToken);
                await vm.RefreshAsync(TestContext.Current.CancellationToken);
                FormControlThemeTests.Layout(host);
                Assert.False(checkbox.IsEnabled);
            });
        }

        [Fact]
        public Task TextSelection_UnmatchedAndConfirmedCandidates_UpdateAvailability()
        {
            return ReviewPlanPageTests.RunStaAsync(async () =>
            {
                using var directory = new TemporaryDirectory();
                using var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path);
                using var scope = provider.CreateScope();
                var vm = scope.ServiceProvider.GetRequiredService<TextImportViewModel>();
                vm.Initialize(LearningLanguage.Japanese);
                var page = new TextImportPage { DataContext = vm };
                var host = FormControlThemeTests.CreateHost(page);
                ThemePalette.Apply(host.Resources, ResolvedTheme.Light);
                FormControlThemeTests.Layout(host);
                var checkbox = Assert.Single(FormControlThemeTests.Children<CheckBox>(page), item => Equals(item.Content, "全选已确认义项"));
                Assert.False(checkbox.IsEnabled);
                await vm.LoadTextAsync("不存在的词条", TestContext.Current.CancellationToken);
                FormControlThemeTests.Layout(host);
                Assert.False(checkbox.IsEnabled);
                await vm.LoadTextAsync("食べる\n食前", TestContext.Current.CancellationToken);
                Assert.Equal(2, vm.Lines.Count);
                foreach (var line in vm.Lines) line.SelectedCandidate = line.Candidates[0];
                FormControlThemeTests.Layout(host);
                Assert.True(checkbox.IsEnabled);
                vm.AreAllSelected = true;
                vm.Lines[0].IsSelected = false;
                FormControlThemeTests.Layout(host);
                Assert.Null(checkbox.IsChecked);
                vm.SourceText = "新的输入";
                FormControlThemeTests.Layout(host);
                Assert.False(checkbox.IsEnabled);
            });
        }
    }
}
