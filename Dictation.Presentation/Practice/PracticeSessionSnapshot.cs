using Dictation.Core.Configuration;

namespace Dictation.Presentation.Practice
{
    /// <summary>供共用页面绑定的模式中立快照，不替代 Core 的学习或复习业务 DTO。</summary>
    /// <param name="SessionId">原业务会话标识。</param>
    /// <param name="Language">不可在本轮切换的语言。</param>
    /// <param name="IsCompleted">原学习会话是否已经完成。</param>
    /// <param name="WordGroupIds">本轮整组范围。</param>
    /// <param name="FormCodes">本轮浏览启用的词形。</param>
    public sealed record PracticeSessionSnapshot(string SessionId, LearningLanguage Language, bool IsCompleted,
        IReadOnlyList<string> WordGroupIds, IReadOnlyList<string> FormCodes);
}
