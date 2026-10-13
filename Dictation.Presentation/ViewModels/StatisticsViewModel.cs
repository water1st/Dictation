using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Dictation.Core.Configuration;
using Dictation.Core.Statistics;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>管理固定语言统计页面的月份、绑定和请求生命周期，不承担统计或打卡规则。</summary>
    public sealed class StatisticsViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly ICheckInCalendarReader calendar;
        private readonly ILanguageStatisticsReader reports;
        private readonly TimeProvider clock;
        private readonly CancellationTokenSource lifetime = new();
        private DateOnly month;
        private LearningLanguage language;
        private bool initialized;
        private bool disposed;
        private long refreshVersion;
        private IReadOnlyList<CalendarDayViewModel> cells = Array.Empty<CalendarDayViewModel>();
        private string? calendarErrorMessage;
        private long reportVersion;
        private StatisticsReportViewModel? report;
        private string? reportErrorMessage;

        /// <summary>组合只读月历服务与时钟，作用域释放时停止发布旧结果。</summary>
        /// <param name="calendar">业务层历史月历查询。</param>
        /// <param name="clock">确定本月的统一时钟。</param>
        /// <param name="reports">只读当前语言的统计报表。</param>
        public StatisticsViewModel(ICheckInCalendarReader calendar, TimeProvider clock, ILanguageStatisticsReader reports)
        {
            this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
            this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
            this.reports = reports ?? throw new ArgumentNullException(nameof(reports));
            var now = clock.GetLocalNow();
            month = new DateOnly(now.Year, now.Month, 1);
            PreviousMonthCommand = new AsyncCommand(() => ShowMonthAsync(month.AddMonths(-1).Year, month.AddMonths(-1).Month), () => month.Year > 1 || month.Month > 1);
            NextMonthCommand = new AsyncCommand(() => ShowMonthAsync(month.AddMonths(1).Year, month.AddMonths(1).Month), () => month.Year < 9999 || month.Month < 12);
            CurrentMonthCommand = new AsyncCommand(() => ShowMonthAsync(clock.GetLocalNow().Year, clock.GetLocalNow().Month));
            RefreshCommand = new AsyncCommand(() => RefreshAsync());
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public LearningLanguage Language => language;
        public string WindowTitle => language == LearningLanguage.Japanese ? "日语 · 学习足迹" : "英语 · 学习足迹";
        public string MonthLabel => $"{month.Year}年{month.Month}月";
        public IReadOnlyList<CalendarDayViewModel> Cells => cells;
        public string? CalendarErrorMessage => calendarErrorMessage;
        public StatisticsReportViewModel? Report => report;
        public string? ReportErrorMessage => reportErrorMessage;
        public ICommand PreviousMonthCommand { get; }
        public ICommand NextMonthCommand { get; }
        public ICommand CurrentMonthCommand { get; }
        public ICommand RefreshCommand { get; }

        /// <summary>固定页面语言，只允许初始化一次。</summary>
        /// <param name="language">从首页传入的语言。</param>
        public void Initialize(LearningLanguage language)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (language is not (LearningLanguage.Japanese or LearningLanguage.English)) throw new ArgumentOutOfRangeException(nameof(language));
            if (initialized) throw new InvalidOperationException("统计页面不能切换语言，请返回首页。");
            this.language = language;
            initialized = true;
            OnPropertyChanged(nameof(Language));
            OnPropertyChanged(nameof(WindowTitle));
        }

        /// <summary>选择有效月份并刷新；最大日期不能溢出下一年。</summary>
        /// <param name="year">1–9999年。</param>
        /// <param name="month">1–12月。</param>
        /// <param name="cancellationToken">取消选择和读取。</param>
        public Task ShowMonthAsync(int year, int month, CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            this.month = new DateOnly(year, month, 1);
            OnPropertyChanged(nameof(MonthLabel));
            ((AsyncCommand)PreviousMonthCommand).NotifyCanExecuteChanged();
            ((AsyncCommand)NextMonthCommand).NotifyCanExecuteChanged();
            return RefreshCalendarAsync(cancellationToken);
        }

        /// <summary>异步刷新当前月份，只让最新请求更新绑定；错误留在页面内。</summary>
        /// <param name="cancellationToken">取消查询。</param>
        public Task RefreshAsync(CancellationToken cancellationToken = default) =>
            Task.WhenAll(RefreshCalendarAsync(cancellationToken), RefreshReportAsync(cancellationToken));

        /// <summary>独立刷新报表；月份导航不重复查询与月份无关的统计。</summary>
        /// <param name="cancellationToken">取消本次报表读取。</param>
        public async Task RefreshReportAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!initialized) throw new InvalidOperationException("请先初始化统计语言。");
            cancellationToken.ThrowIfCancellationRequested();
            var version = Interlocked.Increment(ref reportVersion);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
            try
            {
                var result = await reports.LoadAsync(language, linked.Token);
                linked.Token.ThrowIfCancellationRequested();
                if (disposed || version != Volatile.Read(ref reportVersion)) return;
                if (result.Language != language) throw new InvalidDataException("报表语言与页面不符。");
                report = new StatisticsReportViewModel(result);
                reportErrorMessage = null;
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { return; }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (disposed || version != Volatile.Read(ref reportVersion)) return;
                report = null;
                reportErrorMessage = $"读取统计失败：{exception.Message}";
            }
            OnPropertyChanged(nameof(Report));
            OnPropertyChanged(nameof(ReportErrorMessage));
        }

        /// <summary>只刷新当前月份的日历并隔离迟到结果。</summary>
        /// <param name="cancellationToken">取消月历查询。</param>
        private async Task RefreshCalendarAsync(CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!initialized) throw new InvalidOperationException("请先初始化统计语言。");
            cancellationToken.ThrowIfCancellationRequested();
            var requested = month;
            var version = Interlocked.Increment(ref refreshVersion);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token);
            try
            {
                var result = await calendar.LoadMonthAsync(requested.Year, requested.Month, linked.Token);
                linked.Token.ThrowIfCancellationRequested();
                if (disposed || version != Volatile.Read(ref refreshVersion) || month != requested) return;
                if (result.Year != requested.Year || result.Month != requested.Month) throw new InvalidDataException("月历结果与请求月份不符。");
                // 周一为首列；补齐六行让不同月份的布局和徽章位置保持稳定。
                var monthCells = new List<CalendarDayViewModel>(42);
                var leadingDays = ((int)requested.DayOfWeek + 6) % 7;
                for (var index = 0; index < leadingDays; index++)
                {
                    monthCells.Add(new CalendarDayViewModel(null));
                }
                foreach (var day in result.Days)
                {
                    monthCells.Add(new CalendarDayViewModel(day));
                }
                while (monthCells.Count < 42)
                {
                    monthCells.Add(new CalendarDayViewModel(null));
                }
                cells = monthCells;
                calendarErrorMessage = null;
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { return; }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (disposed || version != Volatile.Read(ref refreshVersion) || month != requested) return;
                cells = Array.Empty<CalendarDayViewModel>();
                calendarErrorMessage = $"读取月历失败：{exception.Message}";
            }
            OnPropertyChanged(nameof(Cells));
            OnPropertyChanged(nameof(CalendarErrorMessage));
        }

        /// <summary>取消作用域内读取并禁止离开页面后的结果发布。</summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Interlocked.Increment(ref refreshVersion);
            lifetime.Cancel();
            lifetime.Dispose();
        }

        /// <summary>通知WPF绑定变化。</summary>
        /// <param name="name">变化属性名称。</param>
        private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
