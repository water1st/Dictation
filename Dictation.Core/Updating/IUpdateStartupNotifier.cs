namespace Dictation.Core.Updating
{
    /// <summary>在迁移、Host和WPF窗口加载成功后确认新版就绪。</summary>
    public interface IUpdateStartupNotifier
    {
        /// <summary>只向更新器指定且有有效收据的CACHE事务写入就绪标记；普通启动不写入。</summary>
        void NotifyReady();
    }
}
