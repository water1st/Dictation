using Dictation.Core.Importing;
using Dictation.Core.Lexicon;
using Dictation.DataAccess.Importing;

namespace Dictation.Tests.Core
{
    /// <summary>
    /// 验证导入成员转换保留词形、来源和中文释义元数据。
    /// </summary>
    public sealed class ImportWordGroupMapperTests
    {
        [Fact]
        public void ToDto_NonChineseSnapshotHidesMeaningsAndPreservesMemberAndFormMetadata()
        {
            var source = new ImportWordGroupMemberPo(
                "Japanese",
                "sense-1",
                "lexeme-1",
                "語",
                "ご",
                "名词",
                "常用汉字",
                false,
                "en",
                "Tomoshi",
                "Exact",
                new[] { "word" },
                new[]
                {
                    new ImportWordGroupFormPo(
                        "語",
                        "ご",
                        "Dictionary",
                        "Kanji",
                        "Noun",
                        "词典形",
                        "汉字 · 名词",
                        true,
                        true)
                },
                "Common");

            var result = ImportWordGroupMapper.ToDto(source);

            Assert.Equal("Japanese", result.Language);
            Assert.Equal("sense-1", result.SourceSenseId);
            Assert.Equal("lexeme-1", result.SourceLexemeId);
            Assert.Equal("語", result.Headword);
            Assert.Equal("ご", result.Reading);
            Assert.Equal("名词", result.PartOfSpeechLabel);
            Assert.Equal("常用汉字", result.AttributeLabel);
            Assert.False(result.IsPhrase);
            Assert.Empty(result.Meanings);
            Assert.False(result.HasChineseMeaning);
            Assert.Equal(LexiconMeaningSource.Tomoshi, result.MeaningSource);
            Assert.Equal(LexiconMeaningPrecision.Exact, result.MeaningPrecision);
            Assert.Equal("Common", result.KanjiUsageCode);
            var form = Assert.Single(result.Forms);
            Assert.Equal("語", form.Surface);
            Assert.Equal("ご", form.Reading);
            Assert.Equal("Dictionary", form.FormCode);
            Assert.Equal("Kanji", form.ScriptCode);
            Assert.Equal("Noun", form.RoleCode);
            Assert.Equal("词典形", form.FormLabel);
            Assert.Equal("汉字 · 名词", form.DetailLabel);
            Assert.True(form.IsCommon);
            Assert.True(form.IsPrimary);
        }
    }
}
