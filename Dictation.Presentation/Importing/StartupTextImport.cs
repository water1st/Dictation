using Dictation.Core.Configuration;
using Dictation.Presentation.Navigation;

namespace Dictation.Presentation.Importing
{
    /// <summary>将主实例首次启动的单个 TXT 参数适配到现有导入确认页。</summary>
    internal sealed class StartupTextImport
    {
        private readonly IHomeNavigation navigation;
        /// <summary>组合单窗口导航，不创建额外窗口或第二套导入通道。</summary>
        /// <param name="navigation">首页页面导航能力。</param>
        public StartupTextImport(IHomeNavigation navigation)
        {
            this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        }

        /// <summary>仅识别单个 TXT 文件参数；其他参数不改变当前页面。</summary>
        /// <param name="args">主实例启动参数。</param>
        /// <param name="language">最后使用的学习语言。</param>
        /// <param name="cancellationToken">取消导航的令牌。</param>
        public Task OpenAsync(IReadOnlyList<string> args, LearningLanguage language, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(args);
            cancellationToken.ThrowIfCancellationRequested();
            return args.Count == 1 && string.Equals(Path.GetExtension(args[0]), ".txt", StringComparison.OrdinalIgnoreCase)
                ? navigation.OpenTextImportAsync(language, args[0], cancellationToken)
                : Task.CompletedTask;
        }
    }
}
