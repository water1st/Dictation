using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Dictation.Core.Configuration;
using Dictation.Presentation.Mcp;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>协调本机监听设置和用户主动的Codex注册，界面不读写配置文件。</summary>
    public sealed class McpSettingsViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly McpSettingsService settings;
        private readonly IMcpHostControl control;
        private readonly IMcpStatusReader status;
        private readonly CodexMcpRegistrationService registration;
        private bool enabled;
        private string portText = "47821";
        private string endpointText = "http://127.0.0.1:47821/mcp";
        private string runtimeStatusLabel = "已关闭";
        private string? errorMessage;
        private string? statusMessage;
        private string registrationMessage = "尚未注册";
        private bool isBusy;
        private bool isDisposed;
        private CancellationTokenSource? operation;
        private int generation;

        /// <summary>组合配置、监听控制、只读状态和客户端注册服务。</summary>
        /// <param name="settings">本应用配置读取和保存。</param>
        /// <param name="control">唯一Host的监听控制。</param>
        /// <param name="status">实际监听状态。</param>
        /// <param name="registration">只操作本应用Codex项。</param>
        /// <param name="configPath">组合根解析或测试明确提供的客户端配置路径。</param>
        public McpSettingsViewModel(McpSettingsService settings, IMcpHostControl control, IMcpStatusReader status,
            CodexMcpRegistrationService registration, string configPath)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.control = control ?? throw new ArgumentNullException(nameof(control));
            this.status = status ?? throw new ArgumentNullException(nameof(status));
            this.registration = registration ?? throw new ArgumentNullException(nameof(registration));
            ArgumentException.ThrowIfNullOrWhiteSpace(configPath);
            ConfigPath = configPath;
            SaveCommand = new AsyncCommand(() => SaveAsync(), CanOperate);
            RetryCommand = new AsyncCommand(() => RetryAsync(), CanOperate);
            RegisterCommand = new AsyncCommand(() => RegisterAsync(), CanOperate);
            UnregisterCommand = new AsyncCommand(() => UnregisterAsync(), CanOperate);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public bool Enabled { get => enabled; set => Set(ref enabled, value); }
        public string PortText { get => portText; set => Set(ref portText, value); }
        public string ConfigPath { get; }
        public string EndpointText { get => endpointText; private set => Set(ref endpointText, value); }
        public string RuntimeStatusLabel { get => runtimeStatusLabel; private set => Set(ref runtimeStatusLabel, value); }
        public string? ErrorMessage { get => errorMessage; private set => Set(ref errorMessage, value); }
        public string? StatusMessage { get => statusMessage; private set => Set(ref statusMessage, value); }
        public string RegistrationMessage { get => registrationMessage; private set => Set(ref registrationMessage, value); }
        public bool IsBusy { get => isBusy; private set { Set(ref isBusy, value); NotifyCommands(); } }
        public ICommand SaveCommand { get; }
        public ICommand RetryCommand { get; }
        public ICommand RegisterCommand { get; }
        public ICommand UnregisterCommand { get; }

        /// <summary>读取设置及只读注册状态，不自动写客户端配置。</summary>
        /// <param name="cancellationToken">取消读取。</param>
        public Task RefreshAsync(CancellationToken cancellationToken = default) => RunAsync(async token =>
        {
            var saved = settings.Load();
            Enabled = saved.Enabled;
            PortText = saved.Port.ToString(CultureInfo.InvariantCulture);
            UpdateRuntime();
            await ReadRegistrationAsync(token);
        }, cancellationToken);
        /// <summary>校验原始端口文本，保存后应用监听开关。</summary>
        /// <param name="cancellationToken">取消保存和启动。</param>
        public Task SaveAsync(CancellationToken cancellationToken = default) => RunAsync(async token =>
        {
            if (!int.TryParse(PortText, NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port is < 1024 or > 65535)
            {
                ErrorMessage = "请输入1024至65535之间的整数端口；设置未保存。";
                return;
            }
            var saved = new McpSettingsDto(Enabled, port);
            await settings.SaveAsync(saved, token);
            token.ThrowIfCancellationRequested();
            await control.ApplyAsync(saved, token);
            token.ThrowIfCancellationRequested();
            StatusMessage = "设置已保存。";
            UpdateRuntime();
            await ReadRegistrationAsync(token);
        }, cancellationToken);
        /// <summary>按已保存设置重试，不保存尚未提交的编辑。</summary>
        /// <param name="cancellationToken">取消重试。</param>
        public Task RetryAsync(CancellationToken cancellationToken = default) => RunAsync(async token =>
        {
            await control.ApplyAsync(settings.Load(), token);
            token.ThrowIfCancellationRequested();
            UpdateRuntime();
            await ReadRegistrationAsync(token);
        }, cancellationToken);
        /// <summary>用户主动注册当前运行端点。</summary>
        /// <param name="cancellationToken">取消写入。</param>
        public Task RegisterAsync(CancellationToken cancellationToken = default) => RunAsync(async token =>
        {
            var current = status.GetStatus();
            if (current.State != McpRunState.Running)
            {
                ErrorMessage = "请先保存并成功开启MCP服务，再注册当前端点。";
                return;
            }
            var result = await registration.RegisterAsync(ConfigPath, current.Endpoint, token);
            token.ThrowIfCancellationRequested();
            StatusMessage = result.Message;
            await ReadRegistrationAsync(token);
        }, cancellationToken);
        /// <summary>用户主动注销本应用项，不能移除其它服务器。</summary>
        /// <param name="cancellationToken">取消写入。</param>
        public Task UnregisterAsync(CancellationToken cancellationToken = default) => RunAsync(async token =>
        {
            var result = await registration.UnregisterAsync(ConfigPath, token);
            token.ThrowIfCancellationRequested();
            StatusMessage = result.Message;
            await ReadRegistrationAsync(token);
        }, cancellationToken);
        /// <summary>离开页面时取消操作，迟到结果不更新旧页面。</summary>
        public void CancelOperation()
        {
            generation++;
            operation?.Cancel();
            IsBusy = false;
        }
        /// <inheritdoc />
        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;
            CancelOperation();
        }

        /// <summary>独立呈现实际监听结果，不能把保存成功误报为服务运行成功。</summary>
        private void UpdateRuntime()
        {
            var current = status.GetStatus();
            EndpointText = current.Endpoint.AbsoluteUri;
            RuntimeStatusLabel = current.State switch
            {
                McpRunState.Disabled => "已关闭", McpRunState.Starting => "正在启动",
                McpRunState.Running => "运行中", McpRunState.Failed => "启动失败", _ => "正在关闭"
            };
            ErrorMessage = current.ErrorMessage;
        }

        /// <summary>只读注册状态；旧端点需要用户主动注销及重新注册。</summary>
        /// <param name="token">当前页面操作的取消令牌。</param>
        private async Task ReadRegistrationAsync(CancellationToken token)
        {
            var result = await registration.ReadAsync(ConfigPath, token);
            token.ThrowIfCancellationRequested();
            RegistrationMessage = result.Endpoint is null ? "尚未注册。"
                : result.Endpoint.AbsoluteUri == EndpointText ? "当前端点已注册；请重载客户端配置。"
                : "需要更新Codex注册：请先注销旧端点，再注册当前端点。";
        }

        /// <summary>串行执行页面操作，取消及迟到完成不修改已离开的页面。</summary>
        /// <param name="action">注入服务上的本次操作。</param>
        /// <param name="cancellationToken">调用方取消令牌。</param>
        private async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            if (IsBusy) return;
            using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            operation = source;
            var current = ++generation;
            IsBusy = true;
            ErrorMessage = null;
            try
            {
                await action(source.Token);
            }
            catch (OperationCanceledException) when (source.IsCancellationRequested) { }
            catch (Exception exception)
            {
                if (current == generation && !isDisposed)
                    ErrorMessage = exception is SettingsConfigurationException or SettingsPersistenceException
                        ? exception.Message : "操作失败，请检查设置后重试。";
            }
            finally
            {
                if (ReferenceEquals(operation, source)) operation = null;
                if (current == generation && !isDisposed) IsBusy = false;
            }
        }

        /// <summary>所有设置命令共享操作互斥，不影响后台监听本身。</summary>
        private bool CanOperate() => !isDisposed && !IsBusy;

        /// <summary>通知四个独立命令重新计算可用状态。</summary>
        private void NotifyCommands()
        {
            foreach (var command in new[] { SaveCommand, RetryCommand, RegisterCommand, UnregisterCommand })
                ((AsyncCommand)command).NotifyCanExecuteChanged();
        }

        /// <summary>通知输入字段变化，不执行业务。</summary>
        private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
