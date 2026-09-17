using System.ComponentModel;
using System.Runtime.CompilerServices;
using Dictation.Core.Importing;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>
    /// 管理导入词组成员的只读来源信息与可编辑中文释义草稿。
    /// </summary>
    public sealed class ImportMemberEditorViewModel : INotifyPropertyChanged
    {
        private string chineseMeaningDraft;

        /// <summary>
        /// 管理导入词组成员的只读来源信息与可编辑中文释义草稿。
        /// </summary>
        /// <param name="member">用于显示和编辑的词组成员快照。</param>
        public ImportMemberEditorViewModel(ImportWordGroupMemberDto member)
        {
            Member = member ?? throw new ArgumentNullException(nameof(member));
            chineseMeaningDraft = string.Join(Environment.NewLine, member.Meanings);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ImportWordGroupMemberDto Member { get; }

        public string SourceSenseId => Member.SourceSenseId;

        public string Headword => Member.Headword;

        public string? Reading => Member.Reading;

        public string PartOfSpeechLabel => Member.PartOfSpeechLabel;

        public string AttributeLabel => Member.AttributeLabel;

        public bool IsPhrase => Member.IsPhrase;

        public IReadOnlyList<ImportWordGroupFormDto> Forms => Member.Forms;

        public string ChineseMeaningDraft
        {
            get => chineseMeaningDraft;
            set
            {
                var nextValue = value ?? string.Empty;
                if (string.Equals(chineseMeaningDraft, nextValue, StringComparison.Ordinal))
                {
                    return;
                }

                chineseMeaningDraft = nextValue;
                OnPropertyChanged();
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
