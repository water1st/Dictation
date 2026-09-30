using Dictation.Core.Configuration;

namespace Dictation.Presentation.ViewModels
{
    /// <summary>为两类计划创建相同的可绑定选项，每次返回独立勾选状态，不共享用户设置。</summary>
    internal static class PracticePlanOptionFactory
    {
        /// <summary>沿用已支持的语言词形标签，不推导词条是否适用。</summary>
        /// <param name="language">决定可选标签的语言。</param>
        /// <returns>独立创建的词形选项。</returns>
        internal static IEnumerable<LearningPlanOptionViewModel> CreateForms(LearningLanguage language)
        {
            var values = language == LearningLanguage.Japanese
                ? new[] { ("Dictionary", "普通形"), ("Polite", "礼貌形"), ("Te", "て形"), ("Ta", "た形") }
                : new[]
                {
                    ("Base", "原形"), ("PresentFirstPersonSingular", "第一人称单数"), ("PresentNonThirdPerson", "非第三人称现在式"),
                    ("ThirdPersonSingular", "第三人称单数"), ("Past", "过去式"), ("PastParticiple", "过去分词"),
                    ("PresentParticiple", "现在分词"), ("Plural", "复数"), ("Comparative", "比较级"), ("Superlative", "最高级")
                };
            return values.Select(value => new LearningPlanOptionViewModel(value.Item1, value.Item2));
        }

        /// <summary>创建听写与看义默写的独立勾选项。</summary>
        /// <returns>两种渠道选项。</returns>
        internal static IEnumerable<LearningPlanOptionViewModel> CreateChannels() => new[]
        {
            new LearningPlanOptionViewModel("MeaningWriting", "看义默写"), new LearningPlanOptionViewModel("Dictation", "听写")
        };

        /// <summary>按语言创建字形选项，英语不显示日文字形。</summary>
        /// <param name="language">当前计划语言。</param>
        /// <returns>独立创建的字形选项。</returns>
        internal static IEnumerable<LearningPlanOptionViewModel> CreateOutputs(LearningLanguage language) => language == LearningLanguage.Japanese
            ? new[] { new LearningPlanOptionViewModel("Kana", "假名"), new LearningPlanOptionViewModel("Kanji", "汉字") }
            : new[] { new LearningPlanOptionViewModel("Latin", "英文字母") };
    }
}
