using Dictation.Core.Kana;

namespace Dictation.Tests.Infrastructure
{
    /// <summary>在与五十音无关的首页单元测试中提供最小只读统计边界。</summary>
    internal sealed class EmptyKanaProgressReader : IKanaProgressReader
    {
        public Task<KanaProgressDto> GetProgressAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new KanaProgressDto(false, 0, 0, 0, false));
    }
}
