using System.Diagnostics;
using System.IO;
using ZCodePluginManager.Models;

namespace ZCodePluginManager.Services;

public static class PluginDirectoryService
{
    public static async Task<PluginDefinition?> AddFromGitHubAsync(
        string pluginsRoot,
        string repositoryInput,
        CancellationToken cancellationToken = default)
    {
        if (!GitService.TryNormalizeGitHubUrl(repositoryInput, out var repositoryUrl, out var branch, out var repositoryName))
        {
            throw new ArgumentException("请输入有效的 GitHub 仓库链接。");
        }

        var destination = Path.Combine(Path.GetFullPath(pluginsRoot), repositoryName);
        if (Directory.Exists(destination))
        {
            throw new InvalidOperationException($"目录已存在：{destination}");
        }

        var result = await GitService.CloneAsync(repositoryUrl, branch, destination, cancellationToken);
        if (!result.Success)
        {
            TryDelete(destination);
            throw new InvalidOperationException(result.Output);
        }

        return PluginCatalog.Discover(pluginsRoot)
            .FirstOrDefault(plugin => string.Equals(plugin.Directory, destination, StringComparison.OrdinalIgnoreCase));
    }

    public static async Task DeleteAsync(PluginDefinition plugin, string pluginsRoot, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetFullPath(plugin.Directory);
        var root = Path.GetFullPath(pluginsRoot);
        if (Path.GetDirectoryName(directory)?.TrimEnd(Path.DirectorySeparatorChar) != root ||
            string.Equals(Path.GetFileName(directory), "PluginManager", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("只允许删除插件根目录中的插件目录。");
        }

        if (!Directory.Exists(directory))
        {
            return;
        }

        await Task.Run(() => DeleteTree(directory), cancellationToken);
    }

    private static void DeleteTree(string directory)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories))
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }
        }

        Directory.Delete(directory, recursive: true);
    }

    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                DeleteTree(directory);
            }
        }
        catch
        {
            // Keep a partial clone visible so the user can inspect the failed download.
        }
    }
}
