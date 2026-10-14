using Dapper;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Tts;
using Dictation.DataAccess.Runtime;
using Dictation.Presentation.Audio;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.ViewModels;
using Dictation.Presentation.Views;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证原共用页面呈现共享学习的真实反馈、总结及首页词组计数。</summary>
    public sealed class LearningSharedDictationPresentationTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public Task SharedLearning_OriginalPageCountsActualAnswersAndTwoGroups(bool firstWrong) => ReviewPlanPageTests.RunStaAsync(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            using var directory = new TemporaryDirectory();
            using var provider = await ReviewPlanPresentationTestHost.CreateAsync(directory.Path, configure: services =>
            {
                services.AddSingleton<ITtsProvider>(new LearningPresentationTestEnvironment.ControlledTtsProvider());
                services.AddSingleton<IAudioPlayer>(new LearningPresentationTestEnvironment.ControlledAudioPlayer());
            });
            var staged = await provider.GetRequiredService<IImportStaging>().StageAsync(new(LearningLanguage.Japanese, ImportSourceKind.Manual,
                new[] { new ImportSelectionDto("食べる", "jmdict:1:1"), new ImportSelectionDto("食べる", "jmdict:1:2") }), token);
            await provider.GetRequiredService<IImportInboxActions>().DecideAsync(new(staged.Batch.BatchId,
                staged.Batch.Items.Select(item => item.ItemId).ToArray(), ImportDecision.Learning), token);
            await provider.GetRequiredService<ILearningPlanEditor>().SaveAsync(new(LearningLanguage.Japanese, 20, 20,
                LearningSelectionMode.Sequential, EnglishFormLabelMode.GrammarName, null, new[] { "Dictionary" },
                new[] { LearningChannel.Dictation }, new[] { LearningOutputMode.Kana }), token);
            var home = provider.GetRequiredService<HomeViewModel>();
            await home.InitializeAsync(token);
            var navigation = provider.GetRequiredService<NavigationService>();
            navigation.InitializeHome(home);
            await home.OpenLearningAsync(token);
            var vm = Assert.IsType<PracticeSessionViewModel>(navigation.CurrentPage);
            var page = new PracticeSessionPage { DataContext = vm };
            vm.OpenBrowseCommand.Execute(null);
            Assert.Equal(2, vm.BrowseMembers.Count);
            await using var connection = provider.GetRequiredService<SqliteConnectionFactory>().CreateConnection();
            await connection.OpenAsync(token);
            Assert.Equal(0, await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM LearningQuestionAttempts;"));
            vm.CloseBrowseCommand.Execute(null);
            foreach (var text in firstWrong ? new[] { "错误", "たべる", "たべる" } : new[] { "たべる", "たべる" })
            {
                vm.AnswerText = text;
                Assert.True(vm.SubmitCommand.CanExecute(null));
                await vm.SubmitAsync(token);
                Assert.Equal(text != "错误", vm.Feedback!.WasCorrect);
                Assert.Equal("たべる", vm.Feedback.CorrectAnswer);
                Assert.Equal(firstWrong, vm.Feedback.HadErrorInSession);
                await vm.ContinueAsync(token);
            }
            Assert.True(vm.IsCompleted);
            Assert.Equal(firstWrong ? 3 : 2, vm.Summary!.AnsweredCount);
            Assert.Equal(2, vm.Summary.CompletedWordGroupCount);
            Assert.Equal(firstWrong ? 2 : 0, vm.Summary.Errors.Count);
            page.Measure(new Size(1100, 1000)); page.Arrange(new Rect(0, 0, 1100, 1000)); page.UpdateLayout();
            Assert.Equal(Visibility.Visible, Assert.IsType<Border>(page.FindName("SummaryPanel")).Visibility);
            Assert.Equal("完成 2 个词组", Assert.IsType<TextBlock>(page.FindName("CompletedGroupCount")).Text);
            navigation.CompleteTransition();
            await navigation.GoBackAsync(token);
            navigation.CompleteTransition();
            Assert.Same(home, navigation.CurrentPage);
            Assert.Equal("学习 2/20", home.LearningProgressLabel);
            Assert.Equal("复习 0/20", home.ReviewProgressLabel);
        });
    }
}
