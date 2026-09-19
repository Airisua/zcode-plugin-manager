using System.IO;
using ZCodePluginManager.Models;
using ZCodePluginManager.Services;

namespace PluginManager.Tests;

internal static class PluginCatalogTests
{
    public static async Task DiscoversGenericPythonPlugin()
    {
        var root = Path.Combine(Path.GetTempPath(), $"zcode-plugins-{Guid.NewGuid():N}");
        var pluginDirectory = Path.Combine(root, "demo-python-plugin");
        try
        {
            Directory.CreateDirectory(pluginDirectory);
            await File.WriteAllTextAsync(Path.Combine(pluginDirectory, "install.py"), "# demo\n");
            await File.WriteAllTextAsync(Path.Combine(pluginDirectory, "README.md"), "# Demo\n\nA demo.\n");

            var plugins = PluginCatalog.Discover(root);
            TestRunner.Single(plugins, out var plugin);
            TestRunner.Equal("Demo", plugin.Title);
            TestRunner.Equal(InstallerKind.PythonScript, plugin.Installer);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
