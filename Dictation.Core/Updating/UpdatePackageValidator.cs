using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Dictation.Core.Updating
{
    /// <summary>验证完整更新包、受管理路径与版本，不修改安装目录。</summary>
    internal static class UpdatePackageValidator
    {
        internal const string ManifestName = "package-manifest.json";
        private static readonly HashSet<string> protectedRoots = new(StringComparer.OrdinalIgnoreCase)
        { "CONFIG", "DATA", "LOGS", "BACKUPS", "CACHE", ".git", ".agents", ".codex", ".superpowers" };

        /// <summary>校验调用参数，禁止安装到驱动器根目录或相对路径。</summary>
        /// <param name="request">安装条件。</param>
        internal static void ValidateRequest(ApplyUpdateRequestDto request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.RootDirectory) || !Path.IsPathFullyQualified(request.RootDirectory)
                || Path.GetFullPath(request.RootDirectory) == Path.GetPathRoot(request.RootDirectory))
                throw new ArgumentException("更新根目录必须是已有应用的完全限定目录。", nameof(request));
            if (string.IsNullOrWhiteSpace(request.PackagePath) || !Path.IsPathFullyQualified(request.PackagePath))
                throw new ArgumentException("更新 ZIP 必须使用完全限定路径。", nameof(request));
            if (request.Sha256 is null || !Regex.IsMatch(request.Sha256, "\\A[0-9a-fA-F]{64}\\z"))
                throw new ArgumentException("更新包 SHA-256 格式错误。", nameof(request));
            if (!TryVersion(request.Version, out _)) throw new ArgumentException("更新版本必须是三段数字。", nameof(request));
        }

        /// <summary>验证现有安装清单，不猜测开发目录或未知文件的归属。</summary>
        /// <param name="root">已校验的根目录。</param>
        /// <returns>旧版本及受管理文件列表。</returns>
        internal static UpdatePackageManifest ReadInstalled(string root)
        {
            EnsureNoLinks(root, ManifestName);
            if (!File.Exists(Path.Combine(root, "Dictation.Presentation.exe")) || !File.Exists(Path.Combine(root, ManifestName)))
                throw new InvalidDataException("当前目录不是带文件清单的发行版本，请勿更新源码目录。");
            using var stream = File.OpenRead(Path.Combine(root, ManifestName));
            return ReadManifest(stream);
        }

        /// <summary>先核对 ZIP 哈希，再验证清单、全部条目及版本。</summary>
        /// <param name="request">正式版本与哈希条件。</param>
        /// <param name="installed">旧安装的有效清单。</param>
        /// <param name="stream">调用方保持打开的 ZIP 只读流。</param>
        /// <param name="cancellationToken">取消读取。</param>
        /// <returns>已验证的新文件清单。</returns>
        internal static async Task<UpdatePackageManifest> ReadPackageAsync(ApplyUpdateRequestDto request,
            UpdatePackageManifest installed, Stream stream, CancellationToken cancellationToken)
        {
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
            if (!StringComparer.OrdinalIgnoreCase.Equals(hash, request.Sha256)) throw new InvalidDataException("更新包 SHA-256 不匹配。");
            stream.Position = 0;
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var entries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ZipArchiveEntry? manifestEntry = null;
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000) throw new InvalidDataException("更新包不允许符号链接。");
                var path = entry.FullName.Replace('\\', '/');
                var isDirectory = path.EndsWith('/');
                var normalized = ValidateRelativePath(isDirectory ? path[..^1] : path);
                if (!entries.Add(normalized)) throw new InvalidDataException("更新包存在重复或大小写冲突路径。");
                if (isDirectory) continue;
                if (normalized.Equals(ManifestName, StringComparison.OrdinalIgnoreCase)) manifestEntry = entry;
                else files.Add(normalized);
            }
            if (manifestEntry is null) throw new InvalidDataException("更新包缺少文件清单。");
            using var manifestStream = manifestEntry.Open();
            var manifest = ReadManifest(manifestStream);
            if (manifest.Version != request.Version || !TryVersion(manifest.Version, out var next)
                || !TryVersion(installed.Version, out var previous) || next <= previous)
                throw new InvalidDataException("更新包版本不符，或者不是更新的正式版本。");
            if (!files.SetEquals(manifest.Files)) throw new InvalidDataException("ZIP 内容与文件清单不一致。");
            return manifest;
        }

        /// <summary>拒绝越界、受保护目录、设备名及 Windows 路径别名。</summary>
        /// <param name="relative">清单或 ZIP 提供的相对路径。</param>
        /// <returns>唯一的正斜杠相对路径。</returns>
        internal static string ValidateRelativePath(string relative)
        {
            if (string.IsNullOrWhiteSpace(relative)) throw new InvalidDataException("更新文件路径为空。");
            var path = relative.Replace('\\', '/');
            var parts = path.Split('/');
            if (Path.IsPathRooted(path) || protectedRoots.Contains(parts[0])) throw new InvalidDataException("更新包试图写入受保护或绝对路径。");
            foreach (var part in parts)
            {
                var stem = part.Split('.')[0];
                if (part.Length == 0 || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ')
                    || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || part.Contains(':')
                    || Regex.IsMatch(stem, "\\A(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])\\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    throw new InvalidDataException("更新包包含不安全的文件路径。");
            }
            return path;
        }

        /// <summary>防止已有目录链接将文件或暂存数据导向安装目录之外。</summary>
        /// <param name="root">根目录。</param>
        /// <param name="relative">已验证或内部固定的相对路径。</param>
        internal static void EnsureNoLinks(string root, string relative)
        {
            var basePath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            var current = Destination(basePath, relative);
            while (true)
            {
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("更新路径包含重解析点，不能安全替换。");
                if (current.Equals(basePath, StringComparison.OrdinalIgnoreCase)) break;
                current = Path.GetDirectoryName(current) ?? throw new InvalidDataException("更新路径越界。");
            }
        }

        /// <summary>组合已经验证的相对文件路径。</summary>
        /// <param name="root">任务或安装根目录。</param>
        /// <param name="relative">规范相对路径。</param>
        internal static string Destination(string root, string relative) => Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

        /// <summary>读取 JSON 并拒绝不完整、重复或缺主程序的清单。</summary>
        /// <param name="stream">只读清单流。</param>
        private static UpdatePackageManifest ReadManifest(Stream stream)
        {
            try
            {
                var manifest = JsonSerializer.Deserialize<UpdatePackageManifest>(stream);
                if (manifest is null || manifest.Files is null || !TryVersion(manifest.Version, out _)) throw new InvalidDataException("更新清单格式或版本错误。");
                var paths = manifest.Files.Select(ValidateRelativePath).ToArray();
                if (paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != paths.Length
                    || paths.Contains(ManifestName, StringComparer.OrdinalIgnoreCase)
                    || !paths.Contains("Dictation.Presentation.exe", StringComparer.OrdinalIgnoreCase)
                    || !paths.Contains("Updater/Dictation.Updater.exe", StringComparer.OrdinalIgnoreCase))
                    throw new InvalidDataException("更新清单缺少必要程序或存在重复路径。");
                return manifest with { Files = paths };
            }
            catch (JsonException exception) { throw new InvalidDataException("更新清单不是有效 JSON。", exception); }
        }

        /// <summary>只接受发行流程的三段非负数字版本。</summary>
        /// <param name="text">版本文本。</param>
        /// <param name="version">解析后的版本。</param>
        private static bool TryVersion(string? text, out Version? version)
        {
            version = null;
            return text is not null && Regex.IsMatch(text, "\\A[0-9]+\\.[0-9]+\\.[0-9]+\\z") && Version.TryParse(text, out version);
        }
    }
}
