using Dapper;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Tts;
using Dictation.DataAccess;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation.Audio;
using Dictation.Presentation.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Infrastructure
{
    /// <summary>
    /// 为学习表现层测试准备隔离数据库、真实业务服务和可替换外部能力。
    /// </summary>
    internal sealed class LearningPresentationTestEnvironment : IDisposable
    {
        private readonly TemporaryDirectory directory = new();
        private ServiceProvider provider = null!;

        public LearningLanguage Language { get; private init; }
        public ApplicationPaths Paths => provider.GetRequiredService<ApplicationPaths>();
        public LearningSessionService Sessions => provider.GetRequiredService<LearningSessionService>();
        public ControlledTtsProvider Tts { get; } = new();
        public ControlledAudioPlayer Audio { get; } = new();

        public static async Task<LearningPresentationTestEnvironment> CreateAsync(
            LearningLanguage language = LearningLanguage.Japanese,
            bool dictation = false,
            bool importWord = true)
        {
            var environment = new LearningPresentationTestEnvironment { Language = language };
            try
            {
                var services = new ServiceCollection();
                services.AddDictationDataAccess(environment.directory.Path).AddDictationCore();
                services.AddSingleton<ITtsProvider>(environment.Tts);
                environment.provider = services.BuildServiceProvider();
                environment.Paths.EnsureRuntimeDirectories();
                LexiconTestDatabases.CreateBoth(environment.Paths);
                await environment.provider.GetRequiredService<SchemaMigrationRunner>()
                    .MigrateAsync(TestContext.Current.CancellationToken);
                if (importWord)
                {
                    var importer = environment.provider.GetRequiredService<ImportInboxService>();
                    var japanese = language == LearningLanguage.Japanese;
                    var batch = await importer.StageAsync(new StageImportBatchRequestDto(
                        language, ImportSourceKind.Manual,
                        new[] { new ImportSelectionDto(japanese ? "食べる" : "take off",
                            japanese ? "jmdict:1:1" : "take_off%2:38:00::") }),
                        TestContext.Current.CancellationToken);
                    await importer.DecideAsync(new DecideImportBatchRequestDto(
                        batch.Batch.BatchId,
                        new[] { Assert.Single(batch.Batch.Items).ItemId },
                        ImportDecision.Learning),
                        TestContext.Current.CancellationToken);
                }

                await environment.provider.GetRequiredService<LearningPlanService>().SaveAsync(
                    new SaveLearningPlanRequestDto(language, 20, 20, LearningSelectionMode.Sequential,
                        EnglishFormLabelMode.GrammarName, null,
                        new[] { language == LearningLanguage.Japanese ? "Polite" : "Base" },
                        new[] { dictation ? LearningChannel.Dictation : LearningChannel.MeaningWriting },
                        new[] { language == LearningLanguage.Japanese ? LearningOutputMode.Kana : LearningOutputMode.Latin }),
                    TestContext.Current.CancellationToken);
                return environment;
            }
            catch
            {
                environment.Dispose();
                throw;
            }
        }

        public LearningSessionViewModel CreateViewModel()
        {
            var viewModel = new LearningSessionViewModel(
                provider.GetRequiredService<ILearningSessionOperations>(),
                provider.GetRequiredService<ILearningPlanReader>(),
                provider.GetRequiredService<TtsService>(), Audio);
            viewModel.Initialize(Language);
            return viewModel;
        }

        public async Task<long> CountAttemptsAsync()
        {
            await using var connection = provider.GetRequiredService<SqliteConnectionFactory>().CreateConnection();
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            return await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM LearningQuestionAttempts;");
        }

        public void Dispose()
        {
            provider?.Dispose();
            directory.Dispose();
        }

        internal sealed class ControlledTtsProvider : ITtsProvider
        {
            public List<TtsSynthesisRequestDto> Requests { get; } = new();
            public Func<CancellationToken, Task>? BeforeReturnAsync { get; set; }
            public Exception? Failure { get; set; }
            public static readonly byte[] Wav = { 82, 73, 70, 70, 0, 0, 0, 0, 87, 65, 86, 69 };

            public async Task<TtsAudioDto> SynthesizeAsync(
                TtsSynthesisRequestDto request, CancellationToken cancellationToken = default)
            {
                Requests.Add(request);
                if (BeforeReturnAsync is not null) await BeforeReturnAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (Failure is not null) throw Failure;
                return new TtsAudioDto(Wav);
            }
        }

        internal sealed class ControlledAudioPlayer : IAudioPlayer
        {
            public List<byte[]> Played { get; } = new();
            public Exception? Failure { get; set; }

            public Task PlayWavAsync(ReadOnlyMemory<byte> wavData, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Failure is not null) throw Failure;
                Played.Add(wavData.ToArray());
                return Task.CompletedTask;
            }
        }
    }
}
