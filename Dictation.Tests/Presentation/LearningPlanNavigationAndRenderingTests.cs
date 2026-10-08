using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Dictation.DataAccess;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Tts;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    /// <summary>
    /// 验证学习计划入口按当前语言导航及页面真实WPF控件结构。
    /// </summary>
    public sealed class LearningPlanNavigationAndRenderingTests
    {
        [Fact]
        public async Task HomeCommand_NavigatesToLearningPlanWithCurrentLanguage()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(temporaryDirectory.Path);
            var navigation = provider.GetRequiredService<NavigationService>();
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(TestContext.Current.CancellationToken);
            await home.SelectLanguageAsync(LearningLanguage.English, TestContext.Current.CancellationToken);
            navigation.InitializeHome(home);
            var reachedPage = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            navigation.TransitionRequested += (_, args) => reachedPage.TrySetResult(args.CurrentPage);

            home.OpenLearningPlanCommand.Execute(null);
            var currentPage = await reachedPage.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken);

            var plan = Assert.IsType<LearningPlanViewModel>(currentPage);
            Assert.Same(plan, navigation.CurrentPage);
            Assert.Equal(LearningLanguage.English, plan.SelectedLanguage);
            Assert.Equal("英语学习计划", navigation.PageTitle);
            Assert.False(navigation.CanGoBack);
            navigation.CompleteTransition();
            Assert.True(navigation.CanGoBack);
        }

        [Fact]
        public async Task LearningPlanStart_NavigatesToSessionWithOnlySelectedManualGroup()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(temporaryDirectory.Path);
            var importer = provider.GetRequiredService<ImportInboxService>();
            var batch = await importer.StageAsync(
                new StageImportBatchRequestDto(
                    LearningLanguage.Japanese,
                    ImportSourceKind.Manual,
                    new[]
                    {
                        new ImportSelectionDto("食前", "jmdict:2:1"),
                        new ImportSelectionDto("食事", "jmdict:10:1")
                    }),
                TestContext.Current.CancellationToken);
            await importer.DecideAsync(
                new DecideImportBatchRequestDto(
                    batch.Batch.BatchId,
                    batch.Batch.Items.Select(item => item.ItemId).ToArray(),
                    ImportDecision.Learning),
                TestContext.Current.CancellationToken);
            var selectedGroupId = Assert.IsType<string>(batch.Batch.Items[0].WordGroupId);
            var navigation = provider.GetRequiredService<NavigationService>();
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(TestContext.Current.CancellationToken);
            navigation.InitializeHome(home);
            await navigation.OpenLearningPlanAsync(
                LearningLanguage.Japanese, TestContext.Current.CancellationToken);
            navigation.CompleteTransition();
            var plan = Assert.IsType<LearningPlanViewModel>(navigation.CurrentPage);
            plan.SelectionMode = LearningSelectionMode.Manual;
            foreach (var option in plan.ChannelOptions)
            {
                option.IsSelected = option.Code == nameof(LearningChannel.MeaningWriting);
            }

            Assert.Equal(2, plan.LibraryItems.Count);
            Assert.Single(plan.LibraryItems, item => item.WordGroupId == selectedGroupId).IsSelected = true;
            await plan.StartAsync(TestContext.Current.CancellationToken);

            Assert.Null(plan.ErrorMessage);
            var session = Assert.IsType<PracticeSessionViewModel>(navigation.CurrentPage);
            Assert.Equal(LearningLanguage.Japanese, session.SelectedLanguage);
            Assert.Equal(new[] { selectedGroupId }, session.Session!.WordGroupIds);
            Assert.NotNull(session.Question);
        }

        [Fact]
        public async Task HomeLearning_WhenManualPlanHasNoActiveSession_OpensSelectionPage()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(temporaryDirectory.Path);
            var plans = provider.GetRequiredService<ILearningPlanReader>();
            var plan = await plans.LoadAsync(
                LearningLanguage.Japanese, TestContext.Current.CancellationToken);
            await provider.GetRequiredService<ILearningPlanEditor>().SaveAsync(
                new SaveLearningPlanRequestDto(
                    LearningLanguage.Japanese,
                    plan.DailyNewTarget,
                    plan.SessionWordLimit,
                    LearningSelectionMode.Manual,
                    plan.EnglishFormLabelMode,
                    plan.EnglishExampleSourceSenseId,
                    plan.FormCodes,
                    plan.Channels,
                    plan.OutputModes),
                TestContext.Current.CancellationToken);
            var navigation = provider.GetRequiredService<NavigationService>();
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(TestContext.Current.CancellationToken);
            navigation.InitializeHome(home);

            await home.OpenLearningAsync(TestContext.Current.CancellationToken);

            Assert.IsType<LearningPlanViewModel>(navigation.CurrentPage);
            Assert.Null(await provider.GetRequiredService<LearningSessionService>().GetActiveAsync(
                LearningLanguage.Japanese, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task HomeLearning_WhenManualPlanHasActiveSession_ResumesIt()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(temporaryDirectory.Path);
            var importer = provider.GetRequiredService<ImportInboxService>();
            var batch = await importer.StageAsync(
                new StageImportBatchRequestDto(
                    LearningLanguage.Japanese,
                    ImportSourceKind.Manual,
                    new[] { new ImportSelectionDto("食前", "jmdict:2:1") }),
                TestContext.Current.CancellationToken);
            await importer.DecideAsync(
                new DecideImportBatchRequestDto(
                    batch.Batch.BatchId,
                    new[] { Assert.Single(batch.Batch.Items).ItemId },
                    ImportDecision.Learning),
                TestContext.Current.CancellationToken);
            var plans = provider.GetRequiredService<ILearningPlanReader>();
            var defaultPlan = await plans.LoadAsync(
                LearningLanguage.Japanese, TestContext.Current.CancellationToken);
            var editor = provider.GetRequiredService<ILearningPlanEditor>();
            await editor.SaveAsync(new SaveLearningPlanRequestDto(
                LearningLanguage.Japanese,
                defaultPlan.DailyNewTarget,
                defaultPlan.SessionWordLimit,
                LearningSelectionMode.Random,
                defaultPlan.EnglishFormLabelMode,
                defaultPlan.EnglishExampleSourceSenseId,
                new[] { "Dictionary" },
                new[] { LearningChannel.MeaningWriting },
                new[] { LearningOutputMode.Kana }), TestContext.Current.CancellationToken);
            var sessions = provider.GetRequiredService<LearningSessionService>();
            var active = await sessions.StartOrResumeAsync(
                new StartLearningSessionRequestDto(LearningLanguage.Japanese),
                TestContext.Current.CancellationToken);
            await editor.SaveAsync(new SaveLearningPlanRequestDto(
                LearningLanguage.Japanese,
                defaultPlan.DailyNewTarget,
                defaultPlan.SessionWordLimit,
                LearningSelectionMode.Manual,
                defaultPlan.EnglishFormLabelMode,
                defaultPlan.EnglishExampleSourceSenseId,
                new[] { "Dictionary" },
                new[] { LearningChannel.MeaningWriting },
                new[] { LearningOutputMode.Kana }), TestContext.Current.CancellationToken);
            var navigation = provider.GetRequiredService<NavigationService>();
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(TestContext.Current.CancellationToken);
            navigation.InitializeHome(home);

            await home.OpenLearningAsync(TestContext.Current.CancellationToken);

            var resumed = Assert.IsType<PracticeSessionViewModel>(navigation.CurrentPage);
            Assert.Equal(active.SessionId, resumed.Session!.SessionId);
            Assert.NotNull(resumed.Question);
        }

        [Fact]
        public async Task ReturningFromLearningPlan_RefreshesHomeDailyTarget()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            using var provider = await CreateProviderAsync(temporaryDirectory.Path);
            var navigation = provider.GetRequiredService<NavigationService>();
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(TestContext.Current.CancellationToken);
            navigation.InitializeHome(home);
            Assert.Equal("学习 0/20", home.LearningProgressLabel);
            await navigation.OpenLearningPlanAsync(LearningLanguage.Japanese, TestContext.Current.CancellationToken);
            navigation.CompleteTransition();
            var plan = await provider.GetRequiredService<ILearningPlanReader>().LoadAsync(
                LearningLanguage.Japanese, TestContext.Current.CancellationToken);
            await provider.GetRequiredService<ILearningPlanEditor>().SaveAsync(
                new SaveLearningPlanRequestDto(
                    LearningLanguage.Japanese, 9, plan.SessionWordLimit, plan.SelectionMode,
                    plan.EnglishFormLabelMode, plan.EnglishExampleSourceSenseId,
                    plan.FormCodes, plan.Channels, plan.OutputModes),
                TestContext.Current.CancellationToken);

            await navigation.GoBackAsync(TestContext.Current.CancellationToken);

            Assert.Same(home, navigation.CurrentPage);
            Assert.Equal("学习 0/9", home.LearningProgressLabel);
            navigation.CompleteTransition();
        }

        [Fact]
        public void HomePage_BindsLearningProgressLabel()
        {
            RunOnSta(() =>
            {
                var page = new HomePage { DataContext = new { LearningProgressLabel = "学习 3/7" } };
                RenderControl(page);
                var label = Assert.Single(FindVisualChildren<TextBlock>(page),
                    item => item.Name == "LearningProgressText");
                Assert.Equal("学习 3/7", label.Text);
            });
        }

        [Fact]
        public void LearningPlanPage_IsSingleWindowUserControlWithPlanAndLibraryActions()
        {
            RunOnSta(() =>
            {
                var page = new LearningPlanPage();
                RenderControl(page);

                Assert.True(typeof(UserControl).IsAssignableFrom(typeof(LearningPlanPage)));
                Assert.Contains(FindVisualChildren<CheckBox>(page), item => Equals(item.Content, "全选"));
                Assert.Contains(FindVisualChildren<Button>(page), item => Equals(item.Content, "保存计划"));
                Assert.Contains(FindVisualChildren<Button>(page), item => Equals(item.Content, "开始本轮"));
                Assert.DoesNotContain(FindVisualChildren<Button>(page), item =>
                    Equals(item.Content, "切换日语") || Equals(item.Content, "切换英语"));
            });
        }

        private static async Task<ServiceProvider> CreateProviderAsync(string root)
        {
            var paths = new ApplicationPaths(root);
            paths.EnsureRuntimeDirectories();
            LexiconTestDatabases.CreateBoth(paths);
            await new SchemaMigrationRunner(paths, new SqliteConnectionFactory(paths)).MigrateAsync(
                TestContext.Current.CancellationToken);
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddDictationDataAccess(root);
            services.AddDictationCore();
            services.AddSingleton<ITtsProvider>(new LearningPresentationTestEnvironment.ControlledTtsProvider());
            services.AddDictationPresentation();
            return services.BuildServiceProvider();
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
            where T : DependencyObject
        {
            if (root is T match)
            {
                yield return match;
            }

            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            {
                foreach (var child in FindVisualChildren<T>(VisualTreeHelper.GetChild(root, index)))
                {
                    yield return child;
                }
            }
        }

        private static void RenderControl(UserControl control)
        {
            control.Width = 1024;
            control.Height = 720;
            control.ApplyTemplate();
            control.Measure(new Size(control.Width, control.Height));
            control.Arrange(new Rect(0, 0, control.Width, control.Height));
            control.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.DataBind, new Action(() => { }));
            control.UpdateLayout();
        }

        private static void RunOnSta(Action action)
        {
            Exception? exception = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception caught)
                {
                    exception = caught;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (exception is not null)
            {
                ExceptionDispatchInfo.Capture(exception).Throw();
            }
        }
    }
}
