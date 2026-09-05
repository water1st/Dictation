using Dictation.Presentation.ViewModels;
using Dictation.Services.Configuration;
using Dictation.Services.Importing;

namespace Dictation.Tests.Presentation
{
    public sealed class ImportInboxDetailViewModelTests
    {
        [Fact]
        public void Initialize_ProjectsEveryMemberOfTheSelectedGroup()
        {
            var batch = CreateBatch(includeWordGroup: true);
            var viewModel = new ImportInboxDetailViewModel();

            viewModel.Initialize(batch, 2);

            Assert.Equal(LearningLanguage.English, viewModel.SelectedLanguage);
            Assert.Same(batch, viewModel.Batch);
            Assert.Equal("went", viewModel.SourceText);
            Assert.Equal("待确认", viewModel.DecisionLabel);
            Assert.Equal("英语导入详情", viewModel.WindowTitle);
            Assert.Collection(
                viewModel.Members,
                member =>
                {
                    Assert.Equal("go", member.Headword);
                    Assert.Null(member.Reading);
                    Assert.Equal("动词", member.PartOfSpeechLabel);
                    Assert.Equal("不规则动词", member.AttributeLabel);
                    Assert.False(member.IsPhrase);
                    Assert.Equal("去\n离开", member.ChineseMeaningDraft.Replace("\r\n", "\n", StringComparison.Ordinal));
                    var form = Assert.Single(member.Forms);
                    Assert.Equal("went", form.Surface);
                    Assert.Equal("过去式", form.FormLabel);
                },
                member =>
                {
                    Assert.Equal("conversion", member.Headword);
                    Assert.Equal("名词", member.PartOfSpeechLabel);
                    Assert.Equal("转化", member.ChineseMeaningDraft);
                });
        }

        [Fact]
        public void Initialize_RejectsInvalidBatchItemLanguageAndRepeatedInitialization()
        {
            var viewModel = new ImportInboxDetailViewModel();
            var batch = CreateBatch(includeWordGroup: true);

            Assert.Throws<ArgumentNullException>(() => viewModel.Initialize(null!, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => viewModel.Initialize(batch, 0));
            Assert.Throws<ArgumentException>(() => viewModel.Initialize(batch, 999));
            Assert.Throws<ArgumentOutOfRangeException>(() => viewModel.Initialize(
                new ImportBatchDto(
                    "invalid-language",
                    (LearningLanguage)99,
                    ImportSourceKind.Manual,
                    ImportBatchStatus.Pending,
                    DateTimeOffset.UtcNow,
                    null,
                    batch.Items),
                2));

            viewModel.Initialize(batch, 2);
            Assert.Throws<InvalidOperationException>(() => viewModel.Initialize(batch, 2));
        }

        [Fact]
        public void Initialize_ItemWithoutSnapshotHasNoMembers()
        {
            var viewModel = new ImportInboxDetailViewModel();

            viewModel.Initialize(CreateBatch(includeWordGroup: false), 2);

            Assert.Empty(viewModel.Members);
            Assert.Equal("went", viewModel.SourceText);
        }

        [Fact]
        public void ScrollOffset_NormalizesNegativeAndNonFiniteValues()
        {
            var viewModel = new ImportInboxDetailViewModel();

            viewModel.ScrollOffset = 73.5;
            Assert.Equal(73.5, viewModel.ScrollOffset);
            viewModel.ScrollOffset = -1;
            Assert.Equal(0, viewModel.ScrollOffset);
            viewModel.ScrollOffset = double.NaN;
            Assert.Equal(0, viewModel.ScrollOffset);
            viewModel.ScrollOffset = double.PositiveInfinity;
            Assert.Equal(0, viewModel.ScrollOffset);
        }

        private static ImportBatchDto CreateBatch(bool includeWordGroup)
        {
            return new ImportBatchDto(
                "batch-detail",
                LearningLanguage.English,
                ImportSourceKind.Manual,
                ImportBatchStatus.Pending,
                DateTimeOffset.UtcNow,
                null,
                new[]
                {
                    new ImportBatchItemDto(
                        1,
                        "unused",
                        "unused%2:00:00::",
                        null,
                        ImportItemDecision.Duplicate,
                        null),
                    new ImportBatchItemDto(
                        2,
                        "went",
                        "go%2:01:00::",
                        includeWordGroup ? "group-detail" : null,
                        ImportItemDecision.Pending,
                        includeWordGroup ? CreateWordGroup() : null)
                });
        }

        private static ImportWordGroupDto CreateWordGroup()
        {
            return new ImportWordGroupDto(
                "group-detail",
                LearningLanguage.English,
                "go%2:01:00::",
                ImportWordGroupPlacement.Inbox,
                DateTimeOffset.UtcNow,
                new[]
                {
                    new ImportWordGroupMemberDto(
                        "English",
                        "go%2:01:00::",
                        "go%v",
                        "go",
                        null,
                        "动词",
                        "不规则动词",
                        false,
                        new[] { "去", "离开" },
                        new[]
                        {
                            new ImportWordGroupFormDto(
                                "went",
                                null,
                                "Past",
                                null,
                                null,
                                "过去式",
                                "词库来源",
                                true,
                                false)
                        }),
                    new ImportWordGroupMemberDto(
                        "English",
                        "conversion%1:04:00::",
                        "conversion%n",
                        "conversion",
                        null,
                        "名词",
                        "名词",
                        false,
                        new[] { "转化" },
                        Array.Empty<ImportWordGroupFormDto>())
                });
        }
    }
}
