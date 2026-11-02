using Dictation.Core.Configuration;
using Dictation.Core.Mistakes;
using Dictation.Core.Tts;
using Dictation.Presentation.Audio;
using Dictation.Presentation.Navigation;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>协调只读错题列表、详情和练习导航，不执行判题、SQL或记忆算法。</summary>
    public sealed class WrongAnswerListViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly IWrongAnswerQuery query;
        private readonly IWrongAnswerNavigation practiceNavigation;
        private readonly ILearningSessionNavigation learningNavigation;
        private readonly TtsService tts;
        private readonly IAudioPlayer audio;
        private readonly List<AsyncCommand> commands = new();
        private CancellationTokenSource? operation;
        private WrongAnswerItemViewModel? selectedItem;
        private bool isBusy;
        private bool initialized;
        private bool disposed;
        private string? errorMessage;
        private string statusMessage = "尚未读取历史错题。";

        /// <summary>组合只读查询、按能力隔离的导航及内存语音输出。</summary>
        /// <param name="query">查询同语言的原始错误。</param>
        /// <param name="practiceNavigation">打开原复习页进行错题重练。</param>
        /// <param name="learningNavigation">恢复未完成学习，不移动词库。</param>
        /// <param name="tts">合成当前详情的目标读音。</param>
        /// <param name="audio">仅播放内存音频。</param>
        public WrongAnswerListViewModel(IWrongAnswerQuery query, IWrongAnswerNavigation practiceNavigation,
            ILearningSessionNavigation learningNavigation, TtsService tts, IAudioPlayer audio)
        {
            this.query = query ?? throw new ArgumentNullException(nameof(query));
            this.practiceNavigation = practiceNavigation ?? throw new ArgumentNullException(nameof(practiceNavigation));
            this.learningNavigation = learningNavigation ?? throw new ArgumentNullException(nameof(learningNavigation));
            this.tts = tts ?? throw new ArgumentNullException(nameof(tts));
            this.audio = audio ?? throw new ArgumentNullException(nameof(audio));
            RefreshCommand = Command(() => RefreshAsync(), () => !IsBusy);
            CancelCommand = Command(() => { CancelOperation(); return Task.CompletedTask; }, () => IsBusy);
            RepracticeCommand = Command(() => RepracticeAsync(), () => !IsBusy && Items.Any(item => item.IsSelected && item.IsReview));
            ContinueLearningCommand = Command(() => ContinueLearningAsync(), () => !IsBusy && Items.Any(item => !item.IsReview));
            PlayDetailCommand = Command(() => PlayDetailAsync(), () => !IsBusy && SelectedItem is not null);
            Items.CollectionChanged += (_, args) =>
            {
                if (args.OldItems is not null) foreach (WrongAnswerItemViewModel item in args.OldItems) item.PropertyChanged -= ItemChanged;
                if (args.NewItems is not null) foreach (WrongAnswerItemViewModel item in args.NewItems) item.PropertyChanged += ItemChanged;
                Changed(nameof(SelectAll));
                NotifyCommands();
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public LearningLanguage Language { get; private set; }
        public string WindowTitle => Language == LearningLanguage.Japanese ? "日语错题回顾" : "英语错题回顾";
        public ObservableCollection<WrongAnswerItemViewModel> Items { get; } = new();
        public WrongAnswerItemViewModel? SelectedItem
        {
            get => selectedItem;
            set
            {
                if (value is not null && !Items.Contains(value)) throw new ArgumentException("请选择当前列表中的错题。", nameof(value));
                if (ReferenceEquals(selectedItem, value)) return;
                selectedItem = value;
                Changed();
                NotifyCommands();
            }
        }
        public bool? SelectAll
        {
            get => Items.Count == 0 || Items.All(item => !item.IsSelected) ? false
                : Items.All(item => item.IsSelected) ? true : null;
            set
            {
                if (value is not bool selected) return;
                foreach (var item in Items) item.IsSelected = selected;
                Changed();
            }
        }
        public bool IsBusy => isBusy;
        public bool CanSelectAll => !disposed && !IsBusy && Items.Count > 0;
        public string? ErrorMessage => errorMessage;
        public string StatusMessage => statusMessage;
        public ICommand RefreshCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand RepracticeCommand { get; }
        public ICommand ContinueLearningCommand { get; }
        public ICommand PlayDetailCommand { get; }

        /// <summary>固定页面语言，只能在返回首页后切换。</summary>
        /// <param name="language">首页选定语言。</param>
        public void Initialize(LearningLanguage language)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (language is not (LearningLanguage.Japanese or LearningLanguage.English)) throw new ArgumentOutOfRangeException(nameof(language));
            if (initialized && Language != language) throw new InvalidOperationException("请回首页后切换语言。");
            Language = language;
            initialized = true;
            Changed(nameof(WindowTitle));
        }

        /// <summary>读取历史错误并保留当前选择，查询不修改任何进度。</summary>
        /// <param name="cancellationToken">外部读取取消令牌。</param>
        public Task RefreshAsync(CancellationToken cancellationToken = default) => RunAsync(async token =>
        {
            var selected = Items.Where(item => item.IsSelected).Select(item => Identity(item)).ToHashSet();
            var detail = SelectedItem is null ? null : Identity(SelectedItem);
            var result = SynchronizationContext.Current is DispatcherSynchronizationContext
                ? await Task.Run(() => query.LoadAsync(Language, token), token)
                : await query.LoadAsync(Language, token);
            token.ThrowIfCancellationRequested();
            SelectedItem = null;
            foreach (var item in Items) item.PropertyChanged -= ItemChanged;
            Items.Clear();
            foreach (var dto in result)
            {
                var item = new WrongAnswerItemViewModel(dto);
                item.IsSelected = selected.Contains(Identity(item));
                Items.Add(item);
            }
            SelectedItem = Items.FirstOrDefault(item => Identity(item) == detail);
            SetStatus($"{Items.Count} 个历史错误词形 · 重试正确仍保留原错误记录");
        }, cancellationToken);

        /// <summary>只把选中的复习整组交给原复习启动能力，学习中选择不会被移动。</summary>
        /// <param name="cancellationToken">取消导航令牌。</param>
        public Task RepracticeAsync(CancellationToken cancellationToken = default) => RunAsync(async token =>
        {
            var ids = Items.Where(item => item.IsSelected && item.IsReview).Select(item => item.Item.WordGroupId).Distinct().ToArray();
            if (ids.Length == 0) throw new InvalidOperationException("请先选择复习中的错题；学习中的错题请继续原学习。");
            await practiceNavigation.OpenWrongAnswerPracticeAsync(Language, ids, token);
        }, cancellationToken);

        /// <summary>恢复本语言学习会话，保留原题和学习库位置。</summary>
        /// <param name="cancellationToken">取消导航令牌。</param>
        public Task ContinueLearningAsync(CancellationToken cancellationToken = default) => RunAsync(async token =>
        {
            if (!Items.Any(item => !item.IsReview)) throw new InvalidOperationException("没有学习中的错题。");
            await learningNavigation.OpenLearningSessionAsync(Language, null, token);
        }, cancellationToken);

        /// <summary>播放详情最近错误的对应读音，仅使用内存流，不产生答题观察。</summary>
        /// <param name="cancellationToken">取消合成或播放的令牌。</param>
        public Task PlayDetailAsync(CancellationToken cancellationToken = default) => RunAsync(async token =>
        {
            var attempt = SelectedItem?.Item.Attempts.FirstOrDefault() ?? throw new InvalidOperationException("请先查看一项错题详情。");
            var language = Language == LearningLanguage.Japanese ? TtsLanguage.Japanese : TtsLanguage.English;
            var data = await tts.SynthesizeAsync(new TtsSynthesisRequestDto(language, attempt.ExpectedSpokenText), token);
            await audio.PlayWavAsync(data.WavData, token);
        }, cancellationToken);

        /// <summary>取消本页读取或音频，不改变业务记录。</summary>
        public void CancelOperation() => operation?.Cancel();

        /// <summary>页面作用域退场后停止操作并禁止迟到绑定更新。</summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            CancelOperation();
            foreach (var item in Items) item.PropertyChanged -= ItemChanged;
        }

        /// <summary>串行页面动作并阻止取消后到达的异常覆盖当前绑定。</summary>
        /// <param name="action">只协调UI的异步动作。</param>
        /// <param name="external">调用方取消令牌；外部取消保持传播。</param>
        private async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken external)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!initialized) throw new InvalidOperationException("错题页尚未初始化。");
            external.ThrowIfCancellationRequested();
            if (IsBusy) return;
            using var current = CancellationTokenSource.CreateLinkedTokenSource(external);
            operation = current;
            isBusy = true;
            errorMessage = null;
            Changed(nameof(IsBusy));
            Changed(nameof(ErrorMessage));
            NotifyCommands();
            try { await action(current.Token); }
            catch (Exception) when (current.IsCancellationRequested)
            {
                external.ThrowIfCancellationRequested();
                if (!disposed) SetStatus("已取消，可重试或返回。");
            }
            catch (Exception exception)
            {
                if (!disposed) { errorMessage = exception.Message; Changed(nameof(ErrorMessage)); }
            }
            finally
            {
                operation = null;
                isBusy = false;
                Changed(nameof(IsBusy));
                NotifyCommands();
            }
        }

        private static string Identity(WrongAnswerItemViewModel item) => $"{item.Item.WordGroupId}|{item.Item.SourceSenseId}|{item.Item.FormCode}";
        private void ItemChanged(object? sender, PropertyChangedEventArgs args) { Changed(nameof(SelectAll)); NotifyCommands(); }
        private AsyncCommand Command(Func<Task> action, Func<bool> enabled)
        {
            var command = new AsyncCommand(action, () => !disposed && enabled());
            commands.Add(command);
            return command;
        }
        private void NotifyCommands()
        {
            Changed(nameof(CanSelectAll));
            foreach (var command in commands) command.NotifyCanExecuteChanged();
        }
        private void SetStatus(string value) { statusMessage = value; Changed(nameof(StatusMessage)); }
        private void Changed([CallerMemberName] string? name = null)
        {
            if (!disposed) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
