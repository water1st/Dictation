using System.Text.Json;
using Dictation.Core.Updating;
using Dictation.DataAccess;
using Dictation.Tests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dictation.Tests.Core
{
    /// <summary>验证启动确认只写对应更新事务，不修改运行数据、不需要另一个配置容器。</summary>
    public sealed class UpdateStartupNotifierTests
    {
        [Fact]
        public void Notify_NormalStartupWithoutUpdaterSignalDoesNotWriteAnything()
        {
            using var fixture = new NotifierFixture();
            using var services = fixture.Build(null);
            services.GetRequiredService<IUpdateStartupNotifier>().NotifyReady();
            Assert.False(File.Exists(fixture.Ready));
            Assert.False(File.Exists(Path.Combine(fixture.Root, "DATA", "UserData.db")));
        }

        [Fact]
        public void Notify_ValidInstalledReceiptWritesOnlyItsReadyMarker()
        {
            using var fixture = new NotifierFixture();
            using var services = fixture.Build(fixture.Ready);
            services.GetRequiredService<IUpdateStartupNotifier>().NotifyReady();
            Assert.True(File.Exists(fixture.Ready));
            Assert.Equal("user-data", File.ReadAllText(Path.Combine(fixture.Root, "DATA", "keep.bin")));
            Assert.False(File.Exists(Path.Combine(fixture.Root, "DATA", "UserData.db")));
        }

        [Theory]
        [InlineData("DATA/startup.ready")]
        [InlineData("CACHE/Updates/not-a-job/startup.ready")]
        public void Notify_RejectsForeignOrUserDataTargetWithoutWriting(string relative)
        {
            using var fixture = new NotifierFixture();
            var target = Path.Combine(fixture.Root, relative);
            using var services = fixture.Build(target);
            Assert.Throws<InvalidDataException>(() => services.GetRequiredService<IUpdateStartupNotifier>().NotifyReady());
            Assert.False(File.Exists(target));
            Assert.Equal("user-data", File.ReadAllText(Path.Combine(fixture.Root, "DATA", "keep.bin")));
        }

        /// <summary>创建字面量发行与事务记录，用真实微软配置及原DI组合根执行通知。</summary>
        private sealed class NotifierFixture : IDisposable
        {
            private readonly TemporaryDirectory directory = new();
            internal string Root => directory.Path;
            internal string Ready { get; }
            internal NotifierFixture()
            {
                var job = Path.Combine(Root, "CACHE", "Updates", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(job);
                Ready = Path.Combine(job, "startup.ready");
                var files = new[] { "Dictation.Presentation.exe", "Updater/Dictation.Updater.exe" };
                File.WriteAllText(Path.Combine(Root, "Dictation.Presentation.exe"), "new-main");
                File.WriteAllText(Path.Combine(Root, "package-manifest.json"), JsonSerializer.Serialize(new { version = "1.0.2", files }));
                File.WriteAllText(Path.Combine(job, "recovery.json"), JsonSerializer.Serialize(new
                {
                    RootDirectory = Root, PreviousVersion = "1.0.1", Version = "1.0.2",
                    PreviousFiles = files.Append("package-manifest.json").ToArray(),
                    NewFiles = files.Append("package-manifest.json").ToArray()
                }));
                Directory.CreateDirectory(Path.Combine(Root, "DATA"));
                File.WriteAllText(Path.Combine(Root, "DATA", "keep.bin"), "user-data");
            }
            internal ServiceProvider Build(string? ready)
            {
                var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                    { ["DICTATION_UPDATE_READY_FILE"] = ready }).Build();
                var collection = new ServiceCollection().AddDictationDataAccess(Root).AddDictationCore();
                collection.AddSingleton<IConfiguration>(configuration);
                return collection.BuildServiceProvider();
            }
            public void Dispose() => directory.Dispose();
        }
    }
}
