using System.IO;
using ZCodePluginManager.Models;

namespace ZCodePluginManager.Services;

public static class PluginCatalog
{
    public static string ResolveDefaultRoot()
    {
        if (Environment.GetEnvironmentVariable("ZCODE_PLUGIN_ROOT") is { Length: > 0 } configured)
        {
            if (Directory.Exists(configured))
            {
                return configured;
            }
        }

        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            var parent = Directory.GetParent(current)?.FullName;
            if (!string.IsNullOrEmpty(parent) && Path.GetFileName(parent) == "插件")
            {
                return parent;
            }

            current = parent;
        }

        current = AppContext.BaseDirectory;
        var projectDir = Directory.GetParent(current);
        if (projectDir is not null)
        {
            var sourceRoot = Directory.GetParent(projectDir.FullName);
            if (sourceRoot is not null && Directory.GetParent(sourceRoot.FullName) is { } siblingRoot)
            {
                return siblingRoot.FullName;
            }
        }

        return Environment.CurrentDirectory;
    }

    public static IReadOnlyList<PluginDefinition> Discover(string root)
    {
        var plugins = new List<PluginDefinition>();
        if (!Directory.Exists(root))
        {
            return plugins;
        }

        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            var name = Path.GetFileName(directory);
            if (name.Equals("PluginManager", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var definition = name switch
            {
                "zcode-keysmith" when File.Exists(Path.Combine(directory, "zcode-keysmith.py")) => CreateKeySmith(directory, name),
                "zcode-token-usage-statusbar" when File.Exists(Path.Combine(directory, "install.py")) => CreateTokenStatusBar(directory, name),
                _ when File.Exists(Path.Combine(directory, "install.py")) => CreateGenericPython(directory, name),
                _ when File.Exists(Path.Combine(directory, "install.ps1")) => CreateGenericPowerShell(directory, name),
                _ when Directory.Exists(Path.Combine(directory, ".git")) => CreateUnsupported(directory, name),
                _ => null
            };

            if (definition is not null)
            {
                plugins.Add(definition);
            }
        }

        return plugins.OrderBy(p => p.Title, StringComparer.CurrentCulture).ToList();
    }

    private static PluginDefinition CreateGenericPython(string directory, string id)
    {
        var readme = PickReadme(directory);
        var markdown = MarkdownDigest.ReadText(readme);
        var title = MarkdownDigest.Title(markdown, id);
        return new PluginDefinition
        {
            Id = id,
            Title = title,
            Directory = directory,
            Installer = InstallerKind.PythonScript,
            RepositoryUrl = TryGetGitRemoteUrl(directory),
            ReadmePath = readme,
            ReadmeText = markdown,
            Summary = MarkdownDigest.Summary(markdown, "已从 GitHub 添加的 Python 插件。"),
            Features = MarkdownDigest.Features(markdown, ["这是一个通用 Python 安装器插件。"])
        };
    }

    private static PluginDefinition CreateGenericPowerShell(string directory, string id)
    {
        var readme = PickReadme(directory);
        var markdown = MarkdownDigest.ReadText(readme);
        return new PluginDefinition
        {
            Id = id,
            Title = MarkdownDigest.Title(markdown, id),
            Directory = directory,
            Installer = InstallerKind.PowerShellScript,
            RepositoryUrl = TryGetGitRemoteUrl(directory),
            ReadmePath = readme,
            ReadmeText = markdown,
            Summary = MarkdownDigest.Summary(markdown, "已从 GitHub 添加的 PowerShell 插件。"),
            Features = MarkdownDigest.Features(markdown, ["这是一个通用 PowerShell 安装器插件。"])
        };
    }

    private static PluginDefinition CreateUnsupported(string directory, string id)
    {
        var readme = PickReadme(directory);
        var markdown = MarkdownDigest.ReadText(readme);
        return new PluginDefinition
        {
            Id = id,
            Title = MarkdownDigest.Title(markdown, id),
            Directory = directory,
            Installer = InstallerKind.Unsupported,
            RepositoryUrl = TryGetGitRemoteUrl(directory),
            ReadmePath = readme,
            ReadmeText = markdown,
            Summary = MarkdownDigest.Summary(markdown, "已添加插件页面，但未识别到通用安装器。"),
            Features = MarkdownDigest.Features(markdown, ["未找到 install.py 或 install.ps1，暂不能自动安装。"])
        };
    }

    private static string? TryGetGitRemoteUrl(string directory)
    {
        try
        {
            var result = ProcessRunner.RunSync("git", ["config", "--get", "remote.origin.url"], directory);
            return result.Success && !string.IsNullOrWhiteSpace(result.Output)
                ? result.Output.Trim()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static PluginDefinition CreateKeySmith(string directory, string id)
    {
        return new PluginDefinition
        {
            Id = id,
            Title = "zcode-keysmith",
            Directory = directory,
            Installer = InstallerKind.KeySmith,
            RepositoryUrl = "https://github.com/Jia-Ethan/zcode-keysmith",
            ReadmePath = PickReadme(directory),
            ReadmeText = MarkdownDigest.ReadText(PickReadme(directory)),
            Summary = "给本机的 ZCode 装一份可撤销的固定指令：先预览，确认后写入，能验证，也能撤走。",
            Features =
            [
                "写入前先展示安装计划，确认后才会修改文件。",
                "安装后新开的 ZCode 对话会按固定指令工作，不读取账号和密钥。",
                "支持 doctor 检查安装状态，卸载后还原受影响的文件。",
                "ZCode 3.12+ 会备份并补丁 glm/zcode.cjs，卸载时还原；更早版本不改 App 原包。",
                "需要 Python 3.10+，支持 Windows 与 macOS。"
            ]
        };
    }

    private static PluginDefinition CreateTokenStatusBar(string directory, string id)
    {
        return new PluginDefinition
        {
            Id = id,
            Title = "ZCode Token 用量状态栏",
            Directory = directory,
            Installer = InstallerKind.TokenStatusBar,
            RepositoryUrl = "https://github.com/xhwxt/zcode-token-usage-statusbar",
            ReadmePath = PickReadme(directory),
            ReadmeText = MarkdownDigest.ReadText(PickReadme(directory)),
            Summary = "给 ZCode 桌面客户端加一个悬浮状态栏，实时展示 token 用量；数据只读本地 SQLite，不联网。",
            Features =
            [
                "显示生成速度、上下文容量、本轮消耗、会话累计、工具调用和今日合计。",
                "按窗口跟随当前会话，多窗口互不串显；支持子代理用量统计。",
                "上下文窗口可自动识别，也可在设置面板手动覆盖。",
                "支持 MCP 对话内查询、CLI 查询和 /usage 命令。",
                "需要 Windows 或 macOS，以及 Python 3.8+，零第三方依赖。"
            ]
        };
    }

    private static string PickReadme(string directory)
    {
        var candidates = new[] { "README.zh-CN.md", "README.md", "README.en.md" };
        return candidates
            .Select(candidate => Path.Combine(directory, candidate))
            .FirstOrDefault(File.Exists) ?? string.Empty;
    }
}
