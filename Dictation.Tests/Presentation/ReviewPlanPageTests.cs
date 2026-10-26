using Dapper;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Dictation.Core.Configuration;
using Dictation.Core.Learning;
using Dictation.Core.Review;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Shell;
using Dictation.Presentation.Theme;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>用真实业务、SQLite 与 STA 控件检验复习计划页面的完整保存和管理链路。</summary>
    public sealed class ReviewPlanPageTests
    {
        [Fact]
        public async Task Refresh_ShowsChineseGroupsAndApplicableFormsForFixedLanguage()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path);
            await ReviewPlanPresentationTestHost.ImportAsync(provider);
            await ReviewPlanPresentationTestHost.ImportAsync(provider, "食前", "jmdict:2:1");
            using var scope = provider.CreateScope();
            var vm = scope.ServiceProvider.GetRequiredService<ReviewPlanViewModel>();
            vm.Initialize(LearningLanguage.Japanese);
            await vm.RefreshAsync(TestContext.Current.CancellationToken);
            Assert.Equal(20, vm.DailyReviewTarget);
            Assert.Equal(2, vm.Groups.Count);
            Assert.Contains("2 组", vm.StatusMessage);
            var verb = Assert.Single(vm.Groups, item => item.Details.HeadwordLabel == "食べる");
            Assert.Contains("吃", verb.Details.MeaningLabel);
            Assert.Contains("二类", verb.Details.AttributeLabel);
            Assert.Contains(verb.Details.Forms, form => form.FormLabel.Contains("て形"));
            var noun = Assert.Single(vm.Groups, item => item.Details.HeadwordLabel == "食前");
            Assert.Contains("名词", noun.Details.AttributeLabel);
            Assert.Single(noun.Details.Forms);
            Assert.DoesNotContain(noun.Details.Forms, form => form.Value.FormCode == "Te");
            Assert.Equal(new[] { "Dictionary", "Polite", "Te", "Ta" }, vm.FormOptions.Select(option => option.Code));
            Assert.Equal(new[] { "Kana", "Kanji" }, vm.OutputOptions.Select(option => option.Code));
            Assert.Throws<InvalidOperationException>(() => vm.Initialize(LearningLanguage.English));
        }

        [Fact]
        public async Task Save_ReopenAndRestartKeepIndependentPlanAndSelectedScope()
        {
            using var directory = new TemporaryDirectory();
            using (var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path))
            {
                var group = await ReviewPlanPresentationTestHost.ImportAsync(provider);
                using var scope = provider.CreateScope();
                var vm = scope.ServiceProvider.GetRequiredService<ReviewPlanViewModel>();
                vm.Initialize(LearningLanguage.Japanese);
                await vm.RefreshAsync(TestContext.Current.CancellationToken);
                vm.DailyReviewTarget = 45;
                vm.SessionWordLimit = 5;
                vm.RangeMode = ReviewRangeMode.Selected;
                Assert.Single(vm.Groups).IsSelected = true;
                foreach (var option in vm.FormOptions) option.IsSelected = option.Code == "Te";
                foreach (var option in vm.ChannelOptions) option.IsSelected = option.Code == "Dictation";
                foreach (var option in vm.OutputOptions) option.IsSelected = option.Code == "Kanji";
                await vm.SaveAsync(TestContext.Current.CancellationToken);
                Assert.Null(vm.ErrorMessage);
                Assert.Contains("已保存", vm.StatusMessage);
                var saved = await provider.GetRequiredService<IReviewPlanReader>().LoadAsync(LearningLanguage.Japanese, TestContext.Current.CancellationToken);
                Assert.Equal(45, saved.DailyReviewTarget);
                Assert.Equal(new[] { group }, saved.WordGroupIds);
                Assert.Equal(new[] { "Te" }, saved.FormCodes);
                using var reopened = provider.CreateScope();
                var second = reopened.ServiceProvider.GetRequiredService<ReviewPlanViewModel>();
                second.Initialize(LearningLanguage.Japanese);
                await second.RefreshAsync(TestContext.Current.CancellationToken);
                Assert.Equal(45, second.DailyReviewTarget);
                Assert.True(Assert.Single(second.Groups).IsSelected);
            }
            using var restarted = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path, createLexicons: false);
            using var restartedScope = restarted.CreateScope();
            var reloaded = restartedScope.ServiceProvider.GetRequiredService<ReviewPlanViewModel>();
            reloaded.Initialize(LearningLanguage.Japanese);
            await reloaded.RefreshAsync(TestContext.Current.CancellationToken);
            Assert.Equal(45, reloaded.DailyReviewTarget);
            Assert.Equal(5, reloaded.SessionWordLimit);
            Assert.Equal(ReviewRangeMode.Selected, reloaded.RangeMode);
            Assert.Equal("Kanji", Assert.Single(reloaded.OutputOptions, option => option.IsSelected).Code);
            Assert.Equal(20, (await restarted.GetRequiredService<ILearningPlanReader>().LoadAsync(LearningLanguage.Japanese, TestContext.Current.CancellationToken)).DailyNewTarget);
            Assert.Equal(20, (await restarted.GetRequiredService<IReviewPlanReader>().LoadAsync(LearningLanguage.English, TestContext.Current.CancellationToken)).DailyReviewTarget);
        }

        [Fact]
        public async Task Selection_SelectAllPartialAndEmptyNeverDeletesWordsOrOverwritesValidPlan()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path);
            await ReviewPlanPresentationTestHost.ImportAsync(provider);
            await ReviewPlanPresentationTestHost.ImportAsync(provider, "食前", "jmdict:2:1");
            using var scope = provider.CreateScope();
            var vm = scope.ServiceProvider.GetRequiredService<ReviewPlanViewModel>();
            vm.Initialize(LearningLanguage.Japanese);
            await vm.RefreshAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2, vm.Groups.Count);
            vm.RangeMode = ReviewRangeMode.Selected;
            vm.IsAllSelected = true;
            Assert.All(vm.Groups, item => Assert.True(item.IsSelected));
            vm.Groups[0].IsSelected = false;
            Assert.Null(vm.IsAllSelected);
            await vm.SaveAsync(TestContext.Current.CancellationToken);
            Assert.Null(vm.ErrorMessage);
            vm.IsAllSelected = false;
            vm.DailyReviewTarget = 60;
            await vm.SaveAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(vm.ErrorMessage);
            var saved = await provider.GetRequiredService<IReviewPlanReader>().LoadAsync(LearningLanguage.Japanese, TestContext.Current.CancellationToken);
            Assert.Equal(20, saved.DailyReviewTarget);
            Assert.Single(saved.WordGroupIds);
            Assert.Equal(2, (await provider.GetRequiredService<IReviewLibraryQuery>().LoadAsync(LearningLanguage.Japanese, TestContext.Current.CancellationToken)).TotalGroupCount);
            vm.RangeMode = ReviewRangeMode.All;
            await vm.SaveAsync(TestContext.Current.CancellationToken);
            var all = await provider.GetRequiredService<IReviewPlanReader>().LoadAsync(LearningLanguage.Japanese, TestContext.Current.CancellationToken);
            Assert.Empty(all.WordGroupIds);
            Assert.Equal(ReviewRangeMode.All, all.RangeMode);
        }

        [Fact]
        public async Task English_ExampleSearchAndSaveUseRealOfflineParadigm()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path);
            await ReviewPlanPresentationTestHost.ImportAsync(provider, "go", "go%2:01:00::", LearningLanguage.English);
            using var scope = provider.CreateScope();
            var vm = scope.ServiceProvider.GetRequiredService<ReviewPlanViewModel>();
            vm.Initialize(LearningLanguage.English);
            await vm.RefreshAsync(TestContext.Current.CancellationToken);
            Assert.Equal(new[] { "Latin" }, vm.OutputOptions.Select(option => option.Code));
            foreach (var form in vm.FormOptions) form.IsSelected = form.Code is "Base" or "Past" or "PastParticiple";
            vm.ExampleQuery = "go";
            await vm.SearchExamplesAsync(TestContext.Current.CancellationToken);
            vm.SelectedExample = Assert.Single(vm.ExampleCandidates, item => item.SourceSenseId == "go%2:01:00::");
            vm.EnglishFormLabelMode = EnglishFormLabelMode.ExampleParadigm;
            await vm.SaveAsync(TestContext.Current.CancellationToken);
            Assert.Null(vm.ErrorMessage);
            var plan = await provider.GetRequiredService<IReviewPlanReader>().LoadAsync(LearningLanguage.English, TestContext.Current.CancellationToken);
            Assert.Equal(new[] { "go", "went", "gone" }, plan.EnglishExampleParadigm!.Forms.Select(form => form.Surface));
            Assert.Contains("go", vm.ExampleSummary);
            Assert.DoesNotContain(vm.FormOptions, item => item.Code == "Dictionary");
        }

        [Fact]
        public async Task Delete_RequiresOverlayConfirmationAndCascadesMemoryWithoutDiscardingUnsavedOptions()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path);
            var group = await ReviewPlanPresentationTestHost.ImportAsync(provider);
            await using (var connection = provider.GetRequiredService<SqliteConnectionFactory>().CreateConnection())
            {
                await connection.ExecuteAsync("""
                    INSERT INTO ReviewFormMemories (WordGroupId, MemberId, FormCode, Stability, Difficulty, LastReviewedAtUtc, DueAtUtc, ReviewCount, LapseCount)
                    SELECT WordGroupId, MemberId, 'Dictionary', 2, 3, '2026-09-01T00:00:00.0000000+00:00', '2026-09-03T00:00:00.0000000+00:00', 1, 0
                    FROM WordGroupMembers WHERE WordGroupId = @Id;
                    """, new { Id = group });
            }
            using var scope = provider.CreateScope();
            var vm = scope.ServiceProvider.GetRequiredService<ReviewPlanViewModel>();
            vm.Initialize(LearningLanguage.Japanese);
            await vm.RefreshAsync(TestContext.Current.CancellationToken);
            vm.SelectedGroup = Assert.Single(vm.Groups);
            vm.DailyReviewTarget = 33;
            var overlay = provider.GetRequiredService<IOverlayHost>();
            var cancel = vm.DeleteSelectedGroupAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(overlay.CurrentRequest);
            Assert.Contains("记忆", overlay.CurrentRequest.Message);
            overlay.Cancel();
            await cancel;
            Assert.Single(vm.Groups);
            var delete = vm.DeleteSelectedGroupAsync(TestContext.Current.CancellationToken);
            Assert.NotNull(overlay.CurrentRequest);
            overlay.Accept();
            await delete;
            Assert.Null(vm.ErrorMessage);
            Assert.Empty(vm.Groups);
            Assert.Equal(33, vm.DailyReviewTarget);
            await using var verify = provider.GetRequiredService<SqliteConnectionFactory>().CreateConnection();
            Assert.Equal(0L, await verify.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM ReviewFormMemories;"));
            Assert.Equal(0L, await verify.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM WordGroups;"));
        }

        [Fact]
        public Task Page_BindsCommandsSelectionAndThemeWithVerticalScrolling() => RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path);
            await ReviewPlanPresentationTestHost.ImportAsync(provider);
            using var scope = provider.CreateScope();
            var vm = scope.ServiceProvider.GetRequiredService<ReviewPlanViewModel>();
            vm.Initialize(LearningLanguage.Japanese);
            await vm.RefreshAsync(TestContext.Current.CancellationToken);
            vm.RangeMode = ReviewRangeMode.Selected;
            var page = new ReviewPlanPage { DataContext = vm, Width = 640, Height = 600 };
            foreach (var theme in new[] { ResolvedTheme.Light, ResolvedTheme.Dark })
            {
                ThemePalette.Apply(page.Resources, theme);
                page.Measure(new Size(640, 600)); page.Arrange(new Rect(0, 0, 640, 600)); page.UpdateLayout();
                Assert.Empty(Children<DataGrid>(page));
                Assert.Contains(Children<Button>(page), button => ReferenceEquals(button.Command, vm.SaveCommand));
                Assert.Contains(Children<Button>(page), button => ReferenceEquals(button.Command, vm.BackCommand));
                Assert.Contains(Children<Button>(page), button => ReferenceEquals(button.Command, vm.DeleteSelectedCommand));
                Assert.Contains(Children<ScrollViewer>(page), scroll => scroll.ScrollableHeight > 0);
                var selectAll = Assert.IsType<CheckBox>(page.FindName("SelectAllGroups"));
                selectAll.SetCurrentValue(CheckBox.IsCheckedProperty, true);
                Assert.True(Assert.Single(vm.Groups).IsSelected);
                var heading = Assert.Single(Children<TextBlock>(page), text => text.Text == "食べる");
                var expected = theme == ResolvedTheme.Light ? Color.FromRgb(31, 41, 55) : Color.FromRgb(248, 250, 252);
                Assert.Equal(expected, Assert.IsType<SolidColorBrush>(heading.Foreground).Color);
                Assert.DoesNotContain(Children<Button>(page), button => Equals(button.Content, "切换日语") || Equals(button.Content, "切换英语"));
            }
        });

        [Fact]
        public Task Home_OpensCurrentLanguagePlanInSameNavigationAndDisposesOnReturn() => RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await WorkspaceSettingsNavigationTests.CreateProviderAsync(directory.Path);
            var home = provider.GetRequiredService<HomeViewModel>();
            var navigation = provider.GetRequiredService<NavigationService>();
            await home.SelectLanguageAsync(LearningLanguage.English, TestContext.Current.CancellationToken);
            navigation.InitializeWorkspace(home);
            await navigation.OpenSettingsAsync(TestContext.Current.CancellationToken);
            navigation.CompleteTransition();
            var settings = Assert.IsType<SettingsViewModel>(navigation.CurrentPage);
            await settings.SelectSectionAsync(SettingsSection.LearningReview, TestContext.Current.CancellationToken);
            var page = new SettingsPage { DataContext = settings, Width = 900, Height = 1000 };
            page.Measure(new Size(900, 1000)); page.Arrange(new Rect(0, 0, 900, 1000)); page.UpdateLayout();
            Assert.Contains(Children<Button>(page), button => ReferenceEquals(button.Command, settings.OpenReviewPlanCommand));
            await home.OpenReviewPlanAsync(TestContext.Current.CancellationToken);
            var vm = Assert.IsType<ReviewPlanViewModel>(navigation.CurrentPage);
            Assert.Equal(LearningLanguage.English, vm.SelectedLanguage);
            Assert.Equal("英语复习计划", navigation.PageTitle);
            navigation.CompleteTransition();
            await navigation.GoBackAsync(TestContext.Current.CancellationToken);
            navigation.CompleteTransition();
            Assert.Same(settings, navigation.CurrentPage);
            await Assert.ThrowsAsync<ObjectDisposedException>(() => vm.RefreshAsync(TestContext.Current.CancellationToken));
        });

        private static IEnumerable<T> Children<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T item) yield return item;
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
                foreach (var child in Children<T>(VisualTreeHelper.GetChild(root, index))) yield return child;
        }

        internal static Task RunStaAsync(Func<Task> action)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() =>
            {
                var dispatcher = Dispatcher.CurrentDispatcher;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
                dispatcher.BeginInvoke(new Action(async () =>
                {
                    try { await action(); completion.SetResult(); }
                    catch (Exception exception) { completion.SetException(exception); }
                    finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Send); }
                }));
                Dispatcher.Run();
            });
            thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
            return completion.Task.WaitAsync(TimeSpan.FromSeconds(20), TestContext.Current.CancellationToken);
        }
    }
}
