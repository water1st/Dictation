using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Dictation.LexiconBuilder.WordNet
{
    /// <summary>
    /// 发现并稳定排序 WordNet YAML 输入，并计算与路径和内容绑定的来源指纹。
    /// </summary>
    internal static class WordNetSourceFiles
    {
        private static readonly Regex EntryFilePattern = new(
            @"^entries-.+\.ya?ml$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly Regex SynsetFilePattern = new(
            @"^(adj|adv|noun|verb)\..+\.ya?ml$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        /// <summary>
        /// 枚举受支持的 entries 与 synset YAML，并排除 .git 目录。
        /// </summary>
        /// <param name="directoryPath">Open English WordNet YAML 根目录。</param>
        /// <returns>按规范化相对路径进行序数排序的来源文件。</returns>
        internal static IReadOnlyList<WordNetSourceFile> Enumerate(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentException("WordNet YAML 目录不能为空。", nameof(directoryPath));
            }

            var fullDirectoryPath = Path.GetFullPath(directoryPath);
            if (!Directory.Exists(fullDirectoryPath))
            {
                throw new DirectoryNotFoundException($"WordNet YAML 目录不存在：{fullDirectoryPath}。");
            }

            return Directory
                .EnumerateFiles(fullDirectoryPath, "*", SearchOption.AllDirectories)
                .Where(path => !IsInGitDirectory(fullDirectoryPath, path))
                .Select(path => CreateSourceFile(fullDirectoryPath, path))
                .Where(file => file.Kind is WordNetSourceFileKind.Entries or WordNetSourceFileKind.Synsets)
                .OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// 按规范化相对路径、文件长度和文件内容计算稳定的 SHA-256 指纹。
        /// </summary>
        /// <param name="sourceFiles">参与构建的来源文件；计算时会按相对路径重新稳定排序。</param>
        /// <returns>小写十六进制 SHA-256；任一相对路径、长度或内容变化都会改变结果。</returns>
        internal static string ComputeFingerprint(IReadOnlyList<WordNetSourceFile> sourceFiles)
        {
            ArgumentNullException.ThrowIfNull(sourceFiles);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var sourceFile in sourceFiles.OrderBy(file => file.RelativePath, StringComparer.Ordinal))
            {
                var relativePathBytes = Encoding.UTF8.GetBytes(sourceFile.RelativePath);
                AppendLength(hash, relativePathBytes.Length);
                hash.AppendData(relativePathBytes);

                var fileInfo = new FileInfo(sourceFile.FullPath);
                AppendLength(hash, fileInfo.Length);
                using var stream = File.OpenRead(sourceFile.FullPath);
                var buffer = new byte[64 * 1024];
                int bytesRead;
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    hash.AppendData(buffer, 0, bytesRead);
                }
            }

            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }

        private static WordNetSourceFile CreateSourceFile(string rootPath, string fullPath)
        {
            var fileName = Path.GetFileName(fullPath);
            var kind = EntryFilePattern.IsMatch(fileName)
                ? WordNetSourceFileKind.Entries
                : SynsetFilePattern.IsMatch(fileName)
                    ? WordNetSourceFileKind.Synsets
                    : (WordNetSourceFileKind)(-1);
            var relativePath = Path.GetRelativePath(rootPath, fullPath)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');
            return new WordNetSourceFile(fullPath, relativePath, kind);
        }

        private static bool IsInGitDirectory(string rootPath, string fullPath)
        {
            var relativePath = Path.GetRelativePath(rootPath, fullPath);
            return relativePath
                .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase));
        }

        private static void AppendLength(IncrementalHash hash, long value)
        {
            Span<byte> buffer = stackalloc byte[sizeof(long)];
            BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
            hash.AppendData(buffer);
        }
    }
}
