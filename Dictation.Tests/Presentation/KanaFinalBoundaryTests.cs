using Dapper;
using Dictation.Core.Kana;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace Dictation.Tests.Presentation
{
    /// <summary>复现最终审查的真实提交边界与TextBox转换问题，不替代Core判题或数据库事务。</summary>
    public sealed class KanaFinalBoundaryTests
    {
        private static CancellationToken Token => TestContext.Current.CancellationToken;

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public Task SubmittedThenCancelledOrResponseLost_RestoresFeedbackWithoutAnotherWrite(bool cancel, bool finalQuestion) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            var gate = new SubmissionGate { LoseResponse = !cancel };
            using var provider = await CreateAsync(directory.Path, gate);
            await provider.GetRequiredService<IKanaPlanEditor>().SaveAsync(new(true, true, false, true, false, false, false, finalQuestion ? 1 : 2), Token);
            var vm = provider.GetRequiredService<KanaSessionViewModel>();
            await vm.StartAsync(Token);
            vm.AnswerText = finalQuestion ? Answer(vm.Question!) : "错误";
            var pending = vm.SubmitAsync(Token);
            await gate.Entered.Task.WaitAsync(Token);
            Assert.Equal(1L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaAttempts;"));
            if (cancel) vm.CancelOperation();
            gate.Release.TrySetResult();
            await pending;
            Assert.NotNull(vm.Feedback);
            Assert.True(vm.IsFeedbackVisible);
            Assert.Equal(finalQuestion, vm.Feedback.IsCompleted);
            Assert.Equal(finalQuestion ? 1 : 0, vm.Feedback.CompletedCount);
            Assert.False(vm.SubmitCommand.CanExecute(null));
            await vm.ContinueAsync(Token);
            Assert.Equal(finalQuestion, vm.IsCompleted);
            Assert.Equal(finalQuestion ? 0 : 1, vm.Errors.Count);
            if (finalQuestion)
            {
                Assert.Equal("进度 1/1", vm.ProgressLabel);
                await vm.SubmitAsync(Token);
                Assert.Equal(1L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaMemories WHERE ReviewCount=1 AND LapseCount=0;"));
            }
            else
            {
                Assert.Equal(1, vm.Question!.QuestionOrdinal);
                Assert.True(vm.SubmitCommand.CanExecute(null));
            }
            Assert.Equal(1L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaAttempts;"));
        });

        [Fact]
        public Task ReturningAfterCommit_DoesNotPublishOldFeedbackOrDuplicateObservation() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            var gate = new SubmissionGate();
            using var provider = await CreateAsync(directory.Path, gate);
            await provider.GetRequiredService<IKanaPlanEditor>().SaveAsync(new(true, true, false, true, false, false, false, 1), Token);
            var home = provider.GetRequiredService<HomeViewModel>();
            var nav = provider.GetRequiredService<NavigationService>();
            nav.InitializeWorkspace(home);
            await home.OpenKanaAsync(Token); nav.CompleteTransition();
            var plan = Assert.IsType<KanaPlanViewModel>(nav.CurrentPage);
            await plan.StartAsync(Token); nav.CompleteTransition();
            var old = Assert.IsType<KanaSessionViewModel>(nav.CurrentPage);
            old.AnswerText = Answer(old.Question!);
            var pending = old.SubmitAsync(Token);
            await gate.Entered.Task.WaitAsync(Token);
            await nav.GoBackAsync(Token);
            // 尚未结束退场动画，退出标记也必须立即阻止迟到反馈。
            gate.Release.TrySetResult();
            await pending;
            Assert.Null(old.Feedback);
            Assert.Null(old.ErrorMessage);
            Assert.Same(plan, nav.CurrentPage);
            Assert.Equal("五十音 1/1", plan.ProgressLabel);
            nav.CompleteTransition();
            Assert.Equal(1L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaAttempts;"));
            Assert.Equal(1L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaMemories WHERE ReviewCount=1;"));
        });

        [Fact]
        public Task CancelBeforeWrite_LeavesOriginalQuestionAvailableAndNoObservation() => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            var gate = new SubmissionGate { BeforeWrite = true };
            using var provider = await CreateAsync(directory.Path, gate);
            await provider.GetRequiredService<IKanaPlanEditor>().SaveAsync(new(true, true, false, true, false, false, false, 1), Token);
            var vm = provider.GetRequiredService<KanaSessionViewModel>();
            await vm.StartAsync(Token);
            var question = vm.Question;
            vm.AnswerText = "错误";
            var pending = vm.SubmitAsync(Token);
            await gate.Entered.Task.WaitAsync(Token);
            vm.CancelOperation(); gate.Release.TrySetResult(); await pending;
            Assert.Null(vm.Feedback);
            Assert.Same(question, vm.Question);
            Assert.True(vm.SubmitCommand.CanExecute(null));
            Assert.Equal(0L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaAttempts;"));
            Assert.Equal(0L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaMemories;"));
        });

        [Theory]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("1.5")]
        [InlineData("2147483648")]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("215")]
        public Task InvalidTextBoxQuantity_NeverSavesOldValueOrStartsAndValidCorrectionWorks(string invalid) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            var home = provider.GetRequiredService<HomeViewModel>();
            var nav = provider.GetRequiredService<NavigationService>();
            nav.InitializeWorkspace(home);
            await home.OpenKanaAsync(Token); nav.CompleteTransition();
            var vm = Assert.IsType<KanaPlanViewModel>(nav.CurrentPage);
            vm.Enabled = true;
            var page = new KanaPlanPage { DataContext = vm, Width = 960, Height = 720 };
            Render(page);
            var quantity = Assert.IsType<TextBox>(page.FindName("DailyQuantity"));
            quantity.SetCurrentValue(TextBox.TextProperty, invalid);
            quantity.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            await vm.SaveAsync(Token);
            Assert.NotNull(vm.ErrorMessage);
            Assert.Equal(0L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaPlans;"));
            await vm.StartAsync(Token);
            Assert.NotNull(vm.ErrorMessage);
            Assert.Same(vm, nav.CurrentPage);
            Assert.Equal(0L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaSessions;"));
            quantity.SetCurrentValue(TextBox.TextProperty, "3");
            quantity.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
            await vm.SaveAsync(Token);
            Assert.Null(vm.ErrorMessage);
            Assert.Equal(3, (await provider.GetRequiredService<IKanaPlanReader>().LoadAsync(Token)).DailyTarget);
            await vm.StartAsync(Token);
            Assert.Equal(3, Assert.IsType<KanaSessionViewModel>(nav.CurrentPage).Session!.TotalCount);
        });

        [Fact]
        public async Task SubmissionRead_ValidatesKeysAndReadsOnlyPersistedFeedbackWithoutWriting()
        {
            using var directory = new TemporaryDirectory();
            using var provider = await CreateAsync(directory.Path);
            await provider.GetRequiredService<IKanaPlanEditor>().SaveAsync(new(true, true, false, true, false, false, false, 1), Token);
            var operations = provider.GetRequiredService<IKanaSessionOperations>();
            var reader = provider.GetRequiredService<IKanaSessionReader>();
            var session = (await operations.StartOrResumeAsync(Token))!;
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.GetSubmissionFeedbackAsync(session.SessionId, -1, Token));
            await Assert.ThrowsAsync<InvalidOperationException>(() => reader.GetSubmissionFeedbackAsync("unknown", 0, Token));
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.GetSubmissionFeedbackAsync(session.SessionId, 0, cancelled.Token));
            Assert.Null(await reader.GetSubmissionFeedbackAsync(session.SessionId, 0, Token));
            await operations.SubmitAsync(session.SessionId, 0, "错误", Token);
            for (var index = 0; index < 2; index++)
            {
                var restored = Assert.IsType<KanaFeedbackDto>(await reader.GetSubmissionFeedbackAsync(session.SessionId, 0, Token));
                Assert.False(restored.IsCorrect);
                Assert.False(restored.IsCompleted);
                Assert.Equal(0, restored.CompletedCount);
                Assert.Equal(1, restored.TotalCount);
                Assert.Equal(1, restored.NextQuestion!.QuestionOrdinal);
                Assert.Equal("错误", Assert.Single(restored.Errors).SubmittedText);
            }
            Assert.Equal(1L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaAttempts;"));
            Assert.Equal(0L, await ScalarAsync(provider, "SELECT COUNT(*) FROM KanaMemories;"));
        }

        private static Task<ServiceProvider> CreateAsync(string root, SubmissionGate? gate = null) =>
            ReviewPlanPresentationTestHost.CreateAsync(root, createLexicons: false, configure: services =>
            {
                services.AddSingleton<Dictation.Core.Tts.ITtsProvider>(new LearningPresentationTestEnvironment.ControlledTtsProvider());
                services.AddSingleton<Dictation.Presentation.Audio.IAudioPlayer>(new LearningPresentationTestEnvironment.ControlledAudioPlayer());
                if (gate is not null) services.AddSingleton<IKanaSessionOperations>(provider =>
                    new DelayedSubmission((IKanaSessionOperations)provider.GetRequiredService<IKanaSessionReader>(), gate));
            });
        private static async Task<long> ScalarAsync(ServiceProvider provider, string sql)
        {
            using var connection = provider.GetRequiredService<SqliteConnectionFactory>().CreateConnection();
            await connection.OpenAsync(Token); return await connection.ExecuteScalarAsync<long>(sql);
        }
        private static void Render(FrameworkElement page)
        {
            page.Measure(new Size(960, 720)); page.Arrange(new Rect(0, 0, 960, 720)); page.UpdateLayout();
        }
        /// <summary>独立清音事实作为状态边界测试的合法正确输入，不以生产Catalog生成期望。</summary>
        private static string Answer(KanaQuestionDto question) =>
            "あ い う え お か き く け こ さ し す せ そ た ち つ て と な に ぬ ね の は ひ ふ へ ほ ま み む め も や ゆ よ ら り る れ ろ わ を ん"
                .Split(' ').Single(text => $"Hiragana:{text}" == question.KanaId);
        /// <summary>只控制真实业务调用前／后响应的时序，不替代数据库写入或判题。</summary>
        private sealed class SubmissionGate
        {
            internal bool LoseResponse { get; init; }
            internal bool BeforeWrite { get; init; }
            internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        /// <summary>委派给真实Core，仅模拟已提交响应迟到或丢失的边界。</summary>
        private sealed class DelayedSubmission(IKanaSessionOperations inner, SubmissionGate gate) : IKanaSessionOperations
        {
            public Task<KanaSessionDto?> StartOrResumeAsync(CancellationToken cancellationToken = default) => inner.StartOrResumeAsync(cancellationToken);
            public async Task<KanaFeedbackDto> SubmitAsync(string sessionId, int questionOrdinal, string? submittedText, CancellationToken cancellationToken = default)
            {
                if (gate.BeforeWrite)
                {
                    gate.Entered.TrySetResult(); await gate.Release.Task;
                }
                var feedback = await inner.SubmitAsync(sessionId, questionOrdinal, submittedText, cancellationToken);
                if (!gate.BeforeWrite)
                {
                    gate.Entered.TrySetResult(); await gate.Release.Task;
                }
                if (gate.LoseResponse) throw new IOException("提交已保存，但响应丢失。");
                return feedback;
            }
        }
    }
}
