using System.Diagnostics;
using System.IO;
using ZCodePluginManager.Models;

namespace ZCodePluginManager.Services;

public static class PluginOperations
{
    public static PluginStatus ReadStatus(PluginDefinition plugin)
    {
        return plugin.Installer switch
        {
            InstallerKind.KeySmith => ReadKeySmithStatus(),
            InstallerKind.TokenStatusBar => ReadTokenStatusBarStatus(),
            InstallerKind.PythonScript or InstallerKind.PowerShellScript => ReadGenericStatus(),
            InstallerKind.Unsupported => ReadUnsupportedStatus(),
            _ => new PluginStatus()
        };
    }

    public static string ResolvePython(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.PythonPath) && File.Exists(settings.PythonPath))
        {
            return settings.PythonPath;
        }

        var py = FindOnPath("py.exe") ?? FindOnPath("py.cmd") ?? FindOnPath("py.bat");
        if (py is not null)
        {
            return py;
        }

        var python = FindOnPath("python.exe");
        if (python is not null)
        {
            return python;
        }

        return "py";
    }

    public static IReadOnlyList<string> BuildInstall(PluginDefinition plugin, AppSettings settings)
    {
        return plugin.Installer switch
        {
            InstallerKind.KeySmith => BuildKeySmith(plugin, "install", settings),
            InstallerKind.TokenStatusBar => BuildTokenStatusBar(plugin, [], settings),
            InstallerKind.PythonScript => ["install.py"],
            InstallerKind.PowerShellScript => ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "install.ps1"],
            _ => throw new NotSupportedException("未知插件安装器。")
        };
    }

    public static IReadOnlyList<string> BuildUninstall(PluginDefinition plugin, AppSettings settings)
    {
        return plugin.Installer switch
        {
            InstallerKind.KeySmith => BuildKeySmith(plugin, "uninstall", settings),
            InstallerKind.TokenStatusBar => BuildTokenStatusBar(plugin, ["--remove"], settings),
            InstallerKind.PythonScript => ["install.py", "--remove"],
            InstallerKind.PowerShellScript => ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "install.ps1"],
            _ => throw new NotSupportedException("未知插件卸载器。")
        };
    }

    public static string BuildFileName(PluginDefinition plugin, AppSettings settings)
    {
        return plugin.Installer switch
        {
            InstallerKind.KeySmith or InstallerKind.TokenStatusBar or InstallerKind.PythonScript => ResolvePython(settings),
            InstallerKind.PowerShellScript => "powershell.exe",
            _ => throw new NotSupportedException("当前插件没有可执行的安装器。")
        };
    }

    private static PluginStatus ReadKeySmithStatus()
    {
        var managedFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".zcode-keysmith",
            "system-role.md");

        if (File.Exists(managedFile))
        {
            return new PluginStatus
            {
                Installed = true,
                StateText = "已安装",
                StateColor = "#15803D"
            };
        }

        return new PluginStatus
        {
            StateText = "未安装",
            StateColor = "#B45309"
        };
    }

    public static PluginStatus ReadGenericStatus()
    {
        return new PluginStatus
        {
            StateText = "可安装",
            StateColor = "#B45309"
        };
    }

    public static PluginStatus ReadUnsupportedStatus()
    {
        return new PluginStatus
        {
            Supported = false,
            StateText = "未识别安装器",
            StateColor = "#92400E"
        };
    }

    private static PluginStatus ReadTokenStatusBarStatus()
    {
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".zcode",
            "zcode-token-usage-statusbar");

        if (File.Exists(Path.Combine(dataDir, "config.json")) ||
            File.Exists(Path.Combine(dataDir, "inject-main.cjs")))
        {
            return new PluginStatus
            {
                Installed = true,
                StateText = "已安装",
                StateColor = "#15803D"
            };
        }

        return new PluginStatus
        {
            StateText = "未安装",
            StateColor = "#B45309"
        };
    }

    private static IReadOnlyList<string> BuildKeySmith(PluginDefinition plugin, string command, AppSettings settings)
    {
        var args = new List<string>
        {
            Path.Combine(plugin.Directory, "zcode-keysmith.py"),
            command
        };

        if (!string.IsNullOrWhiteSpace(settings.ZCodeRoot))
        {
            args.Add("--zcode-app");
            args.Add(settings.ZCodeRoot);
        }

        args.Add("--yes");
        return args;
    }

    private static IReadOnlyList<string> BuildTokenStatusBar(PluginDefinition plugin, string[] extraArgs, AppSettings settings)
    {
        var args = new List<string>
        {
            Path.Combine(plugin.Directory, "install.py")
        };

        args.AddRange(extraArgs);
        if (!string.IsNullOrWhiteSpace(settings.ZCodeRoot))
        {
            args.Add("--root");
            args.Add(settings.ZCodeRoot);
        }

        return args;
    }

    private static string? FindOnPath(string fileName)
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        return paths
            .Select(path => Path.Combine(path.Trim('"'), fileName))
            .FirstOrDefault(File.Exists);
    }
}
