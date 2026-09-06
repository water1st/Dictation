using Dictation.Core.Configuration;

namespace Dictation.Tests.Core
{
    public sealed class ApplicationSettingsMapperTests
    {
        [Theory]
        [InlineData("System", ThemeMode.System)]
        [InlineData("Light", ThemeMode.Light)]
        [InlineData("Dark", ThemeMode.Dark)]
        public void ToDto_MapsEachValidTheme(string persistedTheme, ThemeMode expectedTheme)
        {
            var result = ApplicationSettingsMapper.ToDto(new ApplicationSettingsDocument
            {
                ThemeMode = persistedTheme,
                LastLanguage = "Japanese"
            });

            Assert.Equal(expectedTheme, result.ThemeMode);
        }

        [Theory]
        [InlineData("Japanese", LearningLanguage.Japanese)]
        [InlineData("English", LearningLanguage.English)]
        public void ToDto_MapsEachValidLanguage(string persistedLanguage, LearningLanguage expectedLanguage)
        {
            var result = ApplicationSettingsMapper.ToDto(new ApplicationSettingsDocument
            {
                ThemeMode = "System",
                LastLanguage = persistedLanguage
            });

            Assert.Equal(expectedLanguage, result.LastLanguage);
        }

        [Theory]
        [InlineData("Unknown", "Japanese")]
        [InlineData(null, "Japanese")]
        [InlineData(" ", "Japanese")]
        public void ToDto_RejectsUnknownOrBlankTheme(string? persistedTheme, string persistedLanguage)
        {
            Assert.Throws<SettingsConfigurationException>(() => ApplicationSettingsMapper.ToDto(new ApplicationSettingsDocument
            {
                ThemeMode = persistedTheme,
                LastLanguage = persistedLanguage
            }));
        }

        [Theory]
        [InlineData("System", "Unknown")]
        [InlineData("System", null)]
        [InlineData("System", " ")]
        public void ToDto_RejectsUnknownOrBlankLanguage(string persistedTheme, string? persistedLanguage)
        {
            Assert.Throws<SettingsConfigurationException>(() => ApplicationSettingsMapper.ToDto(new ApplicationSettingsDocument
            {
                ThemeMode = persistedTheme,
                LastLanguage = persistedLanguage
            }));
        }

        [Fact]
        public void ToDocument_MapsDtoEnumsToPersistedStrings()
        {
            var result = ApplicationSettingsMapper.ToDocument(new ApplicationSettingsDto(
                ThemeMode.Dark,
                LearningLanguage.English));

            Assert.Equal("Dark", result.ThemeMode);
            Assert.Equal("English", result.LastLanguage);
        }

        [Fact]
        public void ToDto_RejectsNullDocument()
        {
            Assert.Throws<ArgumentNullException>(() =>
                ApplicationSettingsMapper.ToDto(null!));
        }

        [Fact]
        public void ToDocument_RejectsNullDto()
        {
            Assert.Throws<ArgumentNullException>(() =>
                ApplicationSettingsMapper.ToDocument(null!));
        }
    }
}
