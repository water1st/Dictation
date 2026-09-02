using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Dictation.Presentation.Navigation;
using Dictation.Presentation.Shell;
using Dictation.Services.Configuration;
using Dictation.Services.Importing;
using Dictation.Services.Lexicon;

namespace Dictation.Presentation.ViewModels
{
    public sealed class LexiconLookupViewModel : INotifyPropertyChanged
    {
        private readonly LexiconLookupService lexiconLookupService;
        private LearningLanguage selectedLanguage;
        private string query = string.Empty;
        private LexiconLookupResultDto? selectedResult;
        private string? errorMessage;
        private string emptyStateText = "请输入要查询的词语。";
        private bool isResultDropDownOpen;
        private bool isInitialized;
        private readonly ImportInboxService importInboxService;
        private readonly NavigationService navigationService;
        private double scrollOffset;
        private string? lastSuccessfulSourceText;
        private string? importStatusMessage;
        private string? importErrorMessage;

        public LexiconLookupViewModel(
            LexiconLookupService lexiconLookupService,
            ImportInboxService importInboxService,
            NavigationService navigationService,
            OverlayService overlayService)
        {
            this.lexiconLookupService = lexiconLookupService
                ?? throw new ArgumentNullException(nameof(lexiconLookupService));
            this.importInboxService = importInboxService
                ?? throw new ArgumentNullException(nameof(importInboxService));
            this.navigationService = navigationService
                ?? throw new ArgumentNullException(nameof(navigationService));
            ArgumentNullException.ThrowIfNull(overlayService);
            Results = new ObservableCollection<LexiconLookupResultDto>();
            SearchCommand = new AsyncCommand(() => SearchAsync());
            BackCommand = new AsyncCommand(() => this.navigationService.GoBackAsync());
            StageSelectedCommand = new AsyncCommand(async () =>
            {
                await StageSelectedAsync();
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public LearningLanguage SelectedLanguage
        {
            get
            {
                EnsureInitialized();
                return selectedLanguage;
            }
            private set
            {
                if (selectedLanguage == value)
                {
                    return;
                }

                selectedLanguage = value;
                OnPropertyChanged();
            }
        }

        public string WindowTitle => isInitialized
            ? selectedLanguage == LearningLanguage.Japanese ? "日语查词" : "英语查词"
            : "查词";

        public string Query
        {
            get => query;
            set
            {
                var nextValue = value ?? string.Empty;
                if (query == nextValue)
                {
                    return;
                }

                query = nextValue;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<LexiconLookupResultDto> Results { get; }

        public LexiconLookupResultDto? SelectedResult
        {
            get => selectedResult;
            set
            {
                if (ReferenceEquals(selectedResult, value))
                {
                    return;
                }

                selectedResult = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanStageSelected));
            }
        }

        public ICommand SearchCommand { get; }

        public ICommand BackCommand { get; }

        public ICommand StageSelectedCommand { get; }

        public double ScrollOffset
        {
            get => scrollOffset;
            set
            {
                if (scrollOffset == value)
                {
                    return;
                }

                scrollOffset = value;
                OnPropertyChanged();
            }
        }

        public bool CanStageSelected => isInitialized
            && selectedResult is not null
            && !string.IsNullOrWhiteSpace(lastSuccessfulSourceText);

        public string? ImportStatusMessage
        {
            get => importStatusMessage;
            private set
            {
                if (importStatusMessage == value)
                {
                    return;
                }

                importStatusMessage = value;
                OnPropertyChanged();
            }
        }

        public string? ImportErrorMessage
        {
            get => importErrorMessage;
            private set
            {
                if (importErrorMessage == value)
                {
                    return;
                }

                importErrorMessage = value;
                OnPropertyChanged();
            }
        }

        public string? ErrorMessage
        {
            get => errorMessage;
            private set
            {
                if (errorMessage == value)
                {
                    return;
                }

                errorMessage = value;
                OnPropertyChanged();
            }
        }

        public string EmptyStateText
        {
            get => emptyStateText;
            private set
            {
                if (emptyStateText == value)
                {
                    return;
                }

                emptyStateText = value;
                OnPropertyChanged();
            }
        }

        public bool IsResultDropDownOpen
        {
            get => isResultDropDownOpen;
            set
            {
                if (isResultDropDownOpen == value)
                {
                    return;
                }

                isResultDropDownOpen = value;
                OnPropertyChanged();
            }
        }

        public void Initialize(LearningLanguage language)
        {
            ValidateLanguage(language);
            if (isInitialized)
            {
                throw new InvalidOperationException("查词窗口已经初始化，不能重复初始化语言。");
            }

            selectedLanguage = language;
            isInitialized = true;
            OnPropertyChanged(nameof(SelectedLanguage));
            OnPropertyChanged(nameof(WindowTitle));
            OnPropertyChanged(nameof(CanStageSelected));
        }

        public async Task SearchAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            var searchQuery = Query;
            ResetSearchState();
            Query = searchQuery;
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                EmptyStateText = "请输入要查询的词语。";
                return;
            }

            try
            {
                var results = await lexiconLookupService.SearchAsync(
                    new LexiconSearchRequestDto(selectedLanguage, searchQuery),
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var result in results)
                {
                    Results.Add(result);
                }

                if (Results.Count == 0)
                {
                    EmptyStateText = "未找到匹配词条。";
                    return;
                }

                lastSuccessfulSourceText = searchQuery.Trim().Normalize(System.Text.NormalizationForm.FormC);
                SelectedResult = Results[0];
                IsResultDropDownOpen = true;
                OnPropertyChanged(nameof(CanStageSelected));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                ErrorMessage = $"查词失败：{exception.Message}";
                EmptyStateText = "暂时无法显示结果。";
            }
        }

        public async Task<StageImportBatchResultDto?> StageSelectedAsync(
            CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            ImportStatusMessage = null;
            ImportErrorMessage = null;
            cancellationToken.ThrowIfCancellationRequested();

            if (selectedResult is null || string.IsNullOrWhiteSpace(lastSuccessfulSourceText))
            {
                ImportErrorMessage = "请选择一个查词结果后再加入导入收件箱。";
                return null;
            }

            try
            {
                var result = await importInboxService.StageAsync(
                    new StageImportBatchRequestDto(
                        selectedLanguage,
                        ImportSourceKind.Manual,
                        new[]
                        {
                            new ImportSelectionDto(
                                lastSuccessfulSourceText,
                                selectedResult.SourceSenseId)
                        }),
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                ImportStatusMessage = result switch
                {
                    { AddedCount: > 0, DuplicateCount: > 0 }
                        => $"已加入导入收件箱：新增 {result.AddedCount} 项，忽略重复 {result.DuplicateCount} 项。",
                    { DuplicateCount: > 0 }
                        => "词组已存在，未重复添加。",
                    _ => $"已加入导入收件箱：新增 {result.AddedCount} 项。"
                };
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                ImportErrorMessage = $"加入导入收件箱失败：{exception.Message}";
                return null;
            }
        }

        private void ResetSearchState()
        {
            Results.Clear();
            SelectedResult = null;
            ErrorMessage = null;
            ImportStatusMessage = null;
            ImportErrorMessage = null;
            lastSuccessfulSourceText = null;
            EmptyStateText = string.Empty;
            IsResultDropDownOpen = false;
            OnPropertyChanged(nameof(CanStageSelected));
        }

        private void EnsureInitialized()
        {
            if (!isInitialized)
            {
                throw new InvalidOperationException("查词窗口尚未初始化语言。");
            }
        }

        private static void ValidateLanguage(LearningLanguage language)
        {
            if (language is not (LearningLanguage.Japanese or LearningLanguage.English))
            {
                throw new ArgumentOutOfRangeException(nameof(language), language, "查词语言值无效。");
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
