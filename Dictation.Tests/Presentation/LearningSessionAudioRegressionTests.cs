using Dictation.Core.Configuration;
using Dictation.Core.Learning;
using Dictation.Core.Tts;
using Dictation.DataAccess.Importing;
using Dictation.DataAccess.Learning;
using Dictation.DataAccess.Runtime;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.Presentation
{
    /// <summary>
    /// 验证学习题目音频来源、播放取消和答案揭示边界。
    /// </summary>
    public sealed class LearningSessionAudioRegressionTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ReplayFailure_AfterHearingQuestion_PreservesAbilityToSubmit(bool synthesisFailure)
        {
            using var environment = await LearningPresentationTestEnvironment.CreateAsync(dictation: true);
            using var viewModel = environment.CreateViewModel();
            await viewModel.StartAsync(cancellationToken: TestContext.Current.CancellationToken);
            var original = viewModel.Question;
            viewModel.AnswerText = "たべます";
            Assert.True(viewModel.SubmitCommand.CanExecute(null));
            if (synthesisFailure)
            {
                environment.Tts.Failure = new InvalidOperationException("合成不可用");
            }
            else
            {
                environment.Audio.Failure = new InvalidOperationException("播放设备不可用");
            }

            await viewModel.ReplayQuestionAsync(TestContext.Current.CancellationToken);

            Assert.NotNull(viewModel.ErrorMessage);
            Assert.False(viewModel.IsBusy);
            Assert.Same(original, viewModel.Question);
            Assert.Equal("たべます", viewModel.AnswerText);
            Assert.Equal(0L, await environment.CountAttemptsAsync());
            Assert.True(viewModel.SubmitCommand.CanExecute(null));
            await viewModel.SubmitAsync(TestContext.Current.CancellationToken);
            Assert.True(viewModel.Feedback?.WasCorrect);
            Assert.Equal(1L, await environment.CountAttemptsAsync());
        }

        [Fact]
        public async Task ReplayCancellation_AfterHearingQuestion_PreservesAbilityToSubmit()
        {
            using var environment = await LearningPresentationTestEnvironment.CreateAsync(dictation: true);
            using var viewModel = environment.CreateViewModel();
            await viewModel.StartAsync(cancellationToken: TestContext.Current.CancellationToken);
            var original = viewModel.Question;
            viewModel.AnswerText = "たべます";
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            environment.Tts.BeforeReturnAsync = async token =>
            {
                entered.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            };
            var replay = viewModel.ReplayQuestionAsync(TestContext.Current.CancellationToken);
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken: TestContext.Current.CancellationToken);

            viewModel.CancelCommand.Execute(null);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => replay);

            Assert.False(viewModel.IsBusy);
            Assert.Null(viewModel.ErrorMessage);
            Assert.Same(original, viewModel.Question);
            Assert.Equal("たべます", viewModel.AnswerText);
            Assert.Equal(0L, await environment.CountAttemptsAsync());
            Assert.True(viewModel.SubmitCommand.CanExecute(null));
            await viewModel.SubmitAsync(TestContext.Current.CancellationToken);
            Assert.True(viewModel.Feedback?.WasCorrect);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task JapaneseHomographs_PlayCurrentSenseReadingInsteadOfFirstMatchingSurface(bool dictation)
        {
            using var environment = await LearningPresentationTestEnvironment.CreateAsync(importWord: false);
            await SeedHomographsAsync(environment.Paths, dictation);
            using var viewModel = environment.CreateViewModel();
            await viewModel.StartAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(2, viewModel.BrowseMembers.Count);
            var seenReadings = new HashSet<string>(StringComparer.Ordinal);

            for (var index = 0; index < 2; index++)
            {
                var question = Assert.IsType<LearningQuestionDto>(viewModel.Question);
                var expected = Assert.Single(question.Meanings) == "擅长" ? "じょうず" : "うわて";
                seenReadings.Add(expected);
                if (!dictation)
                {
                    Assert.Null(question.SpokenText);
                    Assert.Empty(environment.Tts.Requests);
                    viewModel.AnswerText = "上手";
                    await viewModel.SubmitAsync(TestContext.Current.CancellationToken);
                    Assert.True(viewModel.Feedback?.WasCorrect);
                    Assert.Equal("上手", viewModel.Feedback?.CorrectAnswer);
                    await viewModel.ReplayQuestionAsync(TestContext.Current.CancellationToken);
                }

                Assert.Equal(new TtsSynthesisRequestDto(TtsLanguage.Japanese, expected),
                    Assert.Single(environment.Tts.Requests));
                if (dictation)
                {
                    viewModel.AnswerText = "上手";
                    await viewModel.SubmitAsync(TestContext.Current.CancellationToken);
                    Assert.True(viewModel.Feedback?.WasCorrect);
                }
                environment.Tts.Requests.Clear();
                await viewModel.ContinueAsync(TestContext.Current.CancellationToken);
            }

            Assert.Equal(2, seenReadings.Count);
            Assert.Equal(2L, await environment.CountAttemptsAsync());
        }

        private static async Task SeedHomographsAsync(ApplicationPaths paths, bool dictation)
        {
            var factory = new SqliteConnectionFactory(paths);
            var importer = DataAccessorTestFactory.CreateImportInbox(factory);
            foreach (var (senseId, reading, meaning) in new[]
            {
                ("test:skill", "じょうず", "擅长"),
                ("test:advantage", "うわて", "占上风")
            })
            {
                var member = new ImportWordGroupMemberPo(
                    "Japanese", senseId, senseId, "上手", reading, "名词", "常用汉字", false,
                    "zh-CN", "User", "UserProvided", new[] { meaning },
                    new[]
                    {
                        new ImportWordGroupFormPo("上手", reading, "Dictionary", "Kanji", "Noun",
                            "普通形", "汉字", true, true),
                        new ImportWordGroupFormPo(reading, reading, "Dictionary", "Kana", "Noun",
                            "普通形", "假名", true, false)
                    }, "Common");
                var batch = await importer.StageAsync(new ImportBatchStagePo("Japanese", "Manual",
                    new[] { new ImportBatchStageItemPo("上手", senseId,
                        new ImportWordGroupSnapshotPo("Japanese", senseId, new[] { member })) }),
                    TestContext.Current.CancellationToken);
                await importer.DecideAsync(batch.BatchId, new[] { Assert.Single(batch.Items).ItemId },
                    "Learning", TestContext.Current.CancellationToken);
            }
            await DataAccessorTestFactory.CreateLearningPlan(factory).SavePlanAsync(new LearningPlanPo(
                "Japanese", 20, 20, "Sequential", "GrammarName", null,
                new[] { "Dictionary" }, new[] { dictation ? "Dictation" : "MeaningWriting" },
                new[] { "Kanji" }, DateTimeOffset.UtcNow), TestContext.Current.CancellationToken);
        }
    }
}
