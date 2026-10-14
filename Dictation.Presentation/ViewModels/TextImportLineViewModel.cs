using System.ComponentModel;
using System.Runtime.CompilerServices;
using Dictation.Core.Importing;
using Dictation.Core.Lexicon;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>
    /// 保存文本词表单行的候选选择，不把模糊义项当作已确认的词语。
    /// </summary>
    public sealed class TextImportLineViewModel : INotifyPropertyChanged
    {
        private LexiconLookupResultDto? selectedCandidate;
        private bool isSelected;
        /// <summary>从只读准备结果创建可选择的行。</summary>
        /// <param name="line">包含来源行和精确候选的准备结果。</param>
        public TextImportLineViewModel(TextImportLineDto line)
        {
            ArgumentNullException.ThrowIfNull(line);
            LineNumber = line.LineNumber;
            SourceText = line.SourceText;
            Candidates = line.Candidates;
            selectedCandidate = Candidates.Count == 1 ? Candidates[0] : null;
            isSelected = selectedCandidate is not null;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public int LineNumber { get; }
        public string SourceText { get; }
        public IReadOnlyList<LexiconLookupResultDto> Candidates { get; }
        public LexiconLookupResultDto? SelectedCandidate
        {
            get => selectedCandidate;
            set
            {
                if (value is not null && !Candidates.Contains(value))
                {
                    throw new ArgumentException("选择必须来自本行候选。", nameof(value));
                }
                if (ReferenceEquals(selectedCandidate, value))
                {
                    return;
                }
                selectedCandidate = value;
                IsSelected = value is not null;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSelect));
                OnPropertyChanged(nameof(MatchLabel));
            }
        }

        public bool IsSelected
        {
            get => isSelected;
            set
            {
                var next = value && CanSelect;
                if (isSelected == next)
                {
                    return;
                }
                isSelected = next;
                OnPropertyChanged();
            }
        }

        public bool CanSelect => selectedCandidate is not null;
        public string MatchLabel => Candidates.Count == 0 ? "未匹配，请检查拼写或改用查词"
            : selectedCandidate is null ? $"匹配 {Candidates.Count} 个义项，请选择" : "已选择义项";

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
