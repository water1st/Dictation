namespace Dictation.Tests.Presentation
{
    /// <summary>串行执行 DPI 渲染，避免多个 STA 同时退出时的 WPF 字体回调冲突。</summary>
    [CollectionDefinition("DpiRendering", DisableParallelization = true)]
    public sealed class DpiRenderingCollection
    {
    }
}
