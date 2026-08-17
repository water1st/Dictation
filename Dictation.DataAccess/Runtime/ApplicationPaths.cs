namespace Dictation.DataAccess.Runtime
{
    public sealed class ApplicationPaths
    {
        public ApplicationPaths(string rootDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
            if (!Path.IsPathFullyQualified(rootDirectory))
            {
                throw new ArgumentException("应用根目录必须是完全限定路径。", nameof(rootDirectory));
            }

            Root = Path.GetFullPath(rootDirectory);
            Config = Path.Combine(Root, "CONFIG");
            Data = Path.Combine(Root, "DATA");
            Logs = Path.Combine(Root, "LOGS");
            Backups = Path.Combine(Root, "BACKUPS");
            Cache = Path.Combine(Root, "CACHE");
            SettingsFile = Path.Combine(Config, "settings.json");
            UserDataDatabase = Path.Combine(Data, "UserData.db");
        }

        public string Root { get; }

        public string Config { get; }

        public string Data { get; }

        public string Logs { get; }

        public string Backups { get; }

        public string Cache { get; }

        public string SettingsFile { get; }

        public string UserDataDatabase { get; }

        public void EnsureRuntimeDirectories()
        {
            Directory.CreateDirectory(Config);
            Directory.CreateDirectory(Data);
            Directory.CreateDirectory(Logs);
            Directory.CreateDirectory(Backups);
        }

        public void EnsureCacheDirectory()
        {
            Directory.CreateDirectory(Cache);
        }
    }
}
