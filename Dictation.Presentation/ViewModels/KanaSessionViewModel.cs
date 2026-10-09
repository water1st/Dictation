using System.ComponentModel;
using System.Windows.Input;
using Dictation.Core.Kana;
using Dictation.Core.Tts;
using Dictation.Presentation.Audio;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>呈现五十音题面、内存音频、即时反馈与结算；判题和记忆全部由Core执行。</summary>
    public sealed class KanaSessionViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly IKanaSessionOperations operations;
        private readonly IKanaSessionReader reader;
        private readonly TtsService tts;
        private readonly IAudioPlayer player;
        private readonly List<AsyncCommand> commands = new();
        private readonly CancellationTokenSource pageLifetime = new();
        private CancellationTokenSource? operation;
        private bool disposed;
        private bool leavingPage;
        private bool hasHeardQuestion;
        private string answerText = string.Empty;

        /// <summary>组合独立听写业务、当前题读取与已有内存语音适配。</summary>
        /// <param name="operations">启动、恢复和提交客观作答。</param>
        /// <param name="reader">读取原轮次当前题。</param>
        /// <param name="tts">生成实际目标假名的内存音频。</param>
        /// <param name="player">播放后释放音频，不持久化文件。</param>
        public KanaSessionViewModel(IKanaSessionOperations operations, IKanaSessionReader reader, TtsService tts, IAudioPlayer player)
        {
            this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
            this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
            this.tts = tts ?? throw new ArgumentNullException(nameof(tts));
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            PlayCommand = Command(() => PlayAsync(), () => !IsBusy && Question is not null && !IsCompleted);
            SubmitCommand = Command(() => SubmitAsync(), () => CanSubmit);
            ContinueCommand = Command(() => ContinueAsync(), () => !IsBusy && IsFeedbackVisible);
            RecoverSubmissionCommand = Command(() => RecoverSubmissionAsync(), () => !IsBusy && HasPendingFeedback);
            CancelCommand = Command(() => { CancelOperation(); return Task.CompletedTask; }, () => IsBusy);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public KanaSessionDto? Session { get; private set; }
        public KanaQuestionDto? Question { get; private set; }
        public KanaFeedbackDto? Feedback { get; private set; }
        public string AnswerText { get => answerText; set { if (answerText == value) return; answerText = value; Notify(); } }
        public bool IsBusy { get; private set; }
        public bool IsFeedbackVisible { get; private set; }
        public bool IsCompleted { get; private set; }
        public bool HasPendingFeedback { get; private set; }
        public bool IsEmpty => !IsBusy && Session is null;
        public bool IsAnswering => Question is not null && !IsFeedbackVisible && !IsCompleted;
        public string? ErrorMessage { get; private set; }
        public string ProgressLabel => $"进度 {Feedback?.CompletedCount ?? Session?.CompletedCount ?? 0}/{Session?.TotalCount ?? 0}";
        public string QuestionPrompt => Question is null ? string.Empty :
            $"{(Question.Script == KanaScript.Hiragana ? "平假名" : "片假名")} · {RangeName(Question.Range)}{(string.IsNullOrEmpty(Question.RowHint) ? "" : " · " + Question.RowHint)}";
        public string CorrectAnswerDisplay => IsFeedbackVisible ? Feedback?.CorrectAnswer ?? string.Empty : string.Empty;
        public string FeedbackLabel => Feedback?.IsCorrect == true ? "正确！" : "再看一遍正确写法，稍后会再练。";
        public IReadOnlyList<KanaErrorDto> Errors => Feedback?.Errors ?? Array.Empty<KanaErrorDto>();
        public ICommand PlayCommand { get; }
        public ICommand SubmitCommand { get; }
        public ICommand ContinueCommand { get; }
        public ICommand RecoverSubmissionCommand { get; }
        public ICommand CancelCommand { get; }
        private bool CanSubmit => !IsBusy && IsAnswering && hasHeardQuestion && !HasPendingFeedback;

        /// <summary>开始或恢复原轮次，首题播放成功前不允许计入作答。</summary>
        /// <param name="cancellationToken">取消准备和播放。</param>
        public Task StartAsync(CancellationToken cancellationToken = default) => RunAsync(async token =>
        {
            var session = await Task.Run(() => operations.StartOrResumeAsync(token), token);
            token.ThrowIfCancellationRequested();
            Session = session; Feedback = null; IsCompleted = false; IsFeedbackVisible = false;
            Question = session is null ? null : await Task.Run(() => reader.GetCurrentQuestionAsync(session.SessionId, token), token);
            token.ThrowIfCancellationRequested();
            hasHeardQuestion = false;
            if (Question is not null) await PlayCoreAsync(token);
        }, cancellationToken);

        /// <summary>重放当前目标假名；合成或播放失败不产生答题观察。</summary>
        /// <param name="cancellationToken">取消合成与播放。</param>
        public Task PlayAsync(CancellationToken cancellationToken = default) => RunAsync(async token =>
        {
            if (Question is not null && !IsCompleted) await PlayCoreAsync(token);
        }, cancellationToken);

        /// <summary>提交当前题的输入并立即呈现正确答案，下一题等待用户继续。</summary>
        /// <param name="cancellationToken">取消业务提交。</param>
        public Task SubmitAsync(CancellationToken cancellationToken = default) => RunAsync(async token =>
        {
            if (!IsAnswering || !hasHeardQuestion) { ErrorMessage = "请先成功播放当前发音，再提交答案。"; return; }
            if (HasPendingFeedback) { ErrorMessage = "请先回读上一笔提交的结果，避免重复作答。"; return; }
            var question = Question!;
            HasPendingFeedback = true;
            KanaFeedbackDto feedback;
            try
            {
                feedback = await Task.Run(() => operations.SubmitAsync(question.SessionId, question.QuestionOrdinal, AnswerText, token), token);
            }
            catch
            {
                // 用户取消不是事务回滚证明。仍在本页时只读确认；离页后禁止回写旧页。
                if (disposed || leavingPage) throw;
                var recovered = await ReadSubmissionAsync(pageLifetime.Token);
                if (recovered is null) { HasPendingFeedback = false; throw; }
                feedback = recovered;
            }
            PublishFeedback(feedback);
        }, cancellationToken);

        /// <summary>提交结果读取失败后仅回读原观察，不再次提交或更新记忆。</summary>
        /// <param name="cancellationToken">取消本次只读恢复。</param>
        public Task RecoverSubmissionAsync(CancellationToken cancellationToken = default) => RunAsync(async token =>
        {
            if (!HasPendingFeedback || Question is null) return;
            var feedback = await ReadSubmissionAsync(token);
            token.ThrowIfCancellationRequested();
            if (feedback is null)
            {
                HasPendingFeedback = false;
                ErrorMessage = "上一笔作答尚未保存，可以重新提交。";
                return;
            }
            PublishFeedback(feedback);
        }, cancellationToken);

        /// <summary>从Core回读原题序，包括已经完成的轮次，不创建新轮次。</summary>
        /// <param name="token">取消只读确认。</param>
        /// <returns>原题序已保存反馈，未保存时为空。</returns>
        private Task<KanaFeedbackDto?> ReadSubmissionAsync(CancellationToken token)
        {
            var question = Question!;
            return Task.Run(() => reader.GetSubmissionFeedbackAsync(question.SessionId, question.QuestionOrdinal, token), token);
        }

        /// <summary>已提交反馈允许在本页取消后呈现，但退场动画期间也不得污染旧页。</summary>
        /// <param name="feedback">已经成功提交或回读确认的反馈。</param>
        private void PublishFeedback(KanaFeedbackDto feedback)
        {
            if (disposed || leavingPage) return;
            Feedback = feedback; IsFeedbackVisible = true; HasPendingFeedback = false;
        }

        /// <summary>反馈确认后进入原业务队列下一题，全部完成则显示结算及原始错题。</summary>
        /// <param name="cancellationToken">取消下一题准备与播放。</param>
        public Task ContinueAsync(CancellationToken cancellationToken = default) => RunAsync(async token =>
        {
            if (!IsFeedbackVisible || Feedback is null) return;
            Question = Feedback.NextQuestion; IsFeedbackVisible = false; hasHeardQuestion = false; answerText = string.Empty;
            IsCompleted = Feedback.IsCompleted;
            if (Question is not null) await PlayCoreAsync(token);
        }, cancellationToken);

        /// <summary>只播放内部语音文本，不把待答假名或技术标识绑定到题面。</summary>
        /// <param name="token">取消合成及内存播放。</param>
        private async Task PlayCoreAsync(CancellationToken token)
        {
            hasHeardQuestion = false;
            var audio = await tts.SynthesizeAsync(new(TtsLanguage.Japanese, Question!.SpokenText), token);
            token.ThrowIfCancellationRequested();
            await player.PlayWavAsync(audio.WavData, token);
            token.ThrowIfCancellationRequested();
            hasHeardQuestion = true;
        }

        /// <summary>串行操作并取消迟到结果，音频错误只显示提示，不伪造判题。</summary>
        /// <param name="action">当前听写页面的业务或音频操作。</param>
        /// <param name="token">调用方取消令牌，页面内取消只停止当前操作。</param>
        private async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken token)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            token.ThrowIfCancellationRequested();
            if (IsBusy) return;
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, pageLifetime.Token);
            operation = cancellation; IsBusy = true; ErrorMessage = null; Notify();
            try { await action(cancellation.Token); }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { token.ThrowIfCancellationRequested(); }
            catch (Exception exception)
            {
                token.ThrowIfCancellationRequested();
                if (!disposed && !leavingPage && !cancellation.IsCancellationRequested)
                    ErrorMessage = $"五十音操作失败：{exception.Message} {exception.InnerException?.Message}".TrimEnd();
            }
            finally { operation = null; IsBusy = false; if (!disposed && !leavingPage) Notify(); }
        }

        private AsyncCommand Command(Func<Task> action, Func<bool> canExecute)
        {
            var command = new AsyncCommand(action, () => !disposed && !leavingPage && canExecute());
            commands.Add(command); return command;
        }
        private void Notify()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            foreach (var command in commands) command.NotifyCanExecuteChanged();
        }
        /// <summary>把已校验音类转换为题型提示，不包含目标答案。</summary>
        /// <param name="range">本题的音类。</param>
        /// <returns>用于题面显示的中文音类名称。</returns>
        private static string RangeName(KanaRange range) => range switch
        {
            KanaRange.Clear => "清音", KanaRange.Voiced => "浊音／半浊音", _ => "拗音"
        };
        /// <summary>返回开始就取消后台操作，不等退出动画结束。</summary>
        /// <param name="leavingPage">为true表示开始退场，禁止任何迟到状态发布；页面内取消为false。</param>
        public void CancelOperation(bool leavingPage = false)
        {
            this.leavingPage |= leavingPage;
            if (this.leavingPage) pageLifetime.Cancel();
            operation?.Cancel();
        }
        /// <inheritdoc />
        public void Dispose() { if (disposed) return; disposed = true; CancelOperation(leavingPage: true); pageLifetime.Dispose(); }
    }
}
