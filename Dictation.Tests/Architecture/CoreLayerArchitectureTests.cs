using System.Reflection;
using System.Runtime.Versioning;
using Dictation.DataAccess;
using Dictation.DataAccess.Runtime;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Lexicon;
using Dictation.Presentation;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Shell;
using Dictation.Presentation.ViewModels;
using Dictation.Tests.Infrastructure;
using Dictation.TtsHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Architecture
{
    /// <summary>
    /// 验证真实程序集依赖、服务注册身份、消费者能力边界及旧层移除。
    /// </summary>
    public sealed class CoreLayerArchitectureTests
    {
        private static readonly string[] ServiceNames =
        {
            "Configuration.SettingsService",
            "Startup.StartupInitializer",
            "SingleInstance.SingleInstanceCoordinator",
            "Tts.TtsService",
            "Lexicon.LexiconLookupService",
            "Importing.ImportInboxService",
            "Learning.LearningPlanService",
            "Learning.LearningSessionService",
            "Learning.AnswerEvaluationService"
        };

        [Fact]
        public void CoreAssembly_OwnsBusinessServicesAndSharedContracts()
        {
            var core = Assembly.Load("Dictation.Core");
            var contracts = new[]
            {
                "Configuration.ApplicationSettingsDto", "Configuration.LearningLanguage",
                "Importing.ImportBatchDto", "Learning.LearningSessionDto",
                "Learning.LearningQuestionDto", "Lexicon.LexiconLookupResultDto",
                "Tts.ITtsProvider", "Tts.TtsSynthesisRequestDto", "Tts.TtsAudioDto"
            };

            Assert.All(ServiceNames.Concat(contracts), name =>
                Assert.NotNull(core.GetType($"Dictation.Core.{name}")));
            Assert.DoesNotContain(core.GetTypes(), type =>
                type.Namespace?.StartsWith("Dictation.Services", StringComparison.Ordinal) == true);
        }

        [Fact]
        public void CompiledDependencies_UseCoreWithoutReversingTheThreeLayers()
        {
            var core = Assembly.Load("Dictation.Core");
            var consumers = new[] { typeof(HomeWindow).Assembly, typeof(TtsPipeServer).Assembly };

            Assert.All(consumers, consumer =>
            {
                var references = consumer.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
                Assert.Contains("Dictation.Core", references);
                Assert.DoesNotContain("Dictation.Services", references);
            });
            Assert.Contains(core.GetReferencedAssemblies(), reference => reference.Name == "Dictation.DataAccess");
            Assert.DoesNotContain(core.GetReferencedAssemblies(), reference =>
                reference.Name is "Dictation.Presentation" or "Dictation.TtsHost");
            Assert.DoesNotContain(typeof(SqliteConnectionFactory).Assembly.GetReferencedAssemblies(), reference =>
                reference.Name is "Dictation.Core" or "Dictation.Services" or "Dictation.Presentation" or "Dictation.TtsHost");
        }

        [Fact]
        public void CoreAssembly_RunsOnNet10WithoutLegacyDesktopDependencies()
        {
            var core = Assembly.Load("Dictation.Core");

            Assert.Equal(".NETCoreApp,Version=v10.0", core.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName);
            Assert.DoesNotContain(core.GetReferencedAssemblies(), reference => reference.Name is
                "System.Windows.Forms" or "System.Drawing.Common" or "System.Speech" or "Edge_tts_sharp");
        }

        [Fact]
        public async Task AddDictationCore_ResolvesBusinessServicesOnceAndSupportsTheHomeViewModel()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ThemeMode"] = "Dark",
                ["LastLanguage"] = "English"
            }).Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<IHomeNavigation, RejectingHomeNavigation>();
            services.AddDictationDataAccess(temporaryDirectory.Path).AddDictationCore();
            var core = Assembly.Load("Dictation.Core");
            using var provider = services.BuildServiceProvider();

            foreach (var name in ServiceNames)
            {
                var descriptor = Assert.Single(services, item => item.ServiceType.FullName == $"Dictation.Core.{name}");
                Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
                Assert.Same(core, descriptor.ServiceType.Assembly);
                if (name != "Tts.TtsService")
                {
                    Assert.Same(provider.GetRequiredService(descriptor.ServiceType), provider.GetRequiredService(descriptor.ServiceType));
                }
            }

            var home = ActivatorUtilities.CreateInstance<HomeViewModel>(provider);
            await home.InitializeAsync(TestContext.Current.CancellationToken);
            Assert.Equal("英语模式", home.CurrentLanguageLabel);
            Assert.Null(home.ErrorMessage);
            var entryPoints = core.GetTypes().SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .Where(method => method.Name is "AddDictationCore" or "AddDictationServices").ToArray();
            Assert.Equal("AddDictationCore", Assert.Single(entryPoints).Name);
        }

        [Fact]
        public void AddDictationCore_LexiconCapabilitiesAliasTheOriginalSingleton()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var services = new ServiceCollection();
            services.AddDictationDataAccess(temporaryDirectory.Path).AddDictationCore();
            using var provider = services.BuildServiceProvider();

            var implementation = provider.GetRequiredService<LexiconLookupService>();

            Assert.Same(implementation, provider.GetRequiredService<ILexiconSearch>());
            Assert.Same(implementation, provider.GetRequiredService<ILexiconImportGroupResolver>());
            Assert.Same(
                provider.GetRequiredService<ILexiconSearch>(),
                provider.GetRequiredService<ILexiconSearch>());
            Assert.Same(
                provider.GetRequiredService<ILexiconImportGroupResolver>(),
                provider.GetRequiredService<ILexiconImportGroupResolver>());

            var importImplementation = provider.GetRequiredService<ImportInboxService>();
            Assert.Same(importImplementation, provider.GetRequiredService<IImportStaging>());
            Assert.Same(importImplementation, provider.GetRequiredService<IImportInboxQuery>());
            Assert.Same(importImplementation, provider.GetRequiredService<IImportInboxActions>());
            Assert.Same(
                provider.GetRequiredService<IImportStaging>(),
                provider.GetRequiredService<IImportStaging>());
            Assert.Same(
                provider.GetRequiredService<IImportInboxQuery>(),
                provider.GetRequiredService<IImportInboxQuery>());
            Assert.Same(
                provider.GetRequiredService<IImportInboxActions>(),
                provider.GetRequiredService<IImportInboxActions>());
        }

        [Fact]
        public void AddDictationCore_LearningCapabilitiesAliasTheirOriginalSingletons()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var services = new ServiceCollection();
            services.AddDictationDataAccess(temporaryDirectory.Path).AddDictationCore();
            using var provider = services.BuildServiceProvider();

            var planImplementation = provider.GetRequiredService<LearningPlanService>();
            Assert.Same(planImplementation, provider.GetRequiredService<ILearningPlanReader>());
            Assert.Same(planImplementation, provider.GetRequiredService<ILearningPlanEditor>());
            Assert.Same(planImplementation, provider.GetRequiredService<ILearningLibraryManager>());
            Assert.Same(planImplementation, provider.GetRequiredService<ILearningSessionStartPreparer>());
            Assert.Same(
                provider.GetRequiredService<ILearningPlanReader>(),
                provider.GetRequiredService<ILearningPlanReader>());

            var sessionImplementation = provider.GetRequiredService<LearningSessionService>();
            Assert.Same(sessionImplementation, provider.GetRequiredService<ILearningSessionOperations>());
            Assert.Same(
                provider.GetRequiredService<ILearningSessionOperations>(),
                provider.GetRequiredService<ILearningSessionOperations>());
        }

        [Fact]
        public void LearningConsumers_OnlyRequestTheirRequiredCapabilities()
        {
            var planViewModelParameters = Assert.Single(typeof(LearningPlanViewModel).GetConstructors())
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray();
            Assert.Equal(
                new[]
                {
                    typeof(ILearningPlanEditor),
                    typeof(ILearningLibraryManager),
                    typeof(ILearningSessionStartPreparer),
                    typeof(ILexiconSearch),
                    typeof(IBackNavigation),
                    typeof(IOverlayRequester)
                },
                planViewModelParameters);

            var sessionViewModelParameters = Assert.Single(typeof(LearningSessionViewModel).GetConstructors())
                .GetParameters();
            Assert.Equal(typeof(ILearningSessionOperations), sessionViewModelParameters[0].ParameterType);
            Assert.Equal(typeof(ILearningPlanReader), sessionViewModelParameters[1].ParameterType);

            var sessionServiceParameters = Assert.Single(typeof(LearningSessionService).GetConstructors())
                .GetParameters();
            Assert.Equal(typeof(ILearningPlanReader), sessionServiceParameters[1].ParameterType);
            Assert.Equal(typeof(ILearningSessionStartPreparer), sessionServiceParameters[2].ParameterType);
            Assert.False(typeof(LearningPlanService).IsPublic);
            Assert.False(typeof(LearningSessionService).IsPublic);
        }

        [Fact]
        public void ImportConsumers_OnlyRequestTheirRequiredCapabilities()
        {
            var lookupParameters = Assert.Single(typeof(LexiconLookupViewModel).GetConstructors())
                .GetParameters();
            Assert.Equal(typeof(ILexiconSearch), lookupParameters[0].ParameterType);
            Assert.Equal(typeof(IImportStaging), lookupParameters[1].ParameterType);
            Assert.Equal(typeof(IBackNavigation), lookupParameters[2].ParameterType);
            Assert.Equal(3, lookupParameters.Length);

            var inboxParameters = Assert.Single(typeof(ImportInboxListViewModel).GetConstructors())
                .GetParameters();
            Assert.Equal(typeof(IImportInboxQuery), inboxParameters[0].ParameterType);
            Assert.Equal(typeof(IImportInboxActions), inboxParameters[1].ParameterType);
            Assert.Equal(typeof(IBackNavigation), inboxParameters[2].ParameterType);
            Assert.Equal(typeof(IImportInboxDetailNavigation), inboxParameters[3].ParameterType);
            Assert.Equal(typeof(IOverlayRequester), inboxParameters[4].ParameterType);
            Assert.Equal(5, inboxParameters.Length);
            Assert.False(typeof(ImportInboxService).IsPublic);
        }

        [Fact]
        public void PresentationShellAndHome_OnlyRequestTheirRoleCapabilities()
        {
            var homeParameters = Assert.Single(typeof(HomeViewModel).GetConstructors())
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray();
            Assert.Equal(new[] { typeof(SettingsService), typeof(IHomeNavigation) }, homeParameters);

            var shellParameters = Assert.Single(typeof(ShellViewModel).GetConstructors())
                .GetParameters()
                .Select(parameter => parameter.ParameterType)
                .ToArray();
            Assert.Equal(new[] { typeof(INavigationHost), typeof(IOverlayHost) }, shellParameters);
            Assert.DoesNotContain(
                typeof(ShellViewModel).GetProperties(BindingFlags.Public | BindingFlags.Instance),
                property => property.PropertyType is { } type
                    && (type == typeof(NavigationService) || type == typeof(OverlayService)));
            Assert.False(typeof(NavigationService).IsPublic);
            Assert.False(typeof(OverlayService).IsPublic);
        }

        [Fact]
        public void AddDictationCore_RejectsNullCollection()
        {
            Assert.Throws<ArgumentNullException>(() =>
                DictationCoreServiceCollectionExtensions.AddDictationCore(null!));
        }

        private sealed class RejectingHomeNavigation : IHomeNavigation
        {
            public Task OpenLexiconLookupAsync(
                LearningLanguage language,
                CancellationToken cancellationToken = default)
            {
                return Task.FromException(new InvalidOperationException("该架构测试不执行页面导航。"));
            }

            public Task OpenImportInboxAsync(
                LearningLanguage language,
                CancellationToken cancellationToken = default)
            {
                return Task.FromException(new InvalidOperationException("该架构测试不执行页面导航。"));
            }

            public Task OpenLearningPlanAsync(
                LearningLanguage language,
                CancellationToken cancellationToken = default)
            {
                return Task.FromException(new InvalidOperationException("该架构测试不执行页面导航。"));
            }

            public Task OpenLearningAsync(
                LearningLanguage language,
                CancellationToken cancellationToken = default)
            {
                return Task.FromException(new InvalidOperationException("该架构测试不执行学习导航。"));
            }
        }
    }
}
