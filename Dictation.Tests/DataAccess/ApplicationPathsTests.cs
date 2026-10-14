using Dictation.DataAccess.Runtime;
using Dictation.Tests.Infrastructure;

namespace Dictation.Tests.DataAccess
{
    /// <summary>
    /// 验证应用根路径校验、目录计算和运行目录创建边界。
    /// </summary>
    public sealed class ApplicationPathsTests
    {
        [Fact]
        public void EnsureRuntimeDirectories_CreatesFourSiblingDirectoriesWithExpectedPaths()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);

            paths.EnsureRuntimeDirectories();

            Assert.Equal(temporaryDirectory.Path, paths.Root);
            Assert.Equal(System.IO.Path.Combine(temporaryDirectory.Path, "Resources"), paths.Resources);
            Assert.Equal(System.IO.Path.Combine(temporaryDirectory.Path, "Resources", "Lexicons"), paths.Lexicons);
            Assert.Equal(System.IO.Path.Combine(paths.Lexicons, "JapaneseLexicon.db"), paths.JapaneseLexiconDatabase);
            Assert.Equal(System.IO.Path.Combine(paths.Lexicons, "EnglishLexicon.db"), paths.EnglishLexiconDatabase);
            Assert.Equal(System.IO.Path.Combine(temporaryDirectory.Path, "CONFIG"), paths.Config);
            Assert.Equal(System.IO.Path.Combine(temporaryDirectory.Path, "DATA"), paths.Data);
            Assert.Equal(System.IO.Path.Combine(temporaryDirectory.Path, "LOGS"), paths.Logs);
            Assert.Equal(System.IO.Path.Combine(temporaryDirectory.Path, "BACKUPS"), paths.Backups);
            Assert.True(Directory.Exists(paths.Config));
            Assert.True(Directory.Exists(paths.Data));
            Assert.True(Directory.Exists(paths.Logs));
            Assert.True(Directory.Exists(paths.Backups));
            Assert.False(Directory.Exists(paths.Lexicons));
            Assert.Equal(System.IO.Path.Combine(paths.Config, "settings.json"), paths.SettingsFile);
            Assert.Equal(System.IO.Path.Combine(paths.Data, "UserData.db"), paths.UserDataDatabase);
        }

        [Fact]
        public void EnsureRuntimeDirectories_DoesNotCreateCache()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);

            paths.EnsureRuntimeDirectories();

            Assert.False(Directory.Exists(paths.Cache));
        }

        [Fact]
        public void EnsureCacheDirectory_CreatesCacheOnlyWhenRequested()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);

            paths.EnsureCacheDirectory();

            Assert.True(Directory.Exists(paths.Cache));
        }

        [Fact]
        public void EnsureRuntimeDirectories_IsIdempotent()
        {
            using var temporaryDirectory = new TemporaryDirectory();
            var paths = new ApplicationPaths(temporaryDirectory.Path);

            paths.EnsureRuntimeDirectories();
            paths.EnsureRuntimeDirectories();

            Assert.True(Directory.Exists(paths.Config));
            Assert.True(Directory.Exists(paths.Data));
            Assert.True(Directory.Exists(paths.Logs));
            Assert.True(Directory.Exists(paths.Backups));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("relative-root")]
        public void Constructor_RejectsInvalidRoot(string? root)
        {
            Assert.ThrowsAny<ArgumentException>(() => new ApplicationPaths(root!));
        }

        [Fact]
        public void EnsureRuntimeDirectories_RejectsRootOccupiedByRegularFile()
        {
            var rootFile = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"Dictation.Tests.{Guid.NewGuid():N}");
            File.WriteAllText(rootFile, "occupied");

            try
            {
                var paths = new ApplicationPaths(rootFile);

                Assert.ThrowsAny<IOException>(() => paths.EnsureRuntimeDirectories());
            }
            finally
            {
                File.Delete(rootFile);
            }
        }
    }
}
