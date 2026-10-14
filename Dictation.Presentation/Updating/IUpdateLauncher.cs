using Dictation.Core.Updating;

namespace Dictation.Presentation.Updating
{
    /// <summary>适配隐藏更新进程的启动，业务校验由Core负责。</summary>
    public interface IUpdateLauncher
    {
        /// <summary>启动已准备的独占runner，失败抛异常且不关闭原窗口。</summary>
        /// <param name="update">已校验完整包及runner的启动条件。</param>
        void Launch(PreparedUpdateDto update);
    }
}
