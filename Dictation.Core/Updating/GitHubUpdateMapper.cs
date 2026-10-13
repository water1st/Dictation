namespace Dictation.Core.Updating
{
    /// <summary>纯转换已选正式发行的成对资产并验证固定仓库地址，不执行 HTTP 或修改状态。</summary>
    internal static class GitHubUpdateMapper
    {
        internal const string PackageName = "Dictation-win-x64.zip";

        /// <summary>重新验证公开DTO的下载地址，不能信任调用方自行构造的资产。</summary>
        /// <param name="update">下载条件。</param>
        internal static void ValidateDownload(AvailableUpdateDto update)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(update.Version ?? string.Empty, "\\A[0-9]+\\.[0-9]+\\.[0-9]+\\z"))
                throw new InvalidDataException("更新版本格式错误。");
            foreach (var tag in new[] { "v" + update.Version, update.Version })
            {
                var prefix = $"https://github.com/water1st/Dictation/releases/download/{tag}/";
                if (update.PackageUri?.AbsoluteUri == prefix + PackageName
                    && update.ChecksumUri?.AbsoluteUri == prefix + PackageName + ".sha256") return;
            }
            throw new InvalidDataException("更新资产必须来自同一正式发行的固定仓库。");
        }

        /// <summary>将来源文档转换为业务 DTO；缺失、重复或外部下载地址均拒绝。</summary>
        /// <param name="release">已经通过正式／新版本筛选的发行。</param>
        /// <param name="version">规范化后的三段版本。</param>
        /// <returns>同一固定发行标签下的 ZIP 与 SHA-256 地址。</returns>
        internal static AvailableUpdateDto ToDto(GitHubReleaseDocument release, string version)
        {
            if (release.Assets is null) throw new InvalidDataException("正式发行缺少更新资产。");
            return new(version, ReadAsset(release, PackageName), ReadAsset(release, PackageName + ".sha256"));
        }

        /// <summary>读取唯一指定资产，并排除 HTTP、外部仓库、认证信息与额外查询片段。</summary>
        /// <param name="release">来源发行。</param>
        /// <param name="name">固定成品文件名。</param>
        private static Uri ReadAsset(GitHubReleaseDocument release, string name)
        {
            var matches = release.Assets!.Where(asset => asset.Name == name).ToArray();
            if (matches.Length != 1 || !Uri.TryCreate(matches[0].BrowserDownloadUrl, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps || !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
                || uri.Port != 443 || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0
                || uri.AbsolutePath != $"/water1st/Dictation/releases/download/{Uri.EscapeDataString(release.TagName!)}/{name}")
                throw new InvalidDataException("正式发行资产缺失、重复或下载地址不可信。");
            return uri;
        }
    }
}
