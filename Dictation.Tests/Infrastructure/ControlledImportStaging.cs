using Dictation.Core.Importing;

namespace Dictation.Tests.Infrastructure
{
    /// <summary>控制暂存完成时机，以便在等待期间切换查词结果。</summary>
    internal sealed class ControlledImportStaging : IImportStaging
    {
        public TaskCompletionSource<StageImportBatchResultDto> Completion { get; } = new();

        public Task<StageImportBatchResultDto> StageAsync(
            StageImportBatchRequestDto request, CancellationToken cancellationToken = default)
        {
            return Completion.Task.WaitAsync(cancellationToken);
        }
    }
}
