using Dictation.Core.Configuration;
using Dictation.Core.Learning;

namespace Dictation.Tests.Infrastructure
{
    /// <summary>在分页边界控制延迟、失败和数据变动，验证页面实际追加与恢复结果。</summary>
    internal sealed class ControlledLearningLibraryManager : ILearningLibraryManager
    {
        public ControlledLearningLibraryManager(LearningWordGroupDto template, int count)
        {
            Groups = Enumerable.Range(1, count)
                .Select(index => template with { WordGroupId = $"group-{index:D5}" }).ToList();
        }

        public List<LearningWordGroupDto> Groups { get; }
        public List<int> RequestedPages { get; } = new();
        public TaskCompletionSource? PendingRead { get; set; }
        public bool FailNextRead { get; set; }

        public async Task<LearningLibraryPageDto> GetLibraryPageAsync(
            LearningLanguage language, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            RequestedPages.Add(pageNumber);
            Assert.Equal(20, pageSize);
            Assert.All(Groups, group => Assert.Equal(language, group.Language));
            if (PendingRead is not null) await PendingRead.Task.WaitAsync(cancellationToken);
            if (FailNextRead)
            {
                FailNextRead = false;
                throw new IOException("分页读取失败");
            }
            return new LearningLibraryPageDto(pageNumber, pageSize, Groups.Count,
                Groups.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray());
        }

        public Task DeleteWordGroupAsync(
            LearningLanguage language, string wordGroupId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Groups.RemoveAll(group => group.Language == language && group.WordGroupId == wordGroupId);
            return Task.CompletedTask;
        }
    }
}
