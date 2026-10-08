using Dictation.Core.Practice;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Threading;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Tts;
using Dictation.Presentation.Audio;
using Dictation.Presentation.Practice;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>
    /// 协调共用练习页面状态，通过模式适配调用独立业务，不执行判题或持久化规则。
    /// </summary>
    public sealed class PracticeSessionViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly IPracticeSessionWorkflow workflow;
        private readonly TtsService ttsService;
        private readonly IAudioPlayer audioPlayer;
        private readonly SemaphoreSlim operationGate = new(1, 1);
        private readonly CancellationTokenSource lifetime = new();
        private readonly List<AsyncCommand> commands = new();
        private CancellationTokenSource? currentOperation;
        private LearningLanguage selectedLanguage;
        private PracticeSessionSnapshot? session;
        private PracticeQuestionDto? question;
        private PracticeAnswerFeedback? feedback;
        private PracticeSessionSummary? summary;
        private bool isEmpty;
        private ImportWordGroupMemberDto? selectedBrowseMember;
        private ImportWordGroupFormDto? selectedBrowseForm;
        private IReadOnlyList<ImportWordGroupFormDto> browseForms = Array.Empty<ImportWordGroupFormDto>();
        private HashSet<string> enabledFormCodes = new(StringComparer.Ordinal);
        private IReadOnlyList<string>? startSelection;
        private string answerText = string.Empty;
        private string? errorMessage;
        private bool isInitialized;
        private bool isBusy;
        private bool isBrowsing;
        private bool hasHeardQuestion;
        private bool isDisposed;

        /// <summary>
        /// 组合当前模式适配与共用的内存音频输出。
        /// </summary>
        /// <param name="workflow">仅适配本页需要的学习或复习业务能力。</param>
        /// <param name="ttsService">生成题目或浏览词形音频的业务服务。</param>
        /// <param name="audioPlayer">播放内存 WAV 数据的可替换输出适配器。</param>
        internal PracticeSessionViewModel(
            IPracticeSessionWorkflow workflow,
            TtsService ttsService,
            IAudioPlayer audioPlayer)
        {
            this.workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
            this.ttsService = ttsService ?? throw new ArgumentNullException(nameof(ttsService));
            this.audioPlayer = audioPlayer ?? throw new ArgumentNullException(nameof(audioPlayer));
            SubmitCommand = CreateCommand(() => SubmitAsync(), () => CanSubmit);
            ContinueCommand = CreateCommand(() => ContinueAsync(), () => !IsBusy && Feedback is not null && !IsBrowsing);
            ReplayQuestionCommand = CreateCommand(() => ReplayQuestionAsync(), () => CanReplayQuestion);
            ReplayBrowseCommand = CreateCommand(() => ReplayBrowseAsync(),
                () => !IsBusy && IsBrowsing && SelectedBrowseForm is not null);
            OpenBrowseCommand = CreateCommand(() => SetBrowsingAsync(true),
                () => !IsBusy && !IsBrowsing && BrowseMembers.Count > 0);
            CloseBrowseCommand = CreateCommand(() => SetBrowsingAsync(false), () => !IsBusy && IsBrowsing);
            RetryStartCommand = CreateCommand(() => StartAsync(startSelection),
                () => !IsBusy && Question is null && Summary is null);
            CancelCommand = CreateCommand(() =>
            {
                currentOperation?.Cancel();
                return Task.CompletedTask;
            }, () => IsBusy);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public ObservableCollection<ImportWordGroupMemberDto> BrowseMembers { get; } = new();
        public LearningLanguage SelectedLanguage => selectedLanguage;
        public string WindowTitle => (selectedLanguage == LearningLanguage.Japanese ? "日语" : "英语") + workflow.ModeLabel;
        public PracticeSessionSnapshot? Session => session;
        public PracticeQuestionDto? Question => question;
        public PracticeAnswerFeedback? Feedback => feedback;
        public PracticeSessionSummary? Summary => summary;
        public bool IsEmptyVisible => isEmpty && !isBusy && !isBrowsing;
        public string EmptyMessage => "当前没有待测或到期词组，或今天的复习目标已完成。";
        public bool IsBusy => isBusy;
        public bool IsBrowsing => isBrowsing;
        public bool IsCompleted => summary is not null && feedback is null;
        public bool IsQuestionVisible => question is not null && !isBrowsing;
        public bool IsFeedbackVisible => feedback is not null && !isBrowsing;
        public bool IsSummaryVisible => IsCompleted && !isBrowsing;
        public bool IsRetryStartVisible => !isBusy && errorMessage is not null
            && question is null && summary is null;
        public bool IsQuestionReplayVisible => CanReplayQuestion;
        public string MeaningText => question is null ? string.Empty : string.Join("；", question.Meanings);
        public string ProgressLabel => session is null ? string.Empty
            : question is null ? $"本轮 {session.WordGroupIds.Count} 个词组"
            : $"本轮 {session.WordGroupIds.Count} 个词组 · 第 {question.QuestionOrdinal + 1} 题";
        public string FeedbackLabel => feedback is null ? string.Empty : feedback.WasCorrect ? "回答正确" : "再记一次，稍后重练";
        public string? ErrorMessage => errorMessage;
        public IReadOnlyList<ImportWordGroupFormDto> BrowseForms => browseForms;

        public string AnswerText
        {
            get => answerText;
            set
            {
                if (answerText == value) return;
                answerText = value ?? string.Empty;
                NotifyState();
            }
        }

        public ImportWordGroupMemberDto? SelectedBrowseMember
        {
            get => selectedBrowseMember;
            set
            {
                if (value is not null && !BrowseMembers.Contains(value))
                    throw new ArgumentException("只能选择本轮浏览中的词条。", nameof(value));
                selectedBrowseMember = value;
                browseForms = value?.Forms.Where(form => enabledFormCodes.Contains(form.FormCode)).ToArray()
                    ?? Array.Empty<ImportWordGroupFormDto>();
                selectedBrowseForm = browseForms.FirstOrDefault(form => form.IsPrimary) ?? browseForms.FirstOrDefault();
                NotifyState();
            }
        }

        public ImportWordGroupFormDto? SelectedBrowseForm
        {
            get => selectedBrowseForm;
            set
            {
                if (value is not null && !browseForms.Contains(value))
                    throw new ArgumentException("只能选择当前词条的本轮词形。", nameof(value));
                selectedBrowseForm = value;
                NotifyState();
            }
        }

        public ICommand SubmitCommand { get; }
        public ICommand ContinueCommand { get; }
        public ICommand ReplayQuestionCommand { get; }
        public ICommand ReplayBrowseCommand { get; }
        public ICommand OpenBrowseCommand { get; }
        public ICommand CloseBrowseCommand { get; }
        public ICommand RetryStartCommand { get; }
        public ICommand CancelCommand { get; }

        private bool CanSubmit => !isDisposed && !isBusy && !isBrowsing && question is not null
            && feedback is null && !string.IsNullOrWhiteSpace(answerText)
            && (question.Channel != LearningChannel.Dictation || hasHeardQuestion);
        private bool CanReplayQuestion => !isDisposed && !isBusy && !isBrowsing && question is not null
            && (question.Channel == LearningChannel.Dictation || feedback is not null);

        /// <summary>
        /// 初始化会话语言；初始化后禁止切换为另一语言。
        /// </summary>
        /// <param name="language">当前学习语言。</param>
        public void Initialize(LearningLanguage language)
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            if (language is not (LearningLanguage.Japanese or LearningLanguage.English))
                throw new ArgumentOutOfRangeException(nameof(language), language, "练习语言无效。");
            if (isInitialized && language != selectedLanguage)
                throw new InvalidOperationException("练习会话开始后不能切换语言。");
            selectedLanguage = language;
            isInitialized = true;
            NotifyState();
        }

        /// <summary>
        /// 读取计划并启动或恢复会话，加载浏览内容与当前题；听写题准备后先播放。
        /// </summary>
        /// <param name="selectedWordGroupIds">手动模式选择的词组标识；自动选择时可为空。</param>
        /// <param name="cancellationToken">调用方取消令牌，传递到当前异步操作。</param>
        public Task StartAsync(
            IReadOnlyList<string>? selectedWordGroupIds = null,
            CancellationToken cancellationToken = default)
        {
            EnsureReady();
            startSelection = selectedWordGroupIds;
            return RunOperationAsync(async token =>
            {
                // SQLite 异步接口内部仍有同步工作，不能占住 Dispatcher 导致取消和返回无响应。
                var prepared = await RunWorkflowAsync(() => workflow.StartOrResumeAsync(selectedLanguage, selectedWordGroupIds, token), token);
                token.ThrowIfCancellationRequested();
                if (prepared is null)
                {
                    session = null;
                    question = null;
                    feedback = null;
                    summary = null;
                    BrowseMembers.Clear();
                    SelectedBrowseMember = null;
                    answerText = string.Empty;
                    isBrowsing = false;
                    isEmpty = true;
                    hasHeardQuestion = false;
                    NotifyState();
                    return;
                }
                var members = await RunWorkflowAsync(() => workflow.GetBrowseItemsAsync(prepared.SessionId, token), token);
                var preparedQuestion = await RunWorkflowAsync(() => workflow.GetCurrentQuestionAsync(prepared.SessionId, token), token);
                // 返回或释放页面后的晚结果不能发布到旧页面，即使依赖忽略了取消。
                token.ThrowIfCancellationRequested();
                session = prepared;
                enabledFormCodes = prepared.FormCodes.ToHashSet(StringComparer.Ordinal);
                BrowseMembers.Clear();
                foreach (var member in members) BrowseMembers.Add(member);
                SelectedBrowseMember = BrowseMembers.FirstOrDefault();
                question = preparedQuestion;
                feedback = null;
                summary = session.IsCompleted
                    ? new PracticeSessionSummary(session.SessionId, 0, Array.Empty<PracticeErrorSummary>(), session.WordGroupIds.Count)
                    : null;
                answerText = string.Empty;
                isBrowsing = false;
                isEmpty = false;
                hasHeardQuestion = false;
                NotifyState();
                if (question?.Channel == LearningChannel.Dictation) await PlayQuestionCoreAsync(token);
            }, cancellationToken, "准备" + workflow.ModeLabel + "失败");
        }

        /// <summary>
        /// 在可提交状态下把答案交给业务层，并展示返回的反馈。
        /// </summary>
        /// <param name="cancellationToken">调用方取消令牌，传递到当前异步操作。</param>
        public Task SubmitAsync(CancellationToken cancellationToken = default)
        {
            EnsureReady();
            if (!CanSubmit) return Task.CompletedTask;
            return RunOperationAsync(async token =>
            {
                var currentQuestion = question!;
                var submittedText = answerText;
                var result = await RunWorkflowAsync(() => workflow.SubmitAnswerAsync(currentQuestion.SessionId, currentQuestion.QuestionId, submittedText, token), token);
                token.ThrowIfCancellationRequested();
                feedback = result;
                NotifyState();
            }, cancellationToken, "提交答案失败");
        }

        /// <summary>有 Dispatcher 消息循环时后台执行业务，并由 await 把展示状态更新送回原上下文。</summary>
        /// <param name="operation">不接触 UI 的业务操作。</param>
        /// <param name="cancellationToken">取消后台排队的令牌。</param>
        /// <returns>原业务结果，不改变调用方的状态发布上下文。</returns>
        private static Task<T> RunWorkflowAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
        {
            // 无消息循环的同步宿主无法接收 Dispatcher 回调，保持其原有执行方式。
            return SynchronizationContext.Current is DispatcherSynchronizationContext
                ? Task.Run(operation, cancellationToken)
                : operation();
        }

        /// <summary>
        /// 从反馈切换到下一题或总结；下一道听写题自动播放。
        /// </summary>
        /// <param name="cancellationToken">调用方取消令牌，传递到当前异步操作。</param>
        public Task ContinueAsync(CancellationToken cancellationToken = default)
        {
            EnsureReady();
            if (isBusy || isBrowsing || feedback is null) return Task.CompletedTask;
            return RunOperationAsync(async token =>
            {
                question = feedback.NextQuestion;
                summary = feedback.Summary;
                feedback = null;
                answerText = string.Empty;
                hasHeardQuestion = false;
                NotifyState();
                if (question?.Channel == LearningChannel.Dictation) await PlayQuestionCoreAsync(token);
            }, cancellationToken, "读取下一题失败");
        }

        /// <summary>
        /// 仅在听写题或答案已揭示后重放当前题音频。
        /// </summary>
        /// <param name="cancellationToken">调用方取消令牌，传递到当前异步操作。</param>
        public Task ReplayQuestionAsync(CancellationToken cancellationToken = default)
        {
            EnsureReady();
            if (!CanReplayQuestion) return Task.CompletedTask;
            return RunOperationAsync(PlayQuestionCoreAsync, cancellationToken, "播放发音失败");
        }

        /// <summary>
        /// 播放当前浏览词形；日语优先使用读音。
        /// </summary>
        /// <param name="cancellationToken">调用方取消令牌，传递到当前异步操作。</param>
        public Task ReplayBrowseAsync(CancellationToken cancellationToken = default)
        {
            EnsureReady();
            if (isBusy || !isBrowsing || selectedBrowseForm is null) return Task.CompletedTask;
            var form = selectedBrowseForm;
            return RunOperationAsync(token => PlayTextAsync(
                selectedLanguage == LearningLanguage.Japanese ? form.Reading ?? form.Surface : form.Surface,
                token), cancellationToken, "播放词形失败");
        }

        /// <summary>
        /// 听写题使用题目发音；看义写词只在反馈已经揭示答案后播放，成功后更新已听状态。
        /// </summary>
        private async Task PlayQuestionCoreAsync(CancellationToken cancellationToken)
        {
            if (question is null) return;
            var text = question.SpokenText;
            if (question.Channel != LearningChannel.Dictation)
            {
                if (feedback is null)
                {
                    return;
                }
                text = feedback.SpokenText;
            }

            await PlayTextAsync(text ?? throw new InvalidOperationException("听写题缺少发音内容。"), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            hasHeardQuestion = true;
            NotifyState();
        }

        private async Task PlayTextAsync(string text, CancellationToken cancellationToken)
        {
            var language = selectedLanguage == LearningLanguage.Japanese ? TtsLanguage.Japanese : TtsLanguage.English;
            var audio = await ttsService.SynthesizeAsync(new TtsSynthesisRequestDto(language, text), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await audioPlayer.PlayWavAsync(audio.WavData, cancellationToken);
        }

        private Task SetBrowsingAsync(bool value)
        {
            isBrowsing = value;
            NotifyState();
            return Task.CompletedTask;
        }

        /// <summary>返回上级时立即取消当前操作，旧页动画结束前也不允许晚结果回写。</summary>
        public void CancelOperation() => currentOperation?.Cancel();

        /// <summary>
        /// 用非阻塞信号量防止重叠 UI 操作，关联页面寿命取消；业务错误写入提示，取消继续向上传播。
        /// </summary>
        private async Task RunOperationAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken,
            string errorPrefix)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await operationGate.WaitAsync(0, cancellationToken)) return;
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
            currentOperation = linkedCancellation;
            isBusy = true;
            errorMessage = null;
            NotifyState();
            try
            {
                await operation(linkedCancellation.Token);
            }
            catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                linkedCancellation.Token.ThrowIfCancellationRequested();
                if (!isDisposed)
                {
                    var message = exception.Message;
                    // 保留业务错误分类，同时展示可处理的语音失败原因，不能只提示包装层失败。
                    if (exception is TtsSynthesisException { InnerException: { } synthesisCause })
                        message += " " + synthesisCause.Message;
                    errorMessage = $"{errorPrefix}：{message}";
                }
            }
            finally
            {
                currentOperation = null;
                isBusy = false;
                operationGate.Release();
                if (!isDisposed) NotifyState();
            }
        }

        private AsyncCommand CreateCommand(Func<Task> operation, Func<bool> canExecute)
        {
            var command = new AsyncCommand(async () =>
            {
                try { await operation(); }
                catch (OperationCanceledException) { }
            }, () => !isDisposed && canExecute());
            commands.Add(command);
            return command;
        }

        private void EnsureReady()
        {
            ObjectDisposedException.ThrowIf(isDisposed, this);
            if (!isInitialized) throw new InvalidOperationException("练习会话尚未初始化语言。");
        }

        private void NotifyState()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsBusy)));
            foreach (var command in commands) command.NotifyCanExecuteChanged();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (isDisposed) return;
            isDisposed = true;
            lifetime.Cancel();
            lifetime.Dispose();
            NotifyState();
        }
    }
}
