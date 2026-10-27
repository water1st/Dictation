using System.Net.Http;
using System.Text;
using Dictation.DataAccess.Runtime;
using Dictation.Tests.Infrastructure;
using Dictation.Core.Updating;
using Dictation.Presentation.Updating;
using Dictation.Presentation.ViewModels;

namespace Dictation.Tests.Presentation
{
    /// <summary>验证检查只发布按钮，用户点击后才准备更新且只有成功启动runner才关闭应用。</summary>
    public sealed class UpdateViewModelTests : IDisposable
    {
        private readonly TemporaryDirectory directory = new();

        private UpdateViewModel CreateViewModel(IUpdateReleaseQuery query, IUpdatePreparation preparation, IUpdateLauncher launcher)
        {
            return new UpdateViewModel(query, preparation, launcher, new ApplicationPaths(directory.Path));
        }

        public void Dispose() => directory.Dispose();

        [Theory]
        [InlineData("检查")]
        [InlineData("准备")]
        [InlineData("启动")]
        public async Task 错误日志_更新异常_保存操作时间和完整异常且只关联最新文件(string operation)
        {
            var query = new Query { Failure = new HttpRequestException("错误 A", new IOException("内部原因")) };
            var preparation = new Preparation();
            var launcher = new Launcher();
            using var viewModel = CreateViewModel(query, preparation, launcher);
            var changes = new List<string?>();
            viewModel.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
            var before = DateTimeOffset.UtcNow;
            await viewModel.CheckAsync();
            var first = Assert.IsType<string>(viewModel.CurrentErrorLogPath);
            Assert.Equal(Path.Combine(directory.Path, "LOGS"), Path.GetDirectoryName(first));
            var firstText = File.ReadAllText(first, new UTF8Encoding(false, true));
            Assert.Contains("更新检查", firstText);
            Assert.Contains("错误 A", firstText);
            Assert.Contains("内部原因", firstText);
            Assert.Contains("HttpRequestException", firstText);
            Assert.Contains("CheckAsync", firstText);
            var timestamp = DateTimeOffset.Parse(firstText.Split('\n')[0][3..].Trim());
            Assert.InRange(timestamp, before, DateTimeOffset.UtcNow);
            Assert.Contains(nameof(UpdateViewModel.CurrentErrorLogPath), changes);

            query.Failure = null;
            if (operation == "检查")
            {
                query.Failure = new IOException("错误 B");
                await viewModel.CheckAsync();
            }
            else
            {
                await viewModel.CheckAsync();
                if (operation == "准备") preparation.Failure = new InvalidDataException("错误 B");
                else launcher.Failure = new InvalidOperationException("错误 B");
                await viewModel.InstallAsync();
            }

            var second = Assert.IsType<string>(viewModel.CurrentErrorLogPath);
            Assert.NotEqual(first, second);
            var secondText = File.ReadAllText(second, new UTF8Encoding(false, true));
            Assert.Contains(operation == "检查" ? "更新检查" : "更新准备/启动", secondText);
            Assert.Contains("错误 B", secondText);
            Assert.DoesNotContain("错误 A", secondText);
            Assert.Equal(firstText, File.ReadAllText(first));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task 错误日志_重新检查或更新_操作开始立即清除旧关联(bool check)
        {
            var query = new Query();
            var preparation = new Preparation { Failure = new IOException("下载失败") };
            using var viewModel = CreateViewModel(query, preparation, new Launcher());
            Assert.Null(viewModel.CurrentErrorLogPath);
            await viewModel.CheckAsync();
            await viewModel.InstallAsync();
            Assert.NotNull(viewModel.CurrentErrorLogPath);
            preparation.Failure = null;
            var checking = new TaskCompletionSource<AvailableUpdateDto?>();
            var preparing = new TaskCompletionSource<PreparedUpdateDto>();
            query.Pending = checking.Task;
            preparation.Pending = preparing.Task;
            var operation = check ? viewModel.CheckAsync() : viewModel.InstallAsync();
            Assert.Null(viewModel.CurrentErrorLogPath);
            Assert.False(viewModel.HasError);
            checking.SetResult(null);
            preparing.SetResult(Prepared);
            await operation;
            Assert.Null(viewModel.CurrentErrorLogPath);
        }

        [Fact]
        public async Task 错误日志_日志目录不可写_保留原错误且不关联无效文件()
        {
            File.WriteAllText(Path.Combine(directory.Path, "LOGS"), "阻止创建目录");
            using var viewModel = CreateViewModel(new Query { Failure = new IOException("连接失败") }, new Preparation(), new Launcher());
            await viewModel.CheckAsync();
            Assert.True(viewModel.HasError);
            Assert.Contains("连接失败", viewModel.StatusMessage);
            Assert.Contains("日志", viewModel.StatusMessage);
            Assert.Null(viewModel.CurrentErrorLogPath);
        }

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
            using var viewModel = CreateViewModel(query, preparation, launcher);
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
            using var viewModel = CreateViewModel(new Query { Result = null }, new Preparation(), new Launcher());
            await viewModel.CheckAsync();
            Assert.False(viewModel.HasUpdate);
            Assert.Equal(string.Empty, viewModel.StatusMessage);
            Assert.False(viewModel.HasError);
        }

        [Fact]
        public async Task Check_NetworkFailureIsInlineAndDoesNotRequestShutdown()
        {
            using var viewModel = CreateViewModel(new Query { Failure = new HttpRequestException("offline") }, new Preparation(), new Launcher());
            var shutdowns = 0;
            viewModel.ShutdownRequested += (_, _) => shutdowns++;
            await viewModel.CheckAsync();
            Assert.False(viewModel.HasUpdate);
            Assert.NotEmpty(viewModel.StatusMessage);
            Assert.True(viewModel.HasError);
            Assert.Equal(0, shutdowns);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task 更新检查_长错误后重新检查_立即清除错误并通知绑定(bool hasUpdate)
        {
            var query = new Query { Failure = new HttpRequestException(new string('错', 2048)) };
            using var viewModel = CreateViewModel(query, new Preparation(), new Launcher());
            var changes = new List<string?>();
            viewModel.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
            await viewModel.CheckAsync();
            Assert.True(viewModel.HasError);
            Assert.Contains(new string('错', 2048), viewModel.StatusMessage);
            Assert.Contains(nameof(UpdateViewModel.HasError), changes);

            var pending = new TaskCompletionSource<AvailableUpdateDto?>();
            query.Failure = null;
            query.Pending = pending.Task;
            changes.Clear();
            var checking = viewModel.CheckAsync();
            Assert.False(viewModel.HasError);
            Assert.Empty(viewModel.StatusMessage);
            Assert.Contains(nameof(UpdateViewModel.HasError), changes);
            pending.SetResult(hasUpdate ? Available : null);
            await checking;
            Assert.False(viewModel.HasError);
            Assert.Equal(hasUpdate, viewModel.HasUpdate);
        }

        [Fact]
        public async Task 更新准备_失败后重试_准备期间清除旧错误()
        {
            var preparation = new Preparation { Failure = new InvalidDataException("校验失败") };
            using var viewModel = CreateViewModel(new Query(), preparation, new Launcher());
            await viewModel.CheckAsync();
            await viewModel.InstallAsync();
            Assert.True(viewModel.HasError);
            var pending = new TaskCompletionSource<PreparedUpdateDto>();
            preparation.Failure = null;
            preparation.Pending = pending.Task;
            var installing = viewModel.InstallAsync();
            Assert.True(viewModel.IsBusy);
            Assert.False(viewModel.HasError);
            Assert.Empty(viewModel.StatusMessage);
            pending.SetResult(Prepared);
            await installing;
            Assert.False(viewModel.HasError);
        }

        [Fact]
        public async Task Install_BeforeOfferDoesNotPrepareOrLaunchAnything()
        {
            var preparation = new Preparation();
            var launcher = new Launcher();
            using var viewModel = CreateViewModel(new Query(), preparation, launcher);
            await viewModel.InstallAsync();
            Assert.Equal(0, preparation.Calls);
            Assert.Null(launcher.Launched);
        }

        [Fact]
        public async Task Install_ValidPreparationLaunchesExactRunnerThenRequestsShutdown()
        {
            var preparation = new Preparation();
            var launcher = new Launcher();
            using var viewModel = CreateViewModel(new Query(), preparation, launcher);
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
            using var viewModel = CreateViewModel(new Query(), preparation, launcher);
            var shutdowns = 0;
            viewModel.ShutdownRequested += (_, _) => shutdowns++;
            await viewModel.CheckAsync();
            await viewModel.InstallAsync();
            Assert.True(viewModel.HasUpdate);
            Assert.False(viewModel.IsBusy);
            Assert.NotEmpty(viewModel.StatusMessage);
            Assert.True(viewModel.HasError);
            Assert.Equal(0, shutdowns);
        }

        [Fact]
        public async Task Dispose_IgnoredCancellationDoesNotPublishLateOffer()
        {
            var pending = new TaskCompletionSource<AvailableUpdateDto?>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var viewModel = CreateViewModel(new Query { Pending = pending.Task }, new Preparation(), new Launcher());
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
            internal Exception? Failure { get; set; }
            internal Task<AvailableUpdateDto?>? Pending { get; set; }
            public Task<AvailableUpdateDto?> CheckAsync(CancellationToken cancellationToken = default) => Failure is null
                ? Pending ?? Task.FromResult(Result) : Task.FromException<AvailableUpdateDto?>(Failure);
        }

        /// <summary>替代外部下载并记录真实VM选中的发行，失败不能触发关闭。</summary>
        private sealed class Preparation : IUpdatePreparation
        {
            internal Exception? Failure { get; set; }
            internal Task<PreparedUpdateDto>? Pending { get; set; }
            internal int Calls { get; private set; }
            internal AvailableUpdateDto? Request { get; private set; }
            public Task<PreparedUpdateDto> PrepareAsync(AvailableUpdateDto update, CancellationToken cancellationToken = default)
            {
                Calls++;
                Request = update;
                return Failure is null ? Pending ?? Task.FromResult(Prepared) : Task.FromException<PreparedUpdateDto>(Failure);
            }
        }

        /// <summary>替代创建外部进程，验证VM只传递已准备的同一收据。</summary>
        private sealed class Launcher : IUpdateLauncher
        {
            internal Exception? Failure { get; set; }
            internal PreparedUpdateDto? Launched { get; private set; }
            public void Launch(PreparedUpdateDto update)
            {
                if (Failure is not null) throw Failure;
                Launched = update;
            }
        }
    }
}
