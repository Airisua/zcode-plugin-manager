using System.IO;
using ZCodePluginManager.Services;

namespace PluginManager.Tests;

internal static class UpdateServiceTests
{
    public static Task ParsesVersionTags()
    {
        TestRunner.True(UpdateService.TryParseVersion("v1.2.3", out var version));
        TestRunner.Equal(new Version(1, 2, 3), version);
        TestRunner.True(UpdateService.TryParseVersion("0.10.0-beta+abc", out var prerelease));
        TestRunner.Equal(new Version(0, 10, 0), prerelease);
        TestRunner.False(UpdateService.TryParseVersion("not-a-version", out var _));
        return Task.CompletedTask;
    }

    public static async Task ReadsChecksumManifest()
    {
        var path = Path.Combine(Path.GetTempPath(), $"zcode-checksum-{Guid.NewGuid():N}.txt");
        try
        {
            await File.WriteAllTextAsync(
                path,
                "abcdef1234567890  other.zip\n1234567890abcdef  ZCodePluginManager.zip\n");
            TestRunner.Equal("1234567890abcdef", UpdateService.ReadExpectedChecksum(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    public static Task ExtractsTagFromRedirect()
    {
        TestRunner.Equal(
            "v0.3.0",
            UpdateService.TryGetRedirectTag("https://github.com/owner/repo/releases/tag/v0.3.0"));
        TestRunner.Equal(
            "v1.0.0-rc.1",
            UpdateService.TryGetRedirectTag("https://github.com/owner/repo/releases/tag/v1%2E0%2E0-rc%2E1"));
        TestRunner.Null(UpdateService.TryGetRedirectTag("https://github.com/owner/repo/releases"));
        TestRunner.Null(UpdateService.TryGetRedirectTag(null));
        return Task.CompletedTask;
    }
}
