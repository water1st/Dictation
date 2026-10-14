using Dictation.Core.Configuration;

namespace Dictation.Core.Mistakes
{
    /// <summary>隔离错题只读能力，供页面及其它业务使用，不暴露重练或数据库操作。</summary>
    public interface IWrongAnswerQuery
    {
        /// <summary>读取该语言按成员词形归组的累计历史错误及原记忆到期时间。</summary>
        /// <param name="language">首页固定的语言。</param>
        /// <param name="cancellationToken">取消读取的令牌。</param>
        Task<IReadOnlyList<WrongAnswerItemDto>> LoadAsync(LearningLanguage language, CancellationToken cancellationToken = default);
    }
}
