using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Dictation.Core.Importing;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>
    /// 管理导入词组成员的来源信息、中文释义草稿及保存反馈。
    /// </summary>
    public sealed class ImportMemberEditorViewModel : INotifyPropertyChanged
    {
        private string chineseMeaningDraft;
        private readonly IImportInboxActions importInboxActions;
        private readonly string wordGroupId;
        private string? saveStatusMessage;

        /// <summary>管理一个成员的中文释义编辑与保存。</summary>
        /// <param name="member">用于显示和编辑的词组成员快照。</param>
        /// <param name="wordGroupId">该成员所属的待确认词组编号。</param>
        /// <param name="importInboxActions">保存待确认中文释义的业务能力。</param>
        public ImportMemberEditorViewModel(
            ImportWordGroupMemberDto member,
            string wordGroupId,
            IImportInboxActions importInboxActions)
        {
            Member = member ?? throw new ArgumentNullException(nameof(member));
            ArgumentException.ThrowIfNullOrWhiteSpace(wordGroupId);
            this.wordGroupId = wordGroupId;
            this.importInboxActions = importInboxActions
                ?? throw new ArgumentNullException(nameof(importInboxActions));
            chineseMeaningDraft = string.Join(Environment.NewLine, member.Meanings);
            SaveCommand = new AsyncCommand(() => SaveAsync());
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

        public ICommand SaveCommand { get; }

        public string? SaveStatusMessage
        {
            get => saveStatusMessage;
            private set
            {
                if (string.Equals(saveStatusMessage, value, StringComparison.Ordinal))
                {
                    return;
                }

                saveStatusMessage = value;
                OnPropertyChanged();
            }
        }

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
                SaveStatusMessage = null;
            }
        }

        /// <summary>
        /// 保存当前成员的中文释义草稿。
        /// </summary>
        /// <param name="cancellationToken">取消保存操作的令牌。</param>
        public async Task SaveAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var meanings = chineseMeaningDraft.Split(
                new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (meanings.Length == 0)
            {
                SaveStatusMessage = "请至少填写一条中文释义。";
                return;
            }

            try
            {
                await importInboxActions.UpdateMeaningsAsync(
                    new UpdateImportMeaningsRequestDto(wordGroupId, Member.SourceSenseId, meanings),
                    cancellationToken);
                SaveStatusMessage = "中文释义已保存。";
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                SaveStatusMessage = $"保存中文释义失败：{exception.Message}";
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
