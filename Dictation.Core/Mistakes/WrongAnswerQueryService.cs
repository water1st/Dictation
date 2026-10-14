using Dictation.Core.Configuration;
using Dictation.DataAccess.Mistakes;

namespace Dictation.Core.Mistakes
{
    /// <summary>校验错题语言并返回纯查询投影，不改变学习或FSRS状态。</summary>
    internal sealed class WrongAnswerQueryService : IWrongAnswerQuery
    {
        private readonly WrongAnswerDataAccessor dataAccessor;

        /// <summary>注入只读历史访问组件。</summary>
        /// <param name="dataAccessor">读取原用户错误观察的访问组件。</param>
        public WrongAnswerQueryService(WrongAnswerDataAccessor dataAccessor)
        {
            this.dataAccessor = dataAccessor ?? throw new ArgumentNullException(nameof(dataAccessor));
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<WrongAnswerItemDto>> LoadAsync(LearningLanguage language, CancellationToken cancellationToken = default)
        {
            if (language is not (LearningLanguage.Japanese or LearningLanguage.English))
                throw new ArgumentOutOfRangeException(nameof(language));
            cancellationToken.ThrowIfCancellationRequested();
            return WrongAnswerMapper.ToDtos(await dataAccessor.LoadAsync(language.ToString(), cancellationToken));
        }
    }
}
