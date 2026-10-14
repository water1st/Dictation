using System.Net.Http;
using Dictation.Core.Updating;
using Dictation.Presentation.Updating;
using Dictation.Presentation.ViewModels;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证检查只发布按钮，用户点击后才准备更新且只有成功启动runner才关闭应用。</summary>
    public sealed class UpdateViewModelTests
    {
        private static readonly AvailableUpdateDto Available = new("1.0.2",
            new("https://github.com/water1st/Dictation/releases/download/v1.0.2/Dictation-win-x64.zip"),
            new("https://github.com/water1st/Dictation/releases/download/v1.0.2/Dictation-win-x64.zip.sha256"));
        private static readonly PreparedUpdateDto Prepared = new("job", "runner", "zip", new string('A', 64), "1.0.2", "root");

        [Fact]
        public async Task Check_NewVersionShowsButtonWithoutPreparingOrRequestingShutdown()
        {
            var query = new Query();
            var preparation = new Preparation();
            var launcher = new Launcher();
            using var viewModel = new UpdateViewModel(query, preparation, launcher);
            var shutdowns = 0;
            viewModel.ShutdownRequested += (_, _) => shutdowns++;
            Assert.False(viewModel.HasUpdate);
            await viewModel.CheckAsync();
            Assert.True(viewModel.HasUpdate);
            Assert.Contains("1.0.2", viewModel.ButtonText);
            Assert.Equal(0, preparation.Calls);
            Assert.Null(launcher.Launched);
            Assert.Equal(0, shutdowns);
        }

        [Fact]
        public async Task Check_NoNewVersionKeepsButtonHidden()
        {
            using var viewModel = new UpdateViewModel(new Query { Result = null }, new Preparation(), new Launcher());
            await viewModel.CheckAsync();
            Assert.False(viewModel.HasUpdate);
            Assert.Equal(string.Empty, viewModel.StatusMessage);
        }

        [Fact]
        public async Task Check_NetworkFailureIsInlineAndDoesNotRequestShutdown()
        {
            using var viewModel = new UpdateViewModel(new Query { Failure = new HttpRequestException("offline") }, new Preparation(), new Launcher());
            var shutdowns = 0;
            viewModel.ShutdownRequested += (_, _) => shutdowns++;
            await viewModel.CheckAsync();
            Assert.False(viewModel.HasUpdate);
            Assert.NotEmpty(viewModel.StatusMessage);
            Assert.Equal(0, shutdowns);
        }

        [Fact]
        public async Task Install_BeforeOfferDoesNotPrepareOrLaunchAnything()
        {
            var preparation = new Preparation();
            var launcher = new Launcher();
            using var viewModel = new UpdateViewModel(new Query(), preparation, launcher);
            await viewModel.InstallAsync();
            Assert.Equal(0, preparation.Calls);
            Assert.Null(launcher.Launched);
        }

        [Fact]
        public async Task Install_ValidPreparationLaunchesExactRunnerThenRequestsShutdown()
        {
            var preparation = new Preparation();
            var launcher = new Launcher();
            using var viewModel = new UpdateViewModel(new Query(), preparation, launcher);
            var shutdowns = 0;
            viewModel.ShutdownRequested += (_, _) => shutdowns++;
            await viewModel.CheckAsync();
            await viewModel.InstallAsync();
            Assert.Same(Available, preparation.Request);
            Assert.Same(Prepared, launcher.Launched);
            Assert.Equal(1, shutdowns);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Install_PreparationOrLaunchFailureKeepsApplicationOpenAndAllowsRetry(bool failPreparation)
        {
            var preparation = new Preparation { Failure = failPreparation ? new InvalidDataException("bad hash") : null };
            var launcher = new Launcher { Failure = failPreparation ? null : new InvalidOperationException("cannot launch") };
            using var viewModel = new UpdateViewModel(new Query(), preparation, launcher);
            var shutdowns = 0;
            viewModel.ShutdownRequested += (_, _) => shutdowns++;
            await viewModel.CheckAsync();
            await viewModel.InstallAsync();
            Assert.True(viewModel.HasUpdate);
            Assert.False(viewModel.IsBusy);
            Assert.NotEmpty(viewModel.StatusMessage);
            Assert.Equal(0, shutdowns);
        }

        [Fact]
        public async Task Dispose_IgnoredCancellationDoesNotPublishLateOffer()
        {
            var pending = new TaskCompletionSource<AvailableUpdateDto?>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var viewModel = new UpdateViewModel(new Query { Pending = pending.Task }, new Preparation(), new Launcher());
            var checking = viewModel.CheckAsync();
            viewModel.Dispose();
            pending.SetResult(Available);
            await checking;
            Assert.False(viewModel.HasUpdate);
        }

        /// <summary>只替代远程查询能力，用例状态与事件由真实ViewModel产生。</summary>
        private sealed class Query : IUpdateReleaseQuery
        {
            internal AvailableUpdateDto? Result { get; set; } = Available;
            internal Exception? Failure { get; init; }
            internal Task<AvailableUpdateDto?>? Pending { get; init; }
            public Task<AvailableUpdateDto?> CheckAsync(CancellationToken cancellationToken = default) => Failure is null
                ? Pending ?? Task.FromResult(Result) : Task.FromException<AvailableUpdateDto?>(Failure);
        }

        /// <summary>替代外部下载并记录真实VM选中的发行，失败不能触发关闭。</summary>
        private sealed class Preparation : IUpdatePreparation
        {
            internal Exception? Failure { get; init; }
            internal int Calls { get; private set; }
            internal AvailableUpdateDto? Request { get; private set; }
            public Task<PreparedUpdateDto> PrepareAsync(AvailableUpdateDto update, CancellationToken cancellationToken = default)
            {
                Calls++;
                Request = update;
                return Failure is null ? Task.FromResult(Prepared) : Task.FromException<PreparedUpdateDto>(Failure);
            }
        }

        /// <summary>替代创建外部进程，验证VM只传递已准备的同一收据。</summary>
        private sealed class Launcher : IUpdateLauncher
        {
            internal Exception? Failure { get; init; }
            internal PreparedUpdateDto? Launched { get; private set; }
            public void Launch(PreparedUpdateDto update)
            {
                if (Failure is not null) throw Failure;
                Launched = update;
            }
        }
    }
}
