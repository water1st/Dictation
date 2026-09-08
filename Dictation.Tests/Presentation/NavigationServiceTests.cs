using Dictation.DataAccess;
using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Lexicon;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Shell;
using Dictation.Presentation.ViewModels;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Lexicon;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Presentation
{
    public sealed class NavigationServiceTests
    {
        [Fact]
        public void InitializeHome_OnlyAcceptsOneRootViewModel()
        {
            using var fixture = new NavigationFixture();

            fixture.Navigation.InitializeHome(fixture.HomeViewModel);

            Assert.Same(fixture.HomeViewModel, fixture.Navigation.CurrentPage);
            Assert.False(fixture.Navigation.CanGoBack);
            Assert.Equal("首页", fixture.Navigation.PageTitle);
            Assert.Throws<InvalidOperationException>(() =>
                fixture.Navigation.InitializeHome(fixture.HomeViewModel));
        }

        [Fact]
        public async Task OpenLexiconLookup_UsesForwardDirectionAndFixedLanguage()
        {
            using var fixture = new NavigationFixture();
            fixture.Navigation.InitializeHome(fixture.HomeViewModel);
            var transitions = new List<NavigationTransitionEventArgs>();
            fixture.Navigation.TransitionRequested += (_, args) => transitions.Add(args);

            await fixture.Navigation.OpenLexiconLookupAsync(LearningLanguage.English);

            var lookup = Assert.IsType<LexiconLookupViewModel>(fixture.Navigation.CurrentPage);
            Assert.Equal(LearningLanguage.English, lookup.SelectedLanguage);
            Assert.Equal(NavigationDirection.Forward, fixture.Navigation.LastDirection);
            Assert.False(fixture.Navigation.CanGoBack);
            var transition = Assert.Single(transitions);
            Assert.Same(fixture.HomeViewModel, transition.PreviousPage);
            Assert.Same(lookup, transition.CurrentPage);
            Assert.Equal(NavigationDirection.Forward, transition.Direction);

            fixture.Navigation.CompleteTransition();
            Assert.True(fixture.Navigation.CanGoBack);
        }

        [Fact]
        public async Task GoBackDuringTransition_IsIgnoredUntilTransitionCompletes()
        {
            using var fixture = new NavigationFixture();
            fixture.Navigation.InitializeHome(fixture.HomeViewModel);

            await fixture.Navigation.OpenLexiconLookupAsync(LearningLanguage.Japanese);

            var lookup = Assert.IsType<LexiconLookupViewModel>(fixture.Navigation.CurrentPage);
            var canGoBackChanges = new List<string?>();
            fixture.Navigation.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(NavigationService.CanGoBack))
                {
                    canGoBackChanges.Add(args.PropertyName);
                }
            };

            Assert.False(fixture.Navigation.CanGoBack);
            await fixture.Navigation.GoBackAsync();

            Assert.Same(lookup, fixture.Navigation.CurrentPage);
            Assert.False(fixture.Navigation.CanGoBack);

            fixture.Navigation.CompleteTransition();

            Assert.True(fixture.Navigation.CanGoBack);
            Assert.Contains(nameof(NavigationService.CanGoBack), canGoBackChanges);
        }

        [Fact]
        public async Task GoBack_UsesBackwardDirectionAndRestoresTheSamePreviousViewModel()
        {
            using var fixture = new NavigationFixture();
            fixture.Navigation.InitializeHome(fixture.HomeViewModel);
            await fixture.Navigation.OpenLexiconLookupAsync(LearningLanguage.Japanese);
            fixture.Navigation.CompleteTransition();
            var lookup = fixture.Navigation.CurrentPage;

            await fixture.Navigation.GoBackAsync();

            Assert.Equal(NavigationDirection.Backward, fixture.Navigation.LastDirection);
            Assert.Same(fixture.HomeViewModel, fixture.Navigation.CurrentPage);
            Assert.False(fixture.Navigation.CanGoBack);
            fixture.Navigation.CompleteTransition();
            Assert.NotSame(lookup, fixture.Navigation.CurrentPage);
        }

        [Fact]
        public async Task ShellViewModel_ForwardsTransitionsAndCompletesTransition()
        {
            using var fixture = new NavigationFixture();
            using var overlay = new OverlayService();
            var shell = new ShellViewModel(fixture.Navigation, overlay);
            var transitions = new List<NavigationTransitionEventArgs>();
            shell.TransitionRequested += (_, args) => transitions.Add(args);
            fixture.Navigation.InitializeHome(fixture.HomeViewModel);

            await fixture.Navigation.OpenLexiconLookupAsync(LearningLanguage.Japanese);

            var lookup = Assert.IsType<LexiconLookupViewModel>(fixture.Navigation.CurrentPage);
            var forwardTransition = Assert.Single(transitions);
            Assert.Same(lookup, forwardTransition.CurrentPage);
            Assert.Equal(NavigationDirection.Forward, forwardTransition.Direction);
            shell.CompleteTransition();
            var probe = Assert.Single(fixture.ScopeProbes);

            await fixture.Navigation.GoBackAsync();

            var backwardTransition = Assert.Single(transitions.Skip(1));
            Assert.Same(fixture.HomeViewModel, backwardTransition.CurrentPage);
            Assert.Equal(NavigationDirection.Backward, backwardTransition.Direction);
            Assert.False(probe.IsDisposed);
            shell.CompleteTransition();
            Assert.True(probe.IsDisposed);
        }

        [Fact]
        public async Task GoBackAtRoot_DoesNotRaiseATransition()
        {
            using var fixture = new NavigationFixture();
            fixture.Navigation.InitializeHome(fixture.HomeViewModel);
            var transitionCount = 0;
            fixture.Navigation.TransitionRequested += (_, _) => transitionCount++;

            await fixture.Navigation.GoBackAsync();

            Assert.Equal(0, transitionCount);
            Assert.Same(fixture.HomeViewModel, fixture.Navigation.CurrentPage);
            Assert.False(fixture.Navigation.CanGoBack);
        }

        [Fact]
        public async Task GoBack_DisposesPoppedScopeOnlyAfterTransitionCompletes()
        {
            using var fixture = new NavigationFixture();
            fixture.Navigation.InitializeHome(fixture.HomeViewModel);
            await fixture.Navigation.OpenLexiconLookupAsync(LearningLanguage.English);
            fixture.Navigation.CompleteTransition();
            var probe = Assert.Single(fixture.ScopeProbes);

            await fixture.Navigation.GoBackAsync();

            Assert.False(probe.IsDisposed);
            fixture.Navigation.CompleteTransition();
            Assert.True(probe.IsDisposed);
        }

        [Fact]
        public async Task ForwardNavigation_DoesNotRetainForwardHistoryAfterGoingBack()
        {
            using var fixture = new NavigationFixture();
            fixture.Navigation.InitializeHome(fixture.HomeViewModel);
            await fixture.Navigation.OpenLexiconLookupAsync(LearningLanguage.Japanese);
            fixture.Navigation.CompleteTransition();
            await fixture.Navigation.GoBackAsync();
            fixture.Navigation.CompleteTransition();

            await fixture.Navigation.OpenImportInboxAsync(LearningLanguage.Japanese);
            fixture.Navigation.CompleteTransition();
            await fixture.Navigation.GoBackAsync();

            Assert.Same(fixture.HomeViewModel, fixture.Navigation.CurrentPage);
            Assert.False(fixture.Navigation.CanGoBack);
            fixture.Navigation.CompleteTransition();
        }

        [Fact]
        public async Task Dispose_ReleasesEveryRetainedPageScope()
        {
            using var fixture = new NavigationFixture();
            fixture.Navigation.InitializeHome(fixture.HomeViewModel);
            await fixture.Navigation.OpenLexiconLookupAsync(LearningLanguage.Japanese);
            fixture.Navigation.CompleteTransition();
            await fixture.Navigation.OpenImportInboxAsync(LearningLanguage.English);
            fixture.Navigation.CompleteTransition();

            Assert.Equal(2, fixture.ScopeProbes.Count);
            Assert.All(fixture.ScopeProbes, probe => Assert.False(probe.IsDisposed));

            fixture.Navigation.Dispose();

            Assert.All(fixture.ScopeProbes, probe => Assert.True(probe.IsDisposed));
        }

        [Fact]
        public async Task Dispose_ReleasesScopePendingCompletion()
        {
            using var fixture = new NavigationFixture();
            fixture.Navigation.InitializeHome(fixture.HomeViewModel);
            await fixture.Navigation.OpenLexiconLookupAsync(LearningLanguage.Japanese);
            fixture.Navigation.CompleteTransition();
            await fixture.Navigation.GoBackAsync();
            var probe = Assert.Single(fixture.ScopeProbes);

            fixture.Navigation.Dispose();

            Assert.True(probe.IsDisposed);
        }

        [Fact]
        public async Task Navigation_RejectsInvalidLanguageAndCancellationWithoutChangingState()
        {
            using var fixture = new NavigationFixture();
            fixture.Navigation.InitializeHome(fixture.HomeViewModel);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                fixture.Navigation.OpenLexiconLookupAsync((LearningLanguage)99));
            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                fixture.Navigation.OpenImportInboxAsync(
                    LearningLanguage.Japanese,
                    cancellation.Token));

            Assert.Same(fixture.HomeViewModel, fixture.Navigation.CurrentPage);
            Assert.Empty(fixture.ScopeProbes);
        }

        [Fact]
        public async Task Navigation_RejectsAnotherOperationUntilTransitionCompletes()
        {
            using var fixture = new NavigationFixture();
            fixture.Navigation.InitializeHome(fixture.HomeViewModel);
            await fixture.Navigation.OpenLexiconLookupAsync(LearningLanguage.Japanese);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                fixture.Navigation.OpenImportInboxAsync(LearningLanguage.Japanese));

            fixture.Navigation.CompleteTransition();
        }

        [Fact]
        public async Task PendingInboxLoad_ReservesNavigationUntilItCompletes()
        {
            using var fixture = new NavigationFixture(initializeData: false);
            fixture.Navigation.InitializeHome(fixture.HomeViewModel);

            var inboxNavigation = fixture.Navigation.OpenImportInboxAsync(LearningLanguage.English);
            await WaitForOverlayAsync(fixture.Overlay);

            await fixture.Navigation.OpenLexiconLookupAsync(LearningLanguage.English);
            var pageDuringLoad = fixture.Navigation.CurrentPage;
            fixture.Overlay.Dismiss();
            var navigationException = await Record.ExceptionAsync(() => inboxNavigation);

            Assert.Same(fixture.HomeViewModel, pageDuringLoad);
            Assert.Null(navigationException);
            Assert.IsType<ImportInboxListViewModel>(fixture.Navigation.CurrentPage);
            fixture.Navigation.CompleteTransition();
        }

        [Fact]
        public async Task InboxDetailBack_RestoresTheSameListAndRefreshesItsDtoState()
        {
            using var fixture = new NavigationFixture();
            fixture.Navigation.InitializeHome(fixture.HomeViewModel);
            await fixture.ImportInboxService.StageAsync(
                new StageImportBatchRequestDto(
                    LearningLanguage.English,
                    ImportSourceKind.Manual,
                    new[] { new ImportSelectionDto("symbols", "missing:symbols:v") }));
            await fixture.ImportInboxService.StageAsync(
                new StageImportBatchRequestDto(
                    LearningLanguage.English,
                    ImportSourceKind.Manual,
                    new[] { new ImportSelectionDto("go", "ecdict:go:v") }));
            await fixture.Navigation.OpenImportInboxAsync(LearningLanguage.English);
            fixture.Navigation.CompleteTransition();
            var list = Assert.IsType<ImportInboxListViewModel>(fixture.Navigation.CurrentPage);
            var selectedBatch = list.Batches.Single(batch => batch.Items[0].SourceText == "go");
            list.SelectedBatch = selectedBatch;
            var selectedItem = Assert.Single(list.Items);
            selectedItem.IsSelected = true;
            list.FilterText = "go";
            list.ScrollOffset = 91.25;

            await list.OpenSelectedDetailAsync();
            var detail = Assert.IsType<ImportInboxDetailViewModel>(fixture.Navigation.CurrentPage);
            Assert.Equal("go", detail.SourceText);
            fixture.Navigation.CompleteTransition();
            var detailProbe = fixture.ScopeProbes[^1];

            await fixture.Navigation.GoBackAsync();

            Assert.Same(list, fixture.Navigation.CurrentPage);
            Assert.Equal(selectedBatch.BatchId, list.SelectedBatch?.BatchId);
            Assert.Equal("go", list.FilterText);
            Assert.Equal(91.25, list.ScrollOffset);
            var refreshedItem = Assert.Single(list.Items);
            Assert.NotSame(selectedItem, refreshedItem);
            Assert.True(refreshedItem.IsSelected);
            Assert.False(detailProbe.IsDisposed);
            fixture.Navigation.CompleteTransition();
            Assert.True(detailProbe.IsDisposed);
        }

        private static async Task WaitForOverlayAsync(OverlayService overlay)
        {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!overlay.IsVisible && DateTime.UtcNow < deadline)
            {
                await Task.Delay(1);
            }

            Assert.True(overlay.IsVisible, "等待导航加载错误弹层显示超时。");
        }

        private sealed class NavigationFixture : IDisposable
        {
            private readonly TemporaryDirectory temporaryDirectory = new();

            public NavigationFixture(bool initializeData = true)
            {
                var paths = new ApplicationPaths(temporaryDirectory.Path);
                paths.EnsureRuntimeDirectories();
                if (initializeData)
                {
                    LexiconTestDatabases.CreateBoth(paths);
                    new SchemaMigrationRunner(
                        paths,
                        new SqliteConnectionFactory(paths)).MigrateAsync().GetAwaiter().GetResult();
                }

                var services = new ServiceCollection();
                services.AddSingleton<NavigationService>(serviceProvider =>
                    new NavigationService(
                        serviceProvider.GetRequiredService<IServiceScopeFactory>()));
                services.AddSingleton<OverlayService>();
                services.AddScoped<ScopeProbe>();
                services.AddScoped<LexiconLookupService>(_ => CreateLexiconService(paths));
                services.AddScoped<ILexiconSearch>(serviceProvider =>
                    serviceProvider.GetRequiredService<LexiconLookupService>());
                services.AddScoped<ImportInboxService>(serviceProvider =>
                    new ImportInboxService(
                        serviceProvider.GetRequiredService<LexiconLookupService>(),
                        new ImportInboxDataAccessor(new SqliteConnectionFactory(paths))));
                services.AddScoped<LexiconLookupViewModel>(serviceProvider =>
                {
                    var probe = serviceProvider.GetRequiredService<ScopeProbe>();
                    ScopeProbes.Add(probe);
                    return ActivatorUtilities.CreateInstance<LexiconLookupViewModel>(serviceProvider);
                });
                services.AddScoped<ImportInboxListViewModel>(serviceProvider =>
                {
                    var probe = serviceProvider.GetRequiredService<ScopeProbe>();
                    ScopeProbes.Add(probe);
                    return ActivatorUtilities.CreateInstance<ImportInboxListViewModel>(serviceProvider);
                });
                services.AddScoped<ImportInboxDetailViewModel>(serviceProvider =>
                {
                    var probe = serviceProvider.GetRequiredService<ScopeProbe>();
                    ScopeProbes.Add(probe);
                    return ActivatorUtilities.CreateInstance<ImportInboxDetailViewModel>(serviceProvider);
                });
                ServiceProvider = services.BuildServiceProvider();
                Navigation = ServiceProvider.GetRequiredService<NavigationService>();
                Overlay = ServiceProvider.GetRequiredService<OverlayService>();
                ImportInboxService = ServiceProvider.GetRequiredService<ImportInboxService>();
                HomeViewModel = new HomeViewModel(
                    new SettingsService(new ConfigurationBuilder().Build(), paths),
                    Navigation);
            }

            public List<ScopeProbe> ScopeProbes { get; } = new();

            public ServiceProvider ServiceProvider { get; }

            public NavigationService Navigation { get; }

            public OverlayService Overlay { get; }

            public ImportInboxService ImportInboxService { get; }

            public HomeViewModel HomeViewModel { get; }

            public void Dispose()
            {
                Navigation.Dispose();
                ServiceProvider.Dispose();
                temporaryDirectory.Dispose();
            }

            private static LexiconLookupService CreateLexiconService(ApplicationPaths paths)
            {
                var factory = new LexiconSqliteConnectionFactory(paths);
                return new LexiconLookupService(
                    new JapaneseLexiconDataAccessor(factory),
                    new EnglishLexiconDataAccessor(factory));
            }
        }

        private sealed class ScopeProbe : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }
    }
}
