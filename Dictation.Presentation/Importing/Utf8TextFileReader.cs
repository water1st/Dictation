using System.Text;

namespace Dictation.Presentation.Importing
{
    /// <summary>把 UTF-8 TXT 文件转换为来源词表文本，不查询词库或写数据库。</summary>
    public sealed class Utf8TextFileReader
    {
        /// <summary>严格读取 UTF-8 文本，拒绝其他编码和非 TXT 文件。</summary>
        /// <param name="path">用户选择的文件路径。</param>
        /// <param name="cancellationToken">取消读取的令牌。</param>
        /// <returns>不包含 UTF-8 BOM 的原始词表文本。</returns>
        public async Task<string> ReadAsync(string path, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.Equals(Path.GetExtension(path), ".txt", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("请选择 UTF-8 编码的 TXT 文件。", nameof(path));
            }
            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            // 不用自动检测编码的 StreamReader，避免 UTF-16 或损坏文本被悄悄接受。
            var text = new UTF8Encoding(false, true).GetString(bytes);
            return text.Length > 0 && text[0] == '\uFEFF' ? text[1..] : text;
        }
    }
}
