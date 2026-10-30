using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Importing;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>
    /// 管理固定语言的文本候选确认并复用统一导入收件箱。
    /// </summary>
    public sealed class TextImportViewModel : INotifyPropertyChanged, IDisposable
    {
        private bool isInitialized;
        private bool isDisposed;
        private CancellationTokenSource? operationCancellation;
        private readonly ITextImportPreparation preparation;
        private readonly IImportStaging staging;
        private readonly IHomeNavigation navigation;
        private readonly Utf8TextFileReader fileReader;
        private string sourceText = string.Empty;
        private string? statusMessage;
        private string? errorMessage;
        private bool isBusy;
        private readonly AsyncCommand prepareCommand;
        private readonly AsyncCommand stageCommand;
        private readonly AsyncCommand inboxCommand;
        private readonly AsyncCommand cancelCommand;

        /// <summary>组合文本准备、整组暂存与首页页面导航能力。</summary>
        /// <param name="preparation">解析词表并查询精确来源义项。</param>
        /// <param name="staging">将确认的整组词语暂存到收件箱。</param>
        /// <param name="navigation">打开当前语言的收件箱。</param>
        /// <param name="fileReader">严格读取 TXT 文件的输入适配器，不执行业务判定。</param>
        public TextImportViewModel(ITextImportPreparation preparation, IImportStaging staging,
            IHomeNavigation navigation, Utf8TextFileReader fileReader)
        {
            this.preparation = preparation ?? throw new ArgumentNullException(nameof(preparation));
            this.staging = staging ?? throw new ArgumentNullException(nameof(staging));
            this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            this.fileReader = fileReader ?? throw new ArgumentNullException(nameof(fileReader));
            prepareCommand = new AsyncCommand(() => LoadTextAsync(SourceText), () => CanEdit);
            stageCommand = new AsyncCommand(async () => await StageSelectedAsync(), () => CanStage);
            inboxCommand = new AsyncCommand(() => OpenInboxAsync(), () => CanEdit);
            cancelCommand = new AsyncCommand(() => { CancelOperation(); return Task.CompletedTask; }, () => IsBusy);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public LearningLanguage SelectedLanguage { get; private set; }
        public string WindowTitle => SelectedLanguage == LearningLanguage.Japanese ? "日语文本导入" : "英语文本导入";
        public string SourceText
        {
            get => sourceText;
            set
            {
                if (IsBusy || !SetProperty(ref sourceText, value ?? string.Empty))
                {
                    return;
                }
                ClearLines();
                StatusMessage = null;
                ErrorMessage = null;
            }
        }
        public ObservableCollection<TextImportLineViewModel> Lines { get; } = new();
        public string? StatusMessage
        {
            get => statusMessage;
            private set => SetProperty(ref statusMessage, value);
        }
        public string? ErrorMessage
        {
            get => errorMessage;
            private set => SetProperty(ref errorMessage, value);
        }
        public bool IsBusy
        {
            get => isBusy;
            private set
            {
                if (SetProperty(ref isBusy, value))
                {
                    OnPropertyChanged(nameof(CanEdit));
                    NotifySelectionChanged();
                }
            }
        }
        public bool CanEdit => isInitialized && !isDisposed && !IsBusy;
        public bool CanStage => CanEdit && SelectedCount > 0;
        public bool CanSelectAll => CanEdit && Lines.Any(line => line.CanSelect);
        public int SelectedCount => Lines.Count(line => line.IsSelected && line.CanSelect);
        public bool? AreAllSelected
        {
            get
            {
                var selectable = Lines.Where(line => line.CanSelect).ToArray();
                var selected = selectable.Count(line => line.IsSelected);
                if (selected == 0) return false;
                return selected == selectable.Length ? true : null;
            }
            set
            {
                if (!CanEdit || value is not bool selected)
                {
                    return;
                }
                foreach (var line in Lines)
                {
                    line.IsSelected = selected;
                }
                NotifySelectionChanged();
            }
        }
        public ICommand StageSelectedCommand => stageCommand;
        public ICommand PrepareCommand => prepareCommand;
        public ICommand OpenInboxCommand => inboxCommand;
        public ICommand CancelCommand => cancelCommand;

        /// <summary>取消当前页面的输入准备或暂存，不撤销已经提交的批次。</summary>
        public void CancelOperation()
        {
            operationCancellation?.Cancel();
        }

        /// <summary>页面作用域释放时停止未完成的操作。</summary>
        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }
            isDisposed = true;
            CancelOperation();
            foreach (var line in Lines)
            {
                line.PropertyChanged -= OnLineChanged;
            }
            IsBusy = false;
            OnPropertyChanged(nameof(CanEdit));
            NotifySelectionChanged();
        }

        /// <summary>绑定页面的唯一语言，防止候选跨语言复用。</summary>
        /// <param name="language">首页选定的学习语言。</param>
        public void Initialize(LearningLanguage language)
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            if (!Enum.IsDefined(language))
            {
                throw new ArgumentOutOfRangeException(nameof(language));
            }
            if (isInitialized)
            {
                throw new InvalidOperationException("文本导入页面已经初始化。");
            }
            SelectedLanguage = language;
            isInitialized = true;
            OnPropertyChanged(nameof(SelectedLanguage));
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(CanEdit));
            NotifySelectionChanged();
        }

        /// <summary>重新准备文本候选，不写入用户词库。</summary>
        /// <param name="text">每行一个来源词形的文本。</param>
        /// <param name="cancellationToken">取消查询的令牌。</param>
        public Task LoadTextAsync(string text, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            ArgumentNullException.ThrowIfNull(text);
            return LoadInputAsync(_ => Task.FromResult(text), cancellationToken);
        }

        /// <summary>输入获取和候选查询共用一次忙状态，防止文件读取期间叠加另一次导入。</summary>
        /// <param name="readText">获取本次输入文本的适配函数。</param>
        /// <param name="cancellationToken">取消读取与查询的令牌。</param>
        private async Task LoadInputAsync(Func<CancellationToken, Task<string>> readText, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsBusy)
            {
                return;
            }
            ClearLines();
            StatusMessage = null;
            ErrorMessage = null;
            using var operation = BeginOperation(cancellationToken);
            try
            {
                var language = SelectedLanguage;
                // SQLite 的异步 API 可能同步执行；输入解码和业务准备均离开 Dispatcher。
                var prepared = await Task.Run(async () =>
                {
                    var text = await readText(operation.Token).ConfigureAwait(false);
                    operation.Token.ThrowIfCancellationRequested();
                    var result = await preparation.PrepareAsync(language, text, operation.Token).ConfigureAwait(false);
                    return (Text: text, Result: result);
                }, operation.Token);
                operation.Token.ThrowIfCancellationRequested();
                SetProperty(ref sourceText, prepared.Text, nameof(SourceText));
                foreach (var line in prepared.Result.Lines)
                {
                    var row = new TextImportLineViewModel(line);
                    row.PropertyChanged += OnLineChanged;
                    Lines.Add(row);
                }
                StatusMessage = $"共 {Lines.Count} 项，未匹配 {Lines.Count(line => line.Candidates.Count == 0)} 项。多义词请先选择中文义项。";
            }
            catch (OperationCanceledException) when (operation.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                if (!isDisposed)
                {
                    StatusMessage = "已取消匹配，可重新准备词表。";
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
                    ErrorMessage = $"准备词表失败：{exception.Message}";
                }
            }
            finally
            {
                EndOperation();
            }
        }

        /// <summary>将用户选定的义项交给整组暂存服务。</summary>
        /// <param name="cancellationToken">取消暂存的令牌。</param>
        /// <returns>成功时的批次统计；没有选择或暂存失败时为空。</returns>
        public async Task<StageImportBatchResultDto?> StageSelectedAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            cancellationToken.ThrowIfCancellationRequested();
            if (IsBusy)
            {
                return null;
            }
            ErrorMessage = null;
            var selections = Lines.Where(line => line.IsSelected && line.SelectedCandidate is not null)
                .Select(line => new ImportSelectionDto(line.SourceText, line.SelectedCandidate!.SourceSenseId)).ToArray();
            if (selections.Length == 0)
            {
                ErrorMessage = "请先选择要导入的义项。";
                return null;
            }
            using var operation = BeginOperation(cancellationToken);
            try
            {
                var request = new StageImportBatchRequestDto(SelectedLanguage, ImportSourceKind.Text, selections);
                var result = await Task.Run(() => staging.StageAsync(request, operation.Token), operation.Token);
                operation.Token.ThrowIfCancellationRequested();
                StatusMessage = $"已加入导入收件箱：新增 {result.AddedCount} 项，忽略重复 {result.DuplicateCount} 项。请在收件箱决定学习、复习或丢弃。";
                return result;
            }
            catch (OperationCanceledException) when (operation.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                if (!isDisposed)
                {
                    // 取消不是撤销：若事务已提交，批次仍可在统一收件箱查看。
                    StatusMessage = "已取消暂存操作；已经提交的批次仍保留在收件箱。";
                }
                return null;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (!isDisposed)
                {
                    ErrorMessage = $"暂存词表失败：{exception.Message}";
                }
                return null;
            }
            finally
            {
                EndOperation();
            }
        }

        /// <summary>通过输入适配器读取文件并复用同一候选确认流程。</summary>
        /// <param name="path">用户选择或拖放的 TXT 文件路径。</param>
        /// <param name="cancellationToken">取消读取与查询的令牌。</param>
        public Task LoadFileAsync(string path, CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            return LoadInputAsync(token => fileReader.ReadAsync(path, token), cancellationToken);
        }

        /// <summary>显示输入适配错误，不进行词库或数据库操作。</summary>
        /// <param name="message">面向用户的输入错误说明。</param>
        public void ReportInputError(string message)
        {
            ErrorMessage = message;
        }

        /// <summary>在同一窗口打开当前语言收件箱。</summary>
        /// <param name="cancellationToken">取消导航的令牌。</param>
        public Task OpenInboxAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            cancellationToken.ThrowIfCancellationRequested();
            return IsBusy ? Task.CompletedTask : navigation.OpenImportInboxAsync(SelectedLanguage, cancellationToken);
        }

        /// <summary>丢弃过期候选及其订阅，避免编辑文本后继续导入旧结果。</summary>
        private void ClearLines()
        {
            foreach (var line in Lines)
            {
                line.PropertyChanged -= OnLineChanged;
            }
            Lines.Clear();
            NotifySelectionChanged();
        }

        private void OnLineChanged(object? sender, PropertyChangedEventArgs args) => NotifySelectionChanged();

        private void NotifySelectionChanged()
        {
            OnPropertyChanged(nameof(CanSelectAll));
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(AreAllSelected));
            OnPropertyChanged(nameof(CanStage));
            prepareCommand.NotifyCanExecuteChanged();
            stageCommand.NotifyCanExecuteChanged();
            inboxCommand.NotifyCanExecuteChanged();
            cancelCommand.NotifyCanExecuteChanged();
        }

        /// <summary>每次操作拥有独立的关联令牌，页面取消不会污染下一次重试。</summary>
        /// <param name="cancellationToken">调用方取消令牌。</param>
        /// <returns>由当前异步操作负责释放的取消源。</returns>
        private CancellationTokenSource BeginOperation(CancellationToken cancellationToken)
        {
            operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            IsBusy = true;
            return operationCancellation;
        }

        /// <summary>结束忙状态；已释放页面不得再通知旧界面或应用旧结果。</summary>
        private void EndOperation()
        {
            operationCancellation = null;
            if (!isDisposed)
            {
                IsBusy = false;
            }
        }

        private void EnsureInitialized()
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            if (!isInitialized)
            {
                throw new InvalidOperationException("文本导入页面尚未初始化。");
            }
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
