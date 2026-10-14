using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Dictation.Core.Configuration;
using Dictation.Core.Review;
using Dictation.Presentation.Navigation;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>管理固定语言的只读复习词库页面，不承载记忆算法或答题规则。</summary>
    public sealed class ReviewLibraryViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly IReviewLibraryQuery query;
        private readonly AsyncCommand refreshCommand;
        private readonly AsyncCommand cancelCommand;
        private CancellationTokenSource? operationCancellation;
        private ReviewWordGroupItemViewModel? selectedGroup;
        private bool isBusy;
        private string statusMessage = "尚未加载复习词库。";
        private string? errorMessage;
        private bool isInitialized;
        private bool isDisposed;

        /// <summary>组合只读业务查询和上一页导航，创建页面命令。</summary>
        /// <param name="query">提供当前语言复习词组的只读能力。</param>
        /// <param name="navigation">返回上级页面的能力。</param>
        public ReviewLibraryViewModel(IReviewLibraryQuery query, IBackNavigation navigation)
        {
            this.query = query ?? throw new ArgumentNullException(nameof(query));
            ArgumentNullException.ThrowIfNull(navigation);
            refreshCommand = new AsyncCommand(() => RefreshAsync(), () => !isDisposed && !IsBusy);
            cancelCommand = new AsyncCommand(() =>
            {
                CancelOperation();
                return Task.CompletedTask;
            }, () => !isDisposed && IsBusy && operationCancellation is not null);
            BackCommand = new AsyncCommand(async () =>
            {
                CancelOperation();
                await navigation.GoBackAsync();
            }, () => !isDisposed);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public LearningLanguage SelectedLanguage { get; private set; }
        public string WindowTitle => SelectedLanguage == LearningLanguage.Japanese ? "日语复习词库" : "英语复习词库";
        public ObservableCollection<ReviewWordGroupItemViewModel> Groups { get; } = new();
        public ReviewWordGroupItemViewModel? SelectedGroup
        {
            get => selectedGroup;
            set
            {
                if (value is not null && !Groups.Contains(value))
                {
                    throw new ArgumentException("请选择当前复习词库中的词组。", nameof(value));
                }
                SetProperty(ref selectedGroup, value);
            }
        }
        public bool IsBusy
        {
            get => isBusy;
            private set
            {
                if (SetProperty(ref isBusy, value))
                {
                    refreshCommand.NotifyCanExecuteChanged();
                    cancelCommand.NotifyCanExecuteChanged();
                }
            }
        }
        public string StatusMessage { get => statusMessage; private set => SetProperty(ref statusMessage, value); }
        public string? ErrorMessage { get => errorMessage; private set => SetProperty(ref errorMessage, value); }
        public ICommand RefreshCommand => refreshCommand;
        public ICommand BackCommand { get; }
        public ICommand CancelCommand => cancelCommand;

        /// <summary>固定页面语言；切换语言必须返回首页。</summary>
        /// <param name="language">首页选定语言。</param>
        public void Initialize(LearningLanguage language)
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            if (language is not (LearningLanguage.Japanese or LearningLanguage.English))
            {
                throw new ArgumentOutOfRangeException(nameof(language));
            }
            if (isInitialized && SelectedLanguage != language)
            {
                throw new InvalidOperationException("请返回首页后切换语言。");
            }
            SelectedLanguage = language;
            isInitialized = true;
            OnPropertyChanged(nameof(WindowTitle));
        }

        /// <summary>刷新当前语言的只读风险词库。</summary>
        /// <param name="cancellationToken">调用方取消令牌。</param>
        public async Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            if (!isInitialized)
            {
                throw new InvalidOperationException("复习词库尚未初始化。");
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (IsBusy)
            {
                return;
            }
            using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            operationCancellation = operation;
            IsBusy = true;
            ErrorMessage = null;
            StatusMessage = "正在读取复习词库…";
            var language = SelectedLanguage;
            var selectedId = SelectedGroup?.WordGroupId;
            try
            {
                // SQLite 的异步入口仍可能同步执行查询；整个业务读取放到后台，绑定留在原 UI 上下文。
                var result = await Task.Run(() => query.LoadAsync(language, operation.Token), operation.Token);
                operation.Token.ThrowIfCancellationRequested();
                if (result.Language != language)
                {
                    throw new InvalidDataException("复习词库响应的语言与当前页面不一致。");
                }
                SelectedGroup = null;
                Groups.Clear();
                foreach (var group in result.Groups)
                {
                    Groups.Add(new ReviewWordGroupItemViewModel(group));
                }
                SelectedGroup = Groups.FirstOrDefault(group => group.WordGroupId == selectedId);
                StatusMessage = $"共 {result.TotalGroupCount} 组 · 待测 {result.PendingTestGroupCount} 组 · 待复习 {result.DueGroupCount} 组（含待测）";
            }
            catch (OperationCanceledException) when (operation.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                if (!isDisposed)
                {
                    StatusMessage = "已取消读取，可再次刷新。";
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (!isDisposed)
                {
                    ErrorMessage = $"读取复习词库失败：{exception.Message}";
                    StatusMessage = "读取失败，可再次刷新。";
                }
            }
            finally
            {
                operationCancellation = null;
                if (!isDisposed)
                {
                    IsBusy = false;
                }
            }
        }

        /// <summary>停止后台读取，不修改用户记忆。</summary>
        public void CancelOperation() => operationCancellation?.Cancel();

        /// <summary>标记页面结束并停止仍在执行的操作。</summary>
        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }
            isDisposed = true;
            CancelOperation();
        }

        /// <summary>仅在绑定值确实改变时通知界面，不重复刷新控件。</summary>
        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }
            field = value;
            OnPropertyChanged(name);
            return true;
        }

        /// <summary>退场作用域释放后不再向旧页面发送通知。</summary>
        private void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            if (!isDisposed)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
