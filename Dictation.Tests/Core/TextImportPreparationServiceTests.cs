using Dictation.Core.Configuration;
using Dictation.Core.Importing;
using Dictation.Core.Lexicon;

namespace Dictation.Tests.Core
{
    /// <summary>
    /// 验证文本词表只产生精确、可由用户确认的来源义项候选。
    /// </summary>
    public sealed class TextImportPreparationServiceTests
    {
        [Fact]
        public async Task Prepare_JapaneseWrittenAndReadingFindTheSameSense()
        {
            var result = Result(
                LearningLanguage.Japanese,
                "jmdict:1358280:1",
                "食べる",
                "吃",
                "食べる",
                "たべる");
            var service = Service(_ => new[] { result });

            var prepared = await service.PrepareAsync(
                LearningLanguage.Japanese,
                "食べる\nたべる",
                TestContext.Current.CancellationToken);

            Assert.Equal(LearningLanguage.Japanese, prepared.Language);
            Assert.Equal(2, prepared.Lines.Count);
            Assert.Equal(new[] { 1, 2 }, prepared.Lines.Select(line => line.LineNumber));
            Assert.All(prepared.Lines, line =>
            {
                Assert.Equal("jmdict:1358280:1", Assert.Single(line.Candidates).SourceSenseId);
                Assert.Equal("jmdict:1358280:1", line.DefaultSourceSenseId);
            });
        }

        [Fact]
        public async Task Prepare_EnglishInflectedFormKeepsItsBaseWordGroupCandidate()
        {
            var service = Service(_ => new[]
            {
                Result(LearningLanguage.English, "oewn:go:1", "go", "去", "go", "goes", "went", "gone", "going")
            });

            var prepared = await service.PrepareAsync(
                LearningLanguage.English,
                "WENT",
                TestContext.Current.CancellationToken);

            var line = Assert.Single(prepared.Lines);
            Assert.Equal("WENT", line.SourceText);
            Assert.Equal("oewn:go:1", Assert.Single(line.Candidates).SourceSenseId);
            Assert.Equal("oewn:go:1", line.DefaultSourceSenseId);
        }

        [Fact]
        public async Task Prepare_MultipleExactSensesRequireExplicitChoice()
        {
            var service = Service(_ => new[]
            {
                Result(LearningLanguage.English, "oewn:run:1", "run", "跑", "run"),
                Result(LearningLanguage.English, "oewn:run:2", "run", "经营", "run")
            });

            var prepared = await service.PrepareAsync(
                LearningLanguage.English,
                "run",
                TestContext.Current.CancellationToken);

            var line = Assert.Single(prepared.Lines);
            Assert.Equal(2, line.Candidates.Count);
            Assert.Null(line.DefaultSourceSenseId);
            Assert.Equal(new[] { "oewn:run:1", "oewn:run:2" },
                line.Candidates.Select(candidate => candidate.SourceSenseId));
        }

        [Fact]
        public async Task Prepare_PrefixMatchIsNotAnImportCandidate()
        {
            var service = Service(_ => new[]
            {
                Result(LearningLanguage.English, "oewn:catalog:1", "catalog", "目录", "catalog")
            });

            var prepared = await service.PrepareAsync(
                LearningLanguage.English,
                "cat",
                TestContext.Current.CancellationToken);

            var line = Assert.Single(prepared.Lines);
            Assert.Empty(line.Candidates);
            Assert.Null(line.DefaultSourceSenseId);
        }

        [Fact]
        public async Task Prepare_DuplicateLinesCommentsAndBlankLinesKeepFirstSourceLine()
        {
            var service = Service(_ => new[]
            {
                Result(LearningLanguage.English, "oewn:go:1", "go", "去", "go")
            });

            var prepared = await service.PrepareAsync(
                LearningLanguage.English,
                "  Go  \r\n# 本行是注释\r\n\r\ngo\r\nGO ",
                TestContext.Current.CancellationToken);

            var line = Assert.Single(prepared.Lines);
            Assert.Equal(1, line.LineNumber);
            Assert.Equal("Go", line.SourceText);
            Assert.Equal("oewn:go:1", Assert.Single(line.Candidates).SourceSenseId);
        }

        [Fact]
        public async Task Prepare_JapaneseUnicodeNormalizationDeduplicatesTheSameKana()
        {
            var service = Service(_ => new[]
            {
                Result(LearningLanguage.Japanese, "jmdict:kana:1", "が", "字母", "が")
            });

            var prepared = await service.PrepareAsync(
                LearningLanguage.Japanese,
                "が\nか\u3099",
                TestContext.Current.CancellationToken);

            Assert.Single(prepared.Lines);
            Assert.Equal("が", prepared.Lines[0].SourceText);
        }

        [Fact]
        public async Task Prepare_DuplicateSenseResultsAppearOnlyOnce()
        {
            var result = Result(LearningLanguage.English, "oewn:go:1", "go", "去", "go");
            var service = Service(_ => new[] { result, result });

            var prepared = await service.PrepareAsync(
                LearningLanguage.English,
                "go",
                TestContext.Current.CancellationToken);

            Assert.Single(Assert.Single(prepared.Lines).Candidates);
        }

        [Fact]
        public async Task Prepare_EmptyTextReturnsNoLines()
        {
            var service = Service(_ => throw new InvalidOperationException("空文本不应查询词库。"));

            var prepared = await service.PrepareAsync(
                LearningLanguage.Japanese,
                " \r\n # 注释 \n\t",
                TestContext.Current.CancellationToken);

            Assert.Empty(prepared.Lines);
        }

        [Fact]
        public async Task Prepare_InvalidLanguageThrowsBeforeSearching()
        {
            var service = Service(_ => throw new InvalidOperationException("非法语言不应查询词库。"));

            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.PrepareAsync(
                (LearningLanguage)99,
                "go",
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Prepare_NullTextThrows()
        {
            var service = Service(_ => Array.Empty<LexiconLookupResultDto>());

            await Assert.ThrowsAsync<ArgumentNullException>(() => service.PrepareAsync(
                LearningLanguage.English,
                null!,
                TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Prepare_CanceledCallDoesNotReturnPartialCandidates()
        {
            var service = Service(_ => throw new InvalidOperationException("取消后不应查询词库。"));
            using var source = new CancellationTokenSource();
            source.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.PrepareAsync(
                LearningLanguage.English,
                "go",
                source.Token));
        }

        [Fact]
        public async Task Prepare_MissingLexiconErrorPropagatesWithoutPartialSuccess()
        {
            var service = Service(query => query == "go"
                ? new[] { Result(LearningLanguage.English, "oewn:go:1", "go", "去", "go") }
                : throw new InvalidOperationException("英语词库文件不存在。"));

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.PrepareAsync(
                LearningLanguage.English,
                "go\ncat",
                TestContext.Current.CancellationToken));

            Assert.Equal("英语词库文件不存在。", error.Message);
        }

        private static TextImportPreparationService Service(
            Func<string, IReadOnlyList<LexiconLookupResultDto>> search)
        {
            return new TextImportPreparationService(new StubLexiconSearch(search));
        }

        private static LexiconLookupResultDto Result(
            LearningLanguage language,
            string sourceSenseId,
            string headword,
            string meaning,
            params string[] surfaces)
        {
            var formCode = language == LearningLanguage.Japanese ? "Dictionary" : "Base";
            return new LexiconLookupResultDto(
                language,
                sourceSenseId,
                sourceSenseId,
                headword,
                language == LearningLanguage.Japanese ? headword : null,
                "动词",
                "词库测试属性",
                false,
                new[] { meaning },
                surfaces.Select(surface => new LexiconLookupFormDto(
                    surface,
                    language == LearningLanguage.Japanese ? surface : null,
                    formCode,
                    null,
                    null,
                    "原形",
                    "测试词形",
                    true,
                    true)).ToArray());
        }

        private sealed class StubLexiconSearch : ILexiconSearch
        {
            private readonly Func<string, IReadOnlyList<LexiconLookupResultDto>> search;

            public StubLexiconSearch(Func<string, IReadOnlyList<LexiconLookupResultDto>> search)
            {
                this.search = search;
            }

            public Task<IReadOnlyList<LexiconLookupResultDto>> SearchAsync(
                LexiconSearchRequestDto request,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(search(request.Query));
            }
        }
    }
}
