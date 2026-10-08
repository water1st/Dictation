using Dictation.Core.Practice;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Learning;
using Dictation.Core.Tts;
using Dictation.Presentation.Audio;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>
    /// 协调学习页面状态、业务调用与内存音频播放，不执行判题或持久化规则。
    /// </summary>
    public sealed class LearningSessionViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly ILearningSessionOperations sessionOperations;
        private readonly ILearningPlanReader planReader;
        private readonly TtsService ttsService;
        private readonly IAudioPlayer audioPlayer;
        private readonly SemaphoreSlim operationGate = new(1, 1);
        private readonly CancellationTokenSource lifetime = new();
        private readonly List<AsyncCommand> commands = new();
        private CancellationTokenSource? currentOperation;
        private LearningLanguage selectedLanguage;
        private LearningSessionDto? session;
        private PracticeQuestionDto? question;
        private LearningAnswerResultDto? feedback;
        private LearningSessionSummaryDto? summary;
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
        /// 协调学习页面状态、业务调用与内存音频播放，不执行判题或持久化规则。
        /// </summary>
        /// <param name="sessionOperations">启动、读取和提交学习会话的业务能力。</param>
        /// <param name="planReader">读取当前语言计划的能力。</param>
        /// <param name="ttsService">生成题目或浏览词形音频的业务服务。</param>
        /// <param name="audioPlayer">播放内存 WAV 数据的可替换输出适配器。</param>
        public LearningSessionViewModel(
            ILearningSessionOperations sessionOperations,
            ILearningPlanReader planReader,
            TtsService ttsService,
            IAudioPlayer audioPlayer)
        {
            this.sessionOperations = sessionOperations ?? throw new ArgumentNullException(nameof(sessionOperations));
            this.planReader = planReader ?? throw new ArgumentNullException(nameof(planReader));
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
        public string WindowTitle => selectedLanguage == LearningLanguage.Japanese ? "日语学习" : "英语学习";
        public LearningSessionDto? Session => session;
        public PracticeQuestionDto? Question => question;
        public LearningAnswerResultDto? Feedback => feedback;
        public LearningSessionSummaryDto? Summary => summary;
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
                throw new ArgumentOutOfRangeException(nameof(language), language, "学习语言无效。");
            if (isInitialized && language != selectedLanguage)
                throw new InvalidOperationException("学习会话开始后不能切换语言。");
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
                var plan = await planReader.LoadAsync(selectedLanguage, token);
                enabledFormCodes = plan.FormCodes.ToHashSet(StringComparer.Ordinal);
                session = await sessionOperations.StartOrResumeAsync(
                    new StartLearningSessionRequestDto(selectedLanguage, selectedWordGroupIds), token);
                var members = await sessionOperations.GetBrowseItemsAsync(session.SessionId, token);
                token.ThrowIfCancellationRequested();
                BrowseMembers.Clear();
                foreach (var member in members.SelectMany(group => group.Members)) BrowseMembers.Add(member);
                SelectedBrowseMember = BrowseMembers.FirstOrDefault();
                question = await sessionOperations.GetCurrentQuestionAsync(session.SessionId, token);
                feedback = null;
                summary = session.Status == LearningSessionStatus.Completed
                    ? new LearningSessionSummaryDto(session.SessionId, 0, Array.Empty<LearningErrorSummaryDto>())
                    : null;
                answerText = string.Empty;
                isBrowsing = false;
                hasHeardQuestion = false;
                NotifyState();
                if (question?.Channel == LearningChannel.Dictation) await PlayQuestionCoreAsync(token);
            }, cancellationToken, "准备学习失败");
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
                feedback = await sessionOperations.SubmitAnswerAsync(
                    new SubmitLearningAnswerRequestDto(question!.SessionId, question.QuestionId, answerText), token);
                NotifyState();
            }, cancellationToken, "提交答案失败");
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
            hasHeardQuestion = true;
            NotifyState();
        }

        private async Task PlayTextAsync(string text, CancellationToken cancellationToken)
        {
            var language = selectedLanguage == LearningLanguage.Japanese ? TtsLanguage.Japanese : TtsLanguage.English;
            var audio = await ttsService.SynthesizeAsync(new TtsSynthesisRequestDto(language, text), cancellationToken);
            await audioPlayer.PlayWavAsync(audio.WavData, cancellationToken);
        }

        private Task SetBrowsingAsync(bool value)
        {
            isBrowsing = value;
            NotifyState();
            return Task.CompletedTask;
        }

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
                errorMessage = $"{errorPrefix}：{exception.Message}";
            }
            finally
            {
                currentOperation = null;
                isBusy = false;
                operationGate.Release();
                NotifyState();
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
            if (!isInitialized) throw new InvalidOperationException("学习会话尚未初始化语言。");
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
