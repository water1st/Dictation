using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Dictation.Services.Configuration;
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

        public LexiconLookupViewModel(LexiconLookupService lexiconLookupService)
        {
            this.lexiconLookupService = lexiconLookupService
                ?? throw new ArgumentNullException(nameof(lexiconLookupService));
            Results = new ObservableCollection<LexiconLookupResultDto>();
            SearchCommand = new AsyncCommand(() => SearchAsync());
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
            }
        }

        public ICommand SearchCommand { get; }

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
        }

        public async Task SearchAsync(CancellationToken cancellationToken = default)
        {
            EnsureInitialized();
            ResetSearchState();
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(Query))
            {
                EmptyStateText = "请输入要查询的词语。";
                return;
            }

            try
            {
                var results = await lexiconLookupService.SearchAsync(
                    new LexiconSearchRequestDto(selectedLanguage, Query),
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

                SelectedResult = Results[0];
                IsResultDropDownOpen = true;
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

        private void ResetSearchState()
        {
            Results.Clear();
            SelectedResult = null;
            ErrorMessage = null;
            EmptyStateText = string.Empty;
            IsResultDropDownOpen = false;
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
