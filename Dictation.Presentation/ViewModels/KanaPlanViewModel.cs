using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using Dictation.Core.Kana;
using Dictation.Presentation.Navigation;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>协调独立五十音范围、每日目标及同窗口听写导航，不执行记忆规则。</summary>
    public sealed class KanaPlanViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly IKanaPlanReader reader;
        private readonly IKanaPlanEditor editor;
        private readonly IKanaProgressReader progress;
        private readonly IKanaSessionNavigation navigation;
        private readonly List<AsyncCommand> commands = new();
        private CancellationTokenSource? operation;
        private bool disposed;
        private bool enabled;
        private bool hiragana = true;
        private bool katakana;
        private bool clear = true;
        private bool voiced;
        private bool contracted;
        private bool reviewAll;
        private int dailyTarget = 20;
        private string dailyTargetText = "20";
        private bool hasActiveSession;

        /// <summary>组合计划读写、只读进度和下一层导航能力。</summary>
        /// <param name="reader">读取已保存计划。</param>
        /// <param name="editor">校验并保存计划。</param>
        /// <param name="progress">读取独立每日计数和活动轮次。</param>
        /// <param name="navigation">在当前窗口进入听写。</param>
        public KanaPlanViewModel(IKanaPlanReader reader, IKanaPlanEditor editor, IKanaProgressReader progress, IKanaSessionNavigation navigation)
        {
            this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
            this.editor = editor ?? throw new ArgumentNullException(nameof(editor));
            this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
            this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            RefreshCommand = Command(() => RefreshAsync(), () => !IsBusy);
            SaveCommand = Command(() => SaveAsync(), () => !IsBusy);
            StartCommand = Command(() => StartAsync(), () => !IsBusy && (Enabled || hasActiveSession));
            CancelCommand = Command(() => { CancelOperation(); return Task.CompletedTask; }, () => IsBusy);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public bool Enabled { get => enabled; set => Set(ref enabled, value); }
        public bool Hiragana { get => hiragana; set => SetChoice(ref hiragana, value, katakana); }
        public bool Katakana { get => katakana; set => SetChoice(ref katakana, value, hiragana); }
        public bool Clear { get => clear; set => SetChoice(ref clear, value, voiced || contracted); }
        public bool Voiced { get => voiced; set => SetChoice(ref voiced, value, clear || contracted); }
        public bool Contracted { get => contracted; set => SetChoice(ref contracted, value, clear || voiced); }
        public bool ReviewAll { get => reviewAll; set => Set(ref reviewAll, value); }
        public bool ReviewDaily { get => !ReviewAll; set { if (value) ReviewAll = false; } }
        public int DailyTarget
        {
            get => dailyTarget;
            set { dailyTarget = value; dailyTargetText = value.ToString(CultureInfo.InvariantCulture); Notify(); }
        }
        public string DailyTargetText { get => dailyTargetText; set => Set(ref dailyTargetText, value); }
        public bool IsBusy { get; private set; }
        public bool CanToggleHiragana => !IsBusy && (!hiragana || katakana);
        public bool CanToggleKatakana => !IsBusy && (!katakana || hiragana);
        public bool CanToggleClear => !IsBusy && (!clear || voiced || contracted);
        public bool CanToggleVoiced => !IsBusy && (!voiced || clear || contracted);
        public bool CanToggleContracted => !IsBusy && (!contracted || clear || voiced);
        public bool IsDailyTargetEnabled => !IsBusy && !ReviewAll;
        public string RangeLabel => ReviewAll ? $"本轮全量 {EnabledCount} 项" : $"启用范围 {EnabledCount} 项";
        private int EnabledCount => ((hiragana ? 1 : 0) + (katakana ? 1 : 0))
            * ((clear ? 46 : 0) + (voiced ? 25 : 0) + (contracted ? 36 : 0));
        public string ProgressLabel { get; private set; } = "五十音进度尚未读取";
        public string? ErrorMessage { get; private set; }
        public string? StatusMessage { get; private set; }
        public ICommand RefreshCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand StartCommand { get; }
        public ICommand CancelCommand { get; }

        /// <summary>读取范围及实际今日进度；不创建听写轮次。</summary>
        /// <param name="cancellationToken">取消加载。</param>
        public Task RefreshAsync(CancellationToken cancellationToken = default) => RunAsync(async token =>
        {
            var plan = await Task.Run(() => reader.LoadAsync(token), token);
            token.ThrowIfCancellationRequested();
            enabled = plan.Enabled; hiragana = plan.Hiragana; katakana = plan.Katakana;
            clear = plan.Clear; voiced = plan.Voiced; contracted = plan.Contracted;
            reviewAll = plan.ReviewAll; dailyTarget = plan.DailyTarget;
            dailyTargetText = dailyTarget.ToString(CultureInfo.InvariantCulture);
            await ReadProgressAsync(token);
        }, cancellationToken);

        /// <summary>把当前选项交给业务层校验保存，错误在当前页面呈现。</summary>
        /// <param name="cancellationToken">取消保存。</param>
        public Task SaveAsync(CancellationToken cancellationToken = default) => RunAsync(SaveCoreAsync, cancellationToken);

        /// <summary>保存有效计划后进入听写；存在旧轮次时优先恢复，设置只影响新轮次。</summary>
        /// <param name="cancellationToken">取消保存与导航。</param>
        public Task StartAsync(CancellationToken cancellationToken = default) => RunAsync(async token =>
        {
            await SaveCoreAsync(token);
            if (!Enabled && !hasActiveSession) { StatusMessage = "请启用计划后再开始听写。"; return; }
            await navigation.OpenKanaSessionAsync(token);
        }, cancellationToken);

        /// <summary>保存当前绑定请求，保留业务层独立的范围校验。</summary>
        /// <param name="token">取消业务写入。</param>
        private async Task SaveCoreAsync(CancellationToken token)
        {
            // 保存原始编辑文本才能看见转换失败；WPF直接绑定int会静默保留旧值。
            if (!int.TryParse(DailyTargetText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var requestedTarget))
                throw new ArgumentException("每日数量必须为整数，不能为空、含小数或超过整数范围。");
            var plan = new KanaPlanDto(Enabled, Hiragana, Katakana, Clear, Voiced, Contracted, ReviewAll, requestedTarget);
            await Task.Run(() => editor.SaveAsync(plan, token), token);
            token.ThrowIfCancellationRequested();
            dailyTarget = requestedTarget;
            await ReadProgressAsync(token);
            StatusMessage = "计划已保存；未完成轮次继续使用原范围。";
        }

        /// <summary>读取显示计数，不根据页面推测已完成数量。</summary>
        /// <param name="token">取消查询。</param>
        private async Task ReadProgressAsync(CancellationToken token)
        {
            var value = await Task.Run(() => progress.GetProgressAsync(token), token);
            token.ThrowIfCancellationRequested();
            hasActiveSession = value.HasActiveSession;
            ProgressLabel = $"五十音 {value.CompletedToday}/{value.Target}";
        }

        /// <summary>分别阻止取消各组选项的最后一项，恢复绑定避免出现空范围。</summary>
        /// <param name="field">当前选项的绑定状态。</param>
        /// <param name="value">用户请求的选中状态。</param>
        /// <param name="anotherSelected">同组是否还有另一项被选中。</param>
        private void SetChoice(ref bool field, bool value, bool anotherSelected)
        {
            if (!value && !anotherSelected) { Notify(); return; }
            Set(ref field, value);
        }

        private void Set<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return;
            field = value;
            Notify();
        }

        /// <summary>串行页面操作，退出立即取消，取消后的异常不能发布到旧页。</summary>
        /// <param name="action">计划读写或当前窗口导航操作。</param>
        /// <param name="token">调用方的取消令牌，区别于页面内部取消。</param>
        private async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken token)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            token.ThrowIfCancellationRequested();
            if (IsBusy) return;
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            operation = cancellation; IsBusy = true; ErrorMessage = null; StatusMessage = null; Notify();
            try { await action(cancellation.Token); }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { token.ThrowIfCancellationRequested(); }
            catch (Exception exception)
            {
                token.ThrowIfCancellationRequested();
                if (!disposed && !cancellation.IsCancellationRequested) ErrorMessage = $"五十音计划操作失败：{exception.Message}";
            }
            finally { operation = null; IsBusy = false; if (!disposed) Notify(); }
        }

        private AsyncCommand Command(Func<Task> action, Func<bool> canExecute)
        {
            var command = new AsyncCommand(action, () => !disposed && canExecute());
            commands.Add(command); return command;
        }
        private void Notify()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            foreach (var command in commands) command.NotifyCanExecuteChanged();
        }
        /// <summary>返回或用户取消时停止当前读写／导航准备。</summary>
        public void CancelOperation() => operation?.Cancel();
        /// <inheritdoc />
        public void Dispose() { if (disposed) return; disposed = true; CancelOperation(); }
    }
}
