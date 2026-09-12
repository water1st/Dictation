using Dapper;
using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Learning;
using Dictation.DataAccess.Lexicon;
using Dictation.DataAccess.Runtime;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Lexicon;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.Core
{
    public sealed class LearningQuestionSchedulingTests
    {
        [Fact]
        public async Task GetCurrentQuestionAsync_PersistsAndResumesSamePendingQuestionWithoutAnswerKey()
        {
            using var environment = await TestEnvironment.CreateAsync();
            await environment.StageLearningAsync(LearningLanguage.Japanese, "食べる", "jmdict:1:1");
            await environment.SavePlanAsync(
                LearningLanguage.Japanese,
                new[] { "Dictionary" },
                new[] { LearningChannel.MeaningWriting },
                new[] { LearningOutputMode.Kana });
            var session = await environment.StartAsync(LearningLanguage.Japanese);

            var first = Assert.IsType<LearningQuestionDto>(await environment.Service.GetCurrentQuestionAsync(
                session.SessionId,
                TestContext.Current.CancellationToken));
            var resumed = Assert.IsType<LearningQuestionDto>(await environment.CreateSecondService().GetCurrentQuestionAsync(
                session.SessionId,
                TestContext.Current.CancellationToken));

            Assert.Equivalent(first, resumed);
            Assert.Null(first.SpokenText);
            Assert.Contains("吃", first.Meanings);
            Assert.DoesNotContain(
                typeof(LearningQuestionDto).GetProperties(),
                property => property.Name.Contains("Answer", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(1L, await environment.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM LearningQuestions WHERE SessionId = @SessionId AND Status = 'Pending';",
                new { session.SessionId }));
        }

        [Fact]
        public async Task SubmitAnswerAsync_CoversTwoChannelsAndTwoOutputsWithoutCartesianExpansion()
        {
            using var environment = await TestEnvironment.CreateAsync();
            var groupId = await environment.StageLearningAsync(LearningLanguage.Japanese, "食べる", "jmdict:1:1");
            await environment.SavePlanAsync(
                LearningLanguage.Japanese,
                new[] { "Dictionary" },
                new[] { LearningChannel.MeaningWriting, LearningChannel.Dictation },
                new[] { LearningOutputMode.Kana, LearningOutputMode.Kanji });
            var session = await environment.StartAsync(LearningLanguage.Japanese);
            var first = Assert.IsType<LearningQuestionDto>(await environment.Service.GetCurrentQuestionAsync(
                session.SessionId,
                TestContext.Current.CancellationToken));

            var firstResult = await environment.SubmitCorrectAsync(first);
            var second = Assert.IsType<LearningQuestionDto>(firstResult.NextQuestion);
            var finalResult = await environment.SubmitCorrectAsync(second);

            Assert.NotEqual(first.Channel, second.Channel);
            Assert.NotEqual(first.OutputMode, second.OutputMode);
            Assert.Equal(first.Channel == LearningChannel.Dictation, first.SpokenText is not null);
            Assert.Equal(second.Channel == LearningChannel.Dictation, second.SpokenText is not null);
            Assert.Null(finalResult.NextQuestion);
            Assert.NotNull(finalResult.Summary);
            Assert.Equal(2, finalResult.Summary!.AnsweredCount);
            Assert.Equal("Review", await environment.ExecuteScalarAsync<string>(
                "SELECT Placement FROM WordGroups WHERE WordGroupId = @WordGroupId;",
                new { WordGroupId = groupId }));
        }

        [Fact]
        public async Task GetCurrentQuestionAsync_ExcludesRareKanjiOutput()
        {
            using var environment = await TestEnvironment.CreateAsync();
            await environment.StageLearningAsync(LearningLanguage.Japanese, "跪く", "jmdict:6:1");
            await environment.SavePlanAsync(
                LearningLanguage.Japanese,
                new[] { "Dictionary" },
                new[] { LearningChannel.MeaningWriting },
                new[] { LearningOutputMode.Kana, LearningOutputMode.Kanji });
            var session = await environment.StartAsync(LearningLanguage.Japanese);

            var question = Assert.IsType<LearningQuestionDto>(await environment.Service.GetCurrentQuestionAsync(
                session.SessionId,
                TestContext.Current.CancellationToken));

            Assert.Equal(LearningOutputMode.Kana, question.OutputMode);
            Assert.Equal("假名", question.OutputLabel);
        }

        [Fact]
        public async Task GetCurrentQuestionAsync_EnglishDictationUsesActualTargetAndGrammarLabel()
        {
            using var environment = await TestEnvironment.CreateAsync();
            await environment.StageLearningAsync(LearningLanguage.English, "gone", "go%2:01:00::");
            await environment.SavePlanAsync(
                LearningLanguage.English,
                new[] { "PastParticiple" },
                new[] { LearningChannel.Dictation },
                new[] { LearningOutputMode.Latin });
            var session = await environment.StartAsync(LearningLanguage.English);

            var question = Assert.IsType<LearningQuestionDto>(await environment.Service.GetCurrentQuestionAsync(
                session.SessionId,
                TestContext.Current.CancellationToken));

            Assert.Equal("过去分词", question.FormLabel);
            Assert.Equal("gone", question.SpokenText);
            Assert.Contains("去", question.Meanings);
            Assert.DoesNotContain("gone", question.FormLabel, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SubmitAnswerAsync_WrongAnswerWaitsForOtherUnitsThenRetriesAndSummaryKeepsError()
        {
            using var environment = await TestEnvironment.CreateAsync(seed: 71);
            await environment.StageLearningAsync(LearningLanguage.Japanese, "食前", "jmdict:2:1");
            await environment.StageLearningAsync(LearningLanguage.Japanese, "食事", "jmdict:10:1");
            await environment.StageLearningAsync(LearningLanguage.Japanese, "書く", "jmdict:4:1");
            await environment.SavePlanAsync(
                LearningLanguage.Japanese,
                new[] { "Dictionary" },
                new[] { LearningChannel.MeaningWriting },
                new[] { LearningOutputMode.Kana });
            var session = await environment.StartAsync(LearningLanguage.Japanese);
            var first = Assert.IsType<LearningQuestionDto>(await environment.Service.GetCurrentQuestionAsync(
                session.SessionId,
                TestContext.Current.CancellationToken));

            var wrong = await environment.Service.SubmitAnswerAsync(
                new SubmitLearningAnswerRequestDto(session.SessionId, first.QuestionId, "错误"),
                TestContext.Current.CancellationToken);
            var second = Assert.IsType<LearningQuestionDto>(wrong.NextQuestion);
            var secondResult = await environment.SubmitCorrectAsync(second);
            var third = Assert.IsType<LearningQuestionDto>(secondResult.NextQuestion);
            var thirdResult = await environment.SubmitCorrectAsync(third);
            var retry = Assert.IsType<LearningQuestionDto>(thirdResult.NextQuestion);

            Assert.False(wrong.WasCorrect);
            Assert.True(wrong.HadErrorInSession);
            Assert.Equal(first.WordGroupId, retry.WordGroupId);
            Assert.Equal(first.FormCode, retry.FormCode);
            Assert.Equal(new[] { 0, 1, 2, 3 }, new[]
            {
                first.QuestionOrdinal,
                second.QuestionOrdinal,
                third.QuestionOrdinal,
                retry.QuestionOrdinal
            });
            Assert.NotEqual(first.WordGroupId, second.WordGroupId);
            Assert.NotEqual(first.WordGroupId, third.WordGroupId);
            Assert.NotEqual(second.WordGroupId, third.WordGroupId);

            var result = await environment.SubmitCorrectAsync(retry);
            for (var count = 0; result.Summary is null && count < 20; count++)
            {
                result = await environment.SubmitCorrectAsync(Assert.IsType<LearningQuestionDto>(result.NextQuestion));
            }

            var summary = Assert.IsType<LearningSessionSummaryDto>(result.Summary);
            Assert.True(result.HadErrorInSession);
            Assert.Contains(summary.Errors, error => error.WordGroupId == first.WordGroupId && error.FormCode == first.FormCode);
        }

        [Fact]
        public async Task SubmitAnswerAsync_ErrorResetsOnlyTargetForm()
        {
            using var environment = await TestEnvironment.CreateAsync();
            await environment.StageLearningAsync(LearningLanguage.Japanese, "食べる", "jmdict:1:1");
            await environment.SavePlanAsync(
                LearningLanguage.Japanese,
                new[] { "Dictionary", "Polite" },
                new[] { LearningChannel.MeaningWriting },
                new[] { LearningOutputMode.Kana });
            var session = await environment.StartAsync(LearningLanguage.Japanese);
            await environment.ExecuteAsync(
                "UPDATE LearningFormStates SET CorrectStreak = 1, MeaningWritingCount = 1, KanaCount = 1, CorrectCount = 1;");
            var question = Assert.IsType<LearningQuestionDto>(await environment.Service.GetCurrentQuestionAsync(
                session.SessionId,
                TestContext.Current.CancellationToken));

            await environment.Service.SubmitAnswerAsync(
                new SubmitLearningAnswerRequestDto(session.SessionId, question.QuestionId, "错误"),
                TestContext.Current.CancellationToken);

            Assert.Equal(1L, await environment.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM LearningFormStates WHERE CorrectStreak = 0 AND ErrorCount = 1;"));
            Assert.Equal(1L, await environment.ExecuteScalarAsync<long>(
                "SELECT COUNT(*) FROM LearningFormStates WHERE CorrectStreak = 1 AND ErrorCount = 0;"));
        }

        [Fact]
        public async Task SubmitAnswerAsync_RejectsExternalRepeatedAndCompletedQuestionsWithoutPartialWrites()
        {
            using var environment = await TestEnvironment.CreateAsync();
            await environment.StageLearningAsync(LearningLanguage.Japanese, "食前", "jmdict:2:1");
            await environment.SavePlanAsync(
                LearningLanguage.Japanese,
                new[] { "Dictionary" },
                new[] { LearningChannel.MeaningWriting },
                new[] { LearningOutputMode.Kana });
            var session = await environment.StartAsync(LearningLanguage.Japanese);
            var question = Assert.IsType<LearningQuestionDto>(await environment.Service.GetCurrentQuestionAsync(
                session.SessionId,
                TestContext.Current.CancellationToken));

            await Assert.ThrowsAsync<InvalidOperationException>(() => environment.Service.SubmitAnswerAsync(
                new SubmitLearningAnswerRequestDto(session.SessionId, question.QuestionId + 1000, "しょくぜん"),
                TestContext.Current.CancellationToken));
            Assert.Equal(0L, await environment.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM LearningQuestionAttempts;"));

            var accepted = await environment.SubmitCorrectAsync(question);
            await Assert.ThrowsAsync<InvalidOperationException>(() => environment.Service.SubmitAnswerAsync(
                new SubmitLearningAnswerRequestDto(session.SessionId, question.QuestionId, "しょくぜん"),
                TestContext.Current.CancellationToken));
            Assert.Equal(1L, await environment.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM LearningQuestionAttempts;"));

            var next = Assert.IsType<LearningQuestionDto>(accepted.NextQuestion);
            await environment.ExecuteAsync(
                "UPDATE LearningSessions SET Status = 'Completed', CompletedAtUtc = @CompletedAtUtc WHERE SessionId = @SessionId;",
                new { CompletedAtUtc = environment.TimeProvider.GetUtcNow().ToString("O"), session.SessionId });
            await Assert.ThrowsAsync<InvalidOperationException>(() => environment.Service.SubmitAnswerAsync(
                new SubmitLearningAnswerRequestDto(session.SessionId, next.QuestionId, "しょくぜん"),
                TestContext.Current.CancellationToken));
            Assert.Equal(1L, await environment.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM LearningQuestionAttempts;"));
        }

        [Fact]
        public async Task GetCurrentQuestionAsync_PreCanceledTokenDoesNotPersistQuestion()
        {
            using var environment = await TestEnvironment.CreateAsync();
            await environment.StageLearningAsync(LearningLanguage.Japanese, "食前", "jmdict:2:1");
            var session = await environment.StartAsync(LearningLanguage.Japanese);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => environment.Service.GetCurrentQuestionAsync(
                session.SessionId,
                cancellation.Token));

            Assert.Equal(0L, await environment.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM LearningQuestions;"));
        }

        [Fact]
        public async Task GetCurrentQuestionAsync_AfterCoveragePrioritizesHighestPersistentErrorRate()
        {
            using var environment = await TestEnvironment.CreateAsync(seed: 9);
            await environment.StageLearningAsync(LearningLanguage.Japanese, "食べる", "jmdict:1:1");
            await environment.SavePlanAsync(
                LearningLanguage.Japanese,
                new[] { "Dictionary", "Polite" },
                new[] { LearningChannel.MeaningWriting },
                new[] { LearningOutputMode.Kana });
            var session = await environment.StartAsync(LearningLanguage.Japanese);
            await environment.ExecuteAsync(
                """
                UPDATE LearningFormStates
                SET MeaningWritingCount = 1, KanaCount = 1,
                    CorrectCount = CASE WHEN FormCode = 'Dictionary' THEN 1 ELSE 5 END,
                    ErrorCount = CASE WHEN FormCode = 'Dictionary' THEN 4 ELSE 0 END;
                """);

            var question = Assert.IsType<LearningQuestionDto>(await environment.Service.GetCurrentQuestionAsync(
                session.SessionId,
                TestContext.Current.CancellationToken));

            Assert.Equal("Dictionary", question.FormCode);
        }

        [Fact]
        public async Task SubmitAnswerAsync_WhenThreeOtherUnitsExistWaitsAtLeastThreeQuestions()
        {
            using var environment = await TestEnvironment.CreateAsync(seed: 17);
            await environment.StageLearningAsync(LearningLanguage.Japanese, "食べる", "jmdict:1:1");
            await environment.SavePlanAsync(
                LearningLanguage.Japanese,
                new[] { "Dictionary", "Polite", "Te", "Ta" },
                new[] { LearningChannel.MeaningWriting },
                new[] { LearningOutputMode.Kana });
            var session = await environment.StartAsync(LearningLanguage.Japanese);
            var first = Assert.IsType<LearningQuestionDto>(await environment.Service.GetCurrentQuestionAsync(
                session.SessionId,
                TestContext.Current.CancellationToken));

            var result = await environment.SubmitCorrectAsync(first);
            var interval = await environment.ExecuteScalarAsync<long>(
                """
                SELECT NextEligibleOrdinal
                FROM LearningFormStates AS States
                INNER JOIN LearningQuestions AS Questions
                    ON Questions.WordGroupId = States.WordGroupId
                    AND Questions.MemberId = States.MemberId
                    AND Questions.FormCode = States.FormCode
                WHERE Questions.QuestionId = @QuestionId;
                """,
                new { first.QuestionId });
            Assert.InRange(interval - first.QuestionOrdinal, 4L, 8L);

            var intervening = new List<LearningQuestionDto>();
            for (var count = 0; count < 3; count++)
            {
                var question = Assert.IsType<LearningQuestionDto>(result.NextQuestion);
                Assert.NotEqual(first.FormCode, question.FormCode);
                intervening.Add(question);
                result = await environment.SubmitCorrectAsync(question);
            }

            Assert.Equal(3, intervening.Select(question => question.FormCode).Distinct(StringComparer.Ordinal).Count());
            var retry = Assert.IsType<LearningQuestionDto>(result.NextQuestion);
            Assert.Equal(first.FormCode, retry.FormCode);
            Assert.Equal(4, retry.QuestionOrdinal);
        }

        [Fact]
        public async Task StartAsync_PhraseCreatesOnlyBaseFormState()
        {
            using var environment = await TestEnvironment.CreateAsync();
            await environment.StageLearningAsync(LearningLanguage.English, "take off", "take_off%2:38:00::");
            await environment.SavePlanAsync(
                LearningLanguage.English,
                new[] { "Base", "Past" },
                new[] { LearningChannel.MeaningWriting },
                new[] { LearningOutputMode.Latin });

            await environment.StartAsync(LearningLanguage.English);

            Assert.Equal(0L, await environment.ExecuteScalarAsync<long>(
                """
                SELECT COUNT(*)
                FROM LearningFormStates AS States
                INNER JOIN WordGroupMembers AS Members ON Members.MemberId = States.MemberId
                WHERE Members.IsPhrase = 1 AND States.FormCode <> 'Base';
                """));
        }

        [Fact]
        public async Task ApplyAnswerAsync_WhenNextQuestionIsInvalidRollsBackEntireTransition()
        {
            using var environment = await TestEnvironment.CreateAsync();
            await environment.StageLearningAsync(LearningLanguage.Japanese, "食前", "jmdict:2:1");
            await environment.SavePlanAsync(
                LearningLanguage.Japanese,
                new[] { "Dictionary" },
                new[] { LearningChannel.MeaningWriting },
                new[] { LearningOutputMode.Kana });
            var session = await environment.StartAsync(LearningLanguage.Japanese);
            var question = Assert.IsType<LearningQuestionDto>(await environment.Service.GetCurrentQuestionAsync(
                session.SessionId,
                TestContext.Current.CancellationToken));
            var target = await environment.ReadQuestionTargetAsync(question.QuestionId);

            await Assert.ThrowsAsync<LearningSessionDataAccessException>(() => environment.DataAccessor.ApplyAnswerAsync(
                new ApplyLearningAnswerPo(
                    session.SessionId,
                    question.QuestionId,
                    "しょくぜん",
                    true,
                    1,
                    false,
                    4,
                    environment.TimeProvider.GetUtcNow(),
                    "2026-09-06",
                    new CreateLearningQuestionPo(
                        session.SessionId,
                        "missing-group",
                        target.MemberId,
                        target.FormCode,
                        target.Channel,
                        target.OutputMode,
                        1,
                        environment.TimeProvider.GetUtcNow()),
                    Array.Empty<string>(),
                    false),
                TestContext.Current.CancellationToken));

            Assert.Equal("0|Pending|0|0", await environment.ExecuteScalarAsync<string>(
                """
                SELECT
                    (SELECT COUNT(*) FROM LearningQuestionAttempts) || '|' ||
                    (SELECT Status FROM LearningQuestions WHERE QuestionId = @QuestionId) || '|' ||
                    (SELECT CurrentOrdinal FROM LearningSessions WHERE SessionId = @SessionId) || '|' ||
                    (SELECT CorrectStreak FROM LearningFormStates WHERE MemberId = @MemberId AND FormCode = @FormCode);
                """,
                new { question.QuestionId, session.SessionId, target.MemberId, target.FormCode }));
        }

        [Fact]
        public async Task GetCurrentQuestionAsync_WhenSqliteFailsWrapsDataAccessError()
        {
            using var environment = await TestEnvironment.CreateAsync();
            await environment.StageLearningAsync(LearningLanguage.Japanese, "食前", "jmdict:2:1");
            var session = await environment.StartAsync(LearningLanguage.Japanese);
            await environment.ExecuteAsync("DROP TABLE LearningQuestions;");

            var exception = await Assert.ThrowsAsync<LearningSessionDataAccessException>(() =>
                environment.Service.GetCurrentQuestionAsync(session.SessionId, TestContext.Current.CancellationToken));

            Assert.NotNull(exception.InnerException);
        }

        [Fact]
        public async Task SubmitAnswerAsync_PreCanceledTokenDoesNotChangePendingQuestion()
        {
            using var environment = await TestEnvironment.CreateAsync();
            await environment.StageLearningAsync(LearningLanguage.Japanese, "食前", "jmdict:2:1");
            var session = await environment.StartAsync(LearningLanguage.Japanese);
            var question = Assert.IsType<LearningQuestionDto>(await environment.Service.GetCurrentQuestionAsync(
                session.SessionId,
                TestContext.Current.CancellationToken));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => environment.Service.SubmitAnswerAsync(
                new SubmitLearningAnswerRequestDto(session.SessionId, question.QuestionId, "しょくぜん"),
                cancellation.Token));

            Assert.Equal("Pending|0", await environment.ExecuteScalarAsync<string>(
                """
                SELECT Questions.Status || '|' || (SELECT COUNT(*) FROM LearningQuestionAttempts)
                FROM LearningQuestions AS Questions WHERE Questions.QuestionId = @QuestionId;
                """,
                new { question.QuestionId }));
        }

        [Fact]
        public async Task StartAsync_WhenReviewGroupGetsNewFormCreatesSupplementalLearningSession()
        {
            using var environment = await TestEnvironment.CreateAsync();
            var groupId = await environment.StageLearningAsync(LearningLanguage.Japanese, "食べる", "jmdict:1:1");
            await environment.SavePlanAsync(
                LearningLanguage.Japanese,
                new[] { "Dictionary" },
                new[] { LearningChannel.MeaningWriting },
                new[] { LearningOutputMode.Kana });
            var firstSession = await environment.StartAsync(LearningLanguage.Japanese);
            var question = Assert.IsType<LearningQuestionDto>(await environment.Service.GetCurrentQuestionAsync(
                firstSession.SessionId,
                TestContext.Current.CancellationToken));
            var result = await environment.SubmitCorrectAsync(question);
            result = await environment.SubmitCorrectAsync(Assert.IsType<LearningQuestionDto>(result.NextQuestion));
            Assert.NotNull(result.Summary);
            Assert.Equal("Review", await environment.ExecuteScalarAsync<string>(
                "SELECT Placement FROM WordGroups WHERE WordGroupId = @WordGroupId;",
                new { WordGroupId = groupId }));

            await environment.SavePlanAsync(
                LearningLanguage.Japanese,
                new[] { "Dictionary", "Polite" },
                new[] { LearningChannel.MeaningWriting },
                new[] { LearningOutputMode.Kana });
            var supplemental = await environment.StartAsync(LearningLanguage.Japanese);
            var supplementalQuestion = Assert.IsType<LearningQuestionDto>(await environment.Service.GetCurrentQuestionAsync(
                supplemental.SessionId,
                TestContext.Current.CancellationToken));

            Assert.Equal(LearningSessionStatus.InProgress, supplemental.Status);
            Assert.Equal(new[] { groupId }, supplemental.WordGroupIds);
            Assert.Equal("Polite", supplementalQuestion.FormCode);
        }

        [Fact]
        public async Task GetCurrentQuestionAsync_EnglishExampleParadigmUsesExampleWithoutReplacingTarget()
        {
            using var environment = await TestEnvironment.CreateAsync();
            await environment.StageLearningAsync(LearningLanguage.English, "gone", "go%2:01:00::");
            await environment.SavePlanAsync(
                LearningLanguage.English,
                new[] { "Past" },
                new[] { LearningChannel.MeaningWriting },
                new[] { LearningOutputMode.Latin },
                EnglishFormLabelMode.ExampleParadigm,
                "go%2:01:00::");
            var session = await environment.StartAsync(LearningLanguage.English);
            await environment.ExecuteAsync(
                """
                DELETE FROM LearningFormStates
                WHERE MemberId IN (SELECT MemberId FROM WordGroupMembers WHERE Headword <> 'convert');
                """);

            var question = Assert.IsType<LearningQuestionDto>(await environment.Service.GetCurrentQuestionAsync(
                session.SessionId,
                TestContext.Current.CancellationToken));
            var correctAnswer = await environment.ReadPreferredAnswerAsync(question.QuestionId);

            Assert.Equal("went", question.FormLabel);
            Assert.Equal("converted", correctAnswer);
            Assert.NotEqual(question.FormLabel, correctAnswer);
            Assert.Null(question.SpokenText);
        }

        [Fact]
        public async Task SubmitAnswerAsync_DisabledFormDoesNotBlockCurrentSessionAndCanReturnLater()
        {
            using var environment = await TestEnvironment.CreateAsync();
            var groupId = await environment.StageLearningAsync(LearningLanguage.Japanese, "食べる", "jmdict:1:1");
            await environment.SavePlanAsync(
                LearningLanguage.Japanese,
                new[] { "Dictionary", "Polite" },
                new[] { LearningChannel.MeaningWriting },
                new[] { LearningOutputMode.Kana });
            var abandoned = await environment.StartAsync(LearningLanguage.Japanese);
            await environment.ExecuteAsync(
                """
                UPDATE LearningSessions
                SET Status = 'Completed', CompletedAtUtc = @CompletedAtUtc
                WHERE SessionId = @SessionId;
                """,
                new { CompletedAtUtc = environment.TimeProvider.GetUtcNow().ToString("O"), abandoned.SessionId });

            await environment.SavePlanAsync(
                LearningLanguage.Japanese,
                new[] { "Dictionary" },
                new[] { LearningChannel.MeaningWriting },
                new[] { LearningOutputMode.Kana });
            var dictionarySession = await environment.StartAsync(LearningLanguage.Japanese);
            var question = Assert.IsType<LearningQuestionDto>(await environment.Service.GetCurrentQuestionAsync(
                dictionarySession.SessionId,
                TestContext.Current.CancellationToken));
            var result = await environment.SubmitCorrectAsync(question);
            result = await environment.SubmitCorrectAsync(Assert.IsType<LearningQuestionDto>(result.NextQuestion));

            Assert.NotNull(result.Summary);
            Assert.Equal(0L, await environment.ExecuteScalarAsync<long>(
                "SELECT IsMastered FROM LearningFormStates WHERE WordGroupId = @WordGroupId AND FormCode = 'Polite';",
                new { WordGroupId = groupId }));

            await environment.SavePlanAsync(
                LearningLanguage.Japanese,
                new[] { "Polite" },
                new[] { LearningChannel.MeaningWriting },
                new[] { LearningOutputMode.Kana });
            var supplemental = await environment.StartAsync(LearningLanguage.Japanese);
            var supplementalQuestion = Assert.IsType<LearningQuestionDto>(await environment.Service.GetCurrentQuestionAsync(
                supplemental.SessionId,
                TestContext.Current.CancellationToken));
            Assert.Equal("Polite", supplementalQuestion.FormCode);
        }

        [Fact]
        public async Task StartAsync_WhenOnlyRareKanjiIsAvailableReturnsCompletedEmptySession()
        {
            using var environment = await TestEnvironment.CreateAsync();
            await environment.StageLearningAsync(LearningLanguage.Japanese, "跪く", "jmdict:6:1");
            await environment.SavePlanAsync(
                LearningLanguage.Japanese,
                new[] { "Dictionary" },
                new[] { LearningChannel.MeaningWriting },
                new[] { LearningOutputMode.Kanji });

            var session = await environment.StartAsync(LearningLanguage.Japanese);

            Assert.Equal(LearningSessionStatus.Completed, session.Status);
            Assert.Empty(session.WordGroupIds);
            Assert.Null(await environment.Service.GetCurrentQuestionAsync(
                session.SessionId,
                TestContext.Current.CancellationToken));
        }

        private sealed class TestEnvironment : IDisposable
        {
            private readonly TemporaryDirectory temporaryDirectory;
            private readonly SqliteConnectionFactory connectionFactory;
            private readonly LearningPlanService planService;
            private readonly AnswerEvaluationService evaluationService;
            private readonly int seed;

            private TestEnvironment(
                TemporaryDirectory temporaryDirectory,
                ApplicationPaths paths,
                SqliteConnectionFactory connectionFactory,
                LearningSessionDataAccessor dataAccessor,
                LearningPlanService planService,
                ImportInboxService importService,
                AnswerEvaluationService evaluationService,
                MutableTimeProvider timeProvider,
                LearningSessionService service,
                int seed)
            {
                this.temporaryDirectory = temporaryDirectory;
                this.connectionFactory = connectionFactory;
                DataAccessor = dataAccessor;
                this.planService = planService;
                this.evaluationService = evaluationService;
                this.seed = seed;
                Paths = paths;
                ImportService = importService;
                TimeProvider = timeProvider;
                Service = service;
            }

            public ApplicationPaths Paths { get; }
            public ImportInboxService ImportService { get; }
            public MutableTimeProvider TimeProvider { get; }
            public LearningSessionService Service { get; }
            public LearningSessionDataAccessor DataAccessor { get; }

            public static async Task<TestEnvironment> CreateAsync(int seed = 1234)
            {
                var temporaryDirectory = new TemporaryDirectory();
                var paths = new ApplicationPaths(temporaryDirectory.Path);
                paths.EnsureRuntimeDirectories();
                LexiconTestDatabases.CreateBoth(paths);
                var connectionFactory = new SqliteConnectionFactory(paths);
                await new SchemaMigrationRunner(paths, connectionFactory).MigrateAsync(TestContext.Current.CancellationToken);
                var lexiconFactory = new LexiconSqliteConnectionFactory(paths);
                var lexiconService = new LexiconLookupService(
                    new JapaneseLexiconDataAccessor(lexiconFactory),
                    new EnglishLexiconDataAccessor(lexiconFactory));
                var timeProvider = new MutableTimeProvider(
                    new DateTimeOffset(2026, 9, 6, 9, 0, 0, TimeSpan.FromHours(8)));
                var planService = new LearningPlanService(
                    new LearningPlanDataAccessor(connectionFactory),
                    lexiconService,
                    timeProvider);
                var importService = new ImportInboxService(lexiconService, new ImportInboxDataAccessor(connectionFactory));
                var evaluationService = new AnswerEvaluationService();
                var dataAccessor = new LearningSessionDataAccessor(connectionFactory);
                var service = new LearningSessionService(
                    dataAccessor,
                    planService,
                    planService,
                    evaluationService,
                    timeProvider,
                    new Random(seed));
                var environment = new TestEnvironment(
                    temporaryDirectory,
                    paths,
                    connectionFactory,
                    dataAccessor,
                    planService,
                    importService,
                    evaluationService,
                    timeProvider,
                    service,
                    seed);
                return environment;
            }

            public LearningSessionService CreateSecondService() => new(
                new LearningSessionDataAccessor(connectionFactory),
                planService,
                planService,
                evaluationService,
                TimeProvider,
                new Random(seed + 1));

            public async Task<string> StageLearningAsync(
                LearningLanguage language,
                string sourceText,
                string sourceSenseId)
            {
                var staged = await ImportService.StageAsync(
                    new StageImportBatchRequestDto(
                        language,
                        ImportSourceKind.Manual,
                        new[] { new ImportSelectionDto(sourceText, sourceSenseId) }),
                    TestContext.Current.CancellationToken);
                var item = Assert.Single(staged.Batch.Items);
                await ImportService.DecideAsync(
                    new DecideImportBatchRequestDto(
                        staged.Batch.BatchId,
                        new[] { item.ItemId },
                        ImportDecision.Learning),
                    TestContext.Current.CancellationToken);
                return Assert.IsType<string>(item.WordGroupId);
            }

            public Task<LearningPlanDto> SavePlanAsync(
                LearningLanguage language,
                IReadOnlyList<string> formCodes,
                IReadOnlyList<LearningChannel> channels,
                IReadOnlyList<LearningOutputMode> outputs,
                EnglishFormLabelMode labelMode = EnglishFormLabelMode.GrammarName,
                string? exampleSourceSenseId = null)
            {
                return planService.SaveAsync(
                    new SaveLearningPlanRequestDto(
                        language,
                        20,
                        20,
                        LearningSelectionMode.Sequential,
                        labelMode,
                        exampleSourceSenseId,
                        formCodes,
                        channels,
                        outputs),
                    TestContext.Current.CancellationToken);
            }

            public Task<LearningSessionDto> StartAsync(LearningLanguage language) => Service.StartOrResumeAsync(
                new StartLearningSessionRequestDto(language),
                TestContext.Current.CancellationToken);

            public async Task<LearningAnswerResultDto> SubmitCorrectAsync(LearningQuestionDto question)
            {
                return await Service.SubmitAnswerAsync(
                    new SubmitLearningAnswerRequestDto(
                        question.SessionId,
                        question.QuestionId,
                        await ReadPreferredAnswerAsync(question.QuestionId)),
                    TestContext.Current.CancellationToken);
            }

            public async Task<string> ReadPreferredAnswerAsync(long questionId)
            {
                await using var connection = connectionFactory.CreateConnection();
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                return await connection.QuerySingleAsync<string>(
                    """
                    SELECT Forms.Surface
                    FROM LearningQuestions AS Questions
                    INNER JOIN WordGroupForms AS Forms
                        ON Forms.MemberId = Questions.MemberId AND Forms.FormCode = Questions.FormCode
                    WHERE Questions.QuestionId = @QuestionId
                      AND (Questions.OutputMode = 'Latin' OR Forms.ScriptCode = Questions.OutputMode)
                    ORDER BY Forms.IsPrimary DESC, Forms.IsCommon DESC, Forms.FormIndex
                    LIMIT 1;
                    """,
                    new { QuestionId = questionId });
            }

            public async Task<QuestionTarget> ReadQuestionTargetAsync(long questionId)
            {
                await using var connection = connectionFactory.CreateConnection();
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                return await connection.QuerySingleAsync<QuestionTarget>(
                    """
                    SELECT MemberId, FormCode, Channel, OutputMode
                    FROM LearningQuestions WHERE QuestionId = @QuestionId;
                    """,
                    new { QuestionId = questionId });
            }

            public async Task<T> ExecuteScalarAsync<T>(string sql, object? parameters = null)
            {
                await using var connection = connectionFactory.CreateConnection();
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                return (await connection.ExecuteScalarAsync<T>(sql, parameters))!;
            }

            public async Task ExecuteAsync(string sql, object? parameters = null)
            {
                await using var connection = connectionFactory.CreateConnection();
                await connection.OpenAsync(TestContext.Current.CancellationToken);
                await connection.ExecuteAsync(sql, parameters);
            }

            public void Dispose() => temporaryDirectory.Dispose();

            public sealed class QuestionTarget
            {
                public long MemberId { get; set; }
                public string FormCode { get; set; } = string.Empty;
                public string Channel { get; set; } = string.Empty;
                public string OutputMode { get; set; } = string.Empty;
            }
        }

        public sealed class MutableTimeProvider : TimeProvider
        {
            public MutableTimeProvider(DateTimeOffset value)
            {
                Value = value;
            }

            public DateTimeOffset Value { get; set; }

            public override DateTimeOffset GetUtcNow() => Value.ToUniversalTime();

            public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone(
                "UTC+8",
                TimeSpan.FromHours(8),
                "UTC+8",
                "UTC+8");
        }
    }
}
