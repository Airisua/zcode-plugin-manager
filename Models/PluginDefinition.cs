namespace ZCodePluginManager.Models;

public enum InstallerKind
{
    KeySmith,
    TokenStatusBar,
    PythonScript,
    PowerShellScript,
    Unsupported
}

public sealed class PluginDefinition
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Directory { get; init; }
    public required string Summary { get; init; }
    public required IReadOnlyList<string> Features { get; init; }
    public required InstallerKind Installer { get; init; }
    public string? RepositoryUrl { get; init; }
    public string ReadmePath { get; init; } = string.Empty;
    public string ReadmeText { get; init; } = string.Empty;
}

public sealed class PluginStatus
{
    public bool Installed { get; init; }
    public bool Supported { get; init; } = true;
    public string StateText { get; init; } = "未安装";
    public string StateColor { get; init; } = "#5E6B7E";
}
