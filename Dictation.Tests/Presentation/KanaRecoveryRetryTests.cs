using Dapper;
using Dictation.Core.Kana;
using Dictation.Core.Tts;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation.Audio;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证提交状态暂时无法读取时禁止重复提交，并通过真实只读恢复解除。</summary>
    public sealed class KanaRecoveryRetryTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public Task UncertainSubmitAndBrokenRecovery_OnlyRetriesReadUntilStateIsKnown(bool beforeWrite) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            using var directory = new TemporaryDirectory();
            var faults = new BoundaryFaults { FailBeforeWrite = beforeWrite };
            using var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path, createLexicons: false, configure: services =>
            {
                services.AddSingleton<ITtsProvider>(new LearningPresentationTestEnvironment.ControlledTtsProvider());
                services.AddSingleton<IAudioPlayer>(new LearningPresentationTestEnvironment.ControlledAudioPlayer());
                services.AddSingleton<IKanaSessionReader>(provider => new ReadBoundary(provider.GetRequiredService<KanaSessionService>(), faults));
                services.AddSingleton<IKanaSessionOperations>(provider => new WriteBoundary(provider.GetRequiredService<KanaSessionService>(), faults));
            });
            await provider.GetRequiredService<IKanaPlanEditor>().SaveAsync(new(true, true, false, true, false, false, false, 1), token);
            var vm = provider.GetRequiredService<KanaSessionViewModel>();
            await vm.StartAsync(token);
            vm.AnswerText = "错误";
            await vm.SubmitAsync(token);
            Assert.True(vm.HasPendingFeedback);
            Assert.False(vm.SubmitCommand.CanExecute(null));
            Assert.NotNull(vm.ErrorMessage);
            Assert.True(vm.RecoverSubmissionCommand.CanExecute(null));
            var page = new KanaSessionPage { DataContext = vm, Width = 960, Height = 720 };
            page.Measure(new Size(960, 720)); page.Arrange(new Rect(0, 0, 960, 720)); page.UpdateLayout();
            Assert.Contains(Children<Button>(page), button => ReferenceEquals(button.Command, vm.RecoverSubmissionCommand) && button.Visibility == Visibility.Visible);
            await using var connection = provider.GetRequiredService<SqliteConnectionFactory>().CreateConnection();
            await connection.OpenAsync(token);
            Assert.Equal(beforeWrite ? 0L : 1L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM KanaAttempts;"));
            await vm.SubmitAsync(token);
            Assert.Equal(beforeWrite ? 0L : 1L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM KanaAttempts;"));
            faults.FailRead = false;
            await vm.RecoverSubmissionAsync(token);
            Assert.False(vm.HasPendingFeedback);
            Assert.Equal(!beforeWrite, vm.IsFeedbackVisible);
            if (beforeWrite)
            {
                Assert.True(vm.SubmitCommand.CanExecute(null));
                faults.FailBeforeWrite = false;
                await vm.SubmitAsync(token);
            }
            Assert.False(vm.Feedback!.IsCorrect);
            Assert.Equal("错误", Assert.Single(vm.Errors).SubmittedText);
            Assert.Equal(1L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM KanaAttempts;"));
            Assert.Equal(0L, await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM KanaMemories;"));
        });

        private static IEnumerable<T> Children<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T item) yield return item;
            for (var index = 0; index < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); index++)
                foreach (var child in Children<T>(System.Windows.Media.VisualTreeHelper.GetChild(root, index))) yield return child;
        }
        /// <summary>控制响应和只读边界失败，不篡改数据库或答案。</summary>
        private sealed class BoundaryFaults
        {
            internal bool FailBeforeWrite { get; set; }
            internal bool FailRead { get; set; } = true;
        }
        /// <summary>读取失败恢复后，所有查询仍委派给真实Core服务。</summary>
        private sealed class ReadBoundary(IKanaSessionReader inner, BoundaryFaults faults) : IKanaSessionReader
        {
            public Task<KanaQuestionDto?> GetCurrentQuestionAsync(string sessionId, CancellationToken cancellationToken = default) => inner.GetCurrentQuestionAsync(sessionId, cancellationToken);
            public Task<KanaFeedbackDto?> GetSubmissionFeedbackAsync(string sessionId, int questionOrdinal, CancellationToken cancellationToken = default) =>
                faults.FailRead ? Task.FromException<KanaFeedbackDto?>(new IOException("暂时无法读取提交结果。"))
                    : inner.GetSubmissionFeedbackAsync(sessionId, questionOrdinal, cancellationToken);
        }
        /// <summary>模拟提交前失败或真实事务成功后丢失响应，绝不模拟写入成功。</summary>
        private sealed class WriteBoundary(IKanaSessionOperations inner, BoundaryFaults faults) : IKanaSessionOperations
        {
            public Task<KanaSessionDto?> StartOrResumeAsync(CancellationToken cancellationToken = default) => inner.StartOrResumeAsync(cancellationToken);
            public async Task<KanaFeedbackDto> SubmitAsync(string sessionId, int questionOrdinal, string? submittedText, CancellationToken cancellationToken = default)
            {
                if (faults.FailBeforeWrite) throw new IOException("提交前连接失败。");
                await inner.SubmitAsync(sessionId, questionOrdinal, submittedText, cancellationToken);
                throw new IOException("真实提交已保存，响应丢失。");
            }
        }
    }
}
