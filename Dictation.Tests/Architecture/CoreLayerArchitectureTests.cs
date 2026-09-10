using System.Reflection;
using System.Runtime.Versioning;
using Dictation.DataAccess;
using Dictation.DataAccess.Runtime;
using Dictation.Core.Importing;
using Dictation.Core.Lexicon;
using Dictation.Presentation;
using Dictation.Presentation.ViewModels;
using Dictation.Tests.Infrastructure;
using Dictation.TtsHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Architecture
{
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
        public void AddDictationCore_RejectsNullCollection()
        {
            Assert.Throws<ArgumentNullException>(() =>
                DictationCoreServiceCollectionExtensions.AddDictationCore(null!));
        }
    }
}
