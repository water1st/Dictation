using Dictation.Core.Mistakes;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>绑定一个只读成员词形错题及选择状态，不承载记忆或查询规则。</summary>
    public sealed class WrongAnswerItemViewModel : INotifyPropertyChanged
    {
        private bool isSelected;

        /// <summary>接收业务层的历史错误与当前到期快照。</summary>
        /// <param name="item">同语言独立义项词形的只读错误。</param>
        public WrongAnswerItemViewModel(WrongAnswerItemDto item)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public WrongAnswerItemDto Item { get; }
        public string Heading => $"{Item.Headword} · {Item.FormLabel}";
        public string HistoryLabel => $"累计错误 {Item.Attempts.Count} 次";
        public string PlacementLabel => Item.Placement == "Review" ? "复习中" : "学习中";
        public string NextReviewLabel => Item.DueAtUtc is { } due
            ? $"下次复习 {due.ToLocalTime():yyyy-MM-dd HH:mm}" : "完成本轮学习后建立复习记忆";
        public bool IsReview => Item.Placement == "Review";
        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (isSelected == value) return;
                isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }
}
