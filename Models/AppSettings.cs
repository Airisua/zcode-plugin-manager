namespace ZCodePluginManager.Models;

public sealed class AppSettings
{
    public string PluginsRoot { get; set; } = string.Empty;
    public string ZCodeRoot { get; set; } = string.Empty;
    public string PythonPath { get; set; } = string.Empty;
}
