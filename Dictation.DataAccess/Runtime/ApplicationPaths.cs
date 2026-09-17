namespace Dictation.DataAccess.Runtime
{
    /// <summary>
    /// 集中计算应用资源与用户运行数据路径，不在构造时创建目录。
    /// </summary>
    public sealed class ApplicationPaths
    {
        /// <summary>
        /// 集中计算应用资源与用户运行数据路径，不在构造时创建目录。
        /// </summary>
        /// <param name="rootDirectory">应用完全限定根目录。</param>
        public ApplicationPaths(string rootDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
            if (!Path.IsPathFullyQualified(rootDirectory))
            {
                throw new ArgumentException("应用根目录必须是完全限定路径。", nameof(rootDirectory));
            }

            Root = Path.GetFullPath(rootDirectory);
            Resources = Path.Combine(Root, "Resources");
            Lexicons = Path.Combine(Resources, "Lexicons");
            JapaneseLexiconDatabase = Path.Combine(Lexicons, "JapaneseLexicon.db");
            EnglishLexiconDatabase = Path.Combine(Lexicons, "EnglishLexicon.db");
            Config = Path.Combine(Root, "CONFIG");
            Data = Path.Combine(Root, "DATA");
            Logs = Path.Combine(Root, "LOGS");
            Backups = Path.Combine(Root, "BACKUPS");
            Cache = Path.Combine(Root, "CACHE");
            SettingsFile = Path.Combine(Config, "settings.json");
            UserDataDatabase = Path.Combine(Data, "UserData.db");
        }

        public string Root { get; }

        public string Resources { get; }

        public string Lexicons { get; }

        public string JapaneseLexiconDatabase { get; }

        public string EnglishLexiconDatabase { get; }

        public string Config { get; }

        public string Data { get; }

        public string Logs { get; }

        public string Backups { get; }

        public string Cache { get; }

        public string SettingsFile { get; }

        public string UserDataDatabase { get; }

        /// <summary>
        /// 创建配置、数据、日志和备份目录，不创建可选缓存目录。
        /// </summary>
        public void EnsureRuntimeDirectories()
        {
            Directory.CreateDirectory(Config);
            Directory.CreateDirectory(Data);
            Directory.CreateDirectory(Logs);
            Directory.CreateDirectory(Backups);
        }

        /// <summary>
        /// 在需要缓存时创建缓存目录。
        /// </summary>
        public void EnsureCacheDirectory()
        {
            Directory.CreateDirectory(Cache);
        }
    }
}
