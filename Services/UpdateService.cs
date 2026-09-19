using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;

namespace ZCodePluginManager.Services;

public sealed record UpdateCheckResult(
    Version Version,
    string Tag,
    string ReleaseUrl,
    string ZipUrl,
    string ChecksumUrl,
    string Notes);

public static class UpdateService
{
    public const string DefaultRepository = "https://github.com/Airisua/zcode-plugin-manager";
    private const string ZipAssetName = "ZCodePluginManager.zip";
    private const string ChecksumAssetName = "SHA256SUMS";

    public static async Task<UpdateCheckResult?> CheckAsync(
        string repository,
        Version currentVersion,
        CancellationToken cancellationToken = default)
    {
        var repositoryUrl = BuildRepositoryUrl(repository);
        var latestUrl = new Uri(repositoryUrl, "releases/latest");
        using var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false
        };
        using var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(15)
        };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"ZCodePluginManager/{GetLocalVersion()}");

        using var response = await httpClient.GetAsync(
            latestUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        var tag = response.Headers.Location is { } location
            ? ExtractTagFromLocation(MakeAbsolute(latestUrl, location))
            : null;
        if (string.IsNullOrWhiteSpace(tag) ||
            !TryParseVersion(tag, out var version) ||
            version <= currentVersion)
        {
            return null;
        }

        return new UpdateCheckResult(
            version,
            tag,
            new Uri(repositoryUrl, $"releases/tag/{Uri.EscapeDataString(tag)}").ToString(),
            new Uri(repositoryUrl, $"releases/latest/download/{ZipAssetName}").ToString(),
            new Uri(repositoryUrl, $"releases/latest/download/{ChecksumAssetName}").ToString(),
            string.Empty);
    }

    public static async Task<string> PrepareAsync(
        UpdateCheckResult update,
        string workingDirectory,
        Action<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var updateRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZCodePluginManager",
            "updates",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(updateRoot);

        progress?.Invoke("下载更新包...");
        var zipPath = Path.Combine(updateRoot, ZipAssetName);
        await DownloadFileAsync(update.ZipUrl, zipPath, cancellationToken);

        progress?.Invoke("下载校验文件...");
        var checksumPath = Path.Combine(updateRoot, ChecksumAssetName);
        await DownloadFileAsync(update.ChecksumUrl, checksumPath, cancellationToken);

        progress?.Invoke("校验 SHA-256...");
        await VerifyChecksum(zipPath, ReadExpectedChecksum(checksumPath));

        progress?.Invoke("准备更新文件...");
        var stageDirectory = Path.Combine(updateRoot, "stage");
        Directory.CreateDirectory(stageDirectory);
        ZipFile.ExtractToDirectory(zipPath, stageDirectory, overwriteFiles: true);

        var stagedExecutable = FindExecutable(stageDirectory);
        if (stagedExecutable is null)
        {
            throw new InvalidOperationException("更新包中没有 ZCodePluginManager.exe。");
        }

        _ = workingDirectory;
        return stagedExecutable;
    }

    public static void ApplyUpdate(
        string stagedExecutable,
        string currentExecutable,
        int currentProcessId)
    {
        var stageDirectory = Path.GetDirectoryName(stagedExecutable)
            ?? throw new InvalidOperationException("无法确定更新目录。");
        var targetDirectory = Path.GetDirectoryName(currentExecutable)
            ?? throw new InvalidOperationException("无法确定当前程序目录。");
        var updateRoot = Path.GetDirectoryName(stageDirectory)
            ?? throw new InvalidOperationException("无法确定更新根目录。");

        var scriptPath = Path.Combine(updateRoot, "apply-update.cmd");
        var script = $"""
            @echo off
            setlocal enableextensions
            set "SOURCE={stageDirectory}"
            set "TARGET={targetDirectory}"
            set "BACKUP={Path.Combine(updateRoot, "backup")}"
            set "PID={currentProcessId}"
            set "LOG={Path.Combine(updateRoot, "update.log")}"

            :wait
            tasklist /FI "PID eq %PID%" | find /I "%PID%" >nul 2>&1
            if not errorlevel 1 (
                timeout /t 1 /nobreak >nul
                goto wait
            )

            xcopy "%TARGET%" "%BACKUP%" /E /I /Y /Q >nul 2>&1
            robocopy "%SOURCE%" "%TARGET%" /E /MOVE /NFL /NDL /NJH /NJS /NP >nul
            if errorlevel 8 (
                xcopy "%BACKUP%" "%TARGET%" /E /I /Y /Q >nul 2>&1
                exit /b 1
            )

            start "" "%TARGET%\ZCodePluginManager.exe"
            rd /s /q "{updateRoot}" >nul 2>&1
            exit /b 0
            """;
        File.WriteAllText(scriptPath, script);

        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = updateRoot
        };
        System.Diagnostics.Process.Start(startInfo);
    }

    public static bool TryParseVersion(string value, out Version version)
    {
        var normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        var plusIndex = normalized.IndexOf('+', StringComparison.Ordinal);
        if (plusIndex >= 0)
        {
            normalized = normalized[..plusIndex];
        }

        var dashIndex = normalized.IndexOf('-', StringComparison.Ordinal);
        if (dashIndex >= 0)
        {
            normalized = normalized[..dashIndex];
        }

        return Version.TryParse(normalized, out version!);
    }

    public static string? ReadExpectedChecksum(string checksumFilePath)
    {
        foreach (var line in File.ReadAllLines(checksumFilePath))
        {
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 &&
                parts[1].Trim('*', '"').Equals(ZipAssetName, StringComparison.OrdinalIgnoreCase))
            {
                return parts[0];
            }
        }

        return null;
    }

    public static string? TryGetRedirectTag(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return null;
        }

        const string marker = "/releases/tag/";
        var index = location.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var tag = Uri.UnescapeDataString(location[(index + marker.Length)..]).Trim('/');
        return tag.Length == 0 ? null : tag.Split('/')[0];
    }

    private static Uri BuildRepositoryUrl(string repository)
    {
        if (!Uri.TryCreate(repository, UriKind.Absolute, out var uri) ||
            uri.Host != "github.com" ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("更新仓库必须是有效的 GitHub HTTPS 地址。");
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            throw new ArgumentException("更新仓库地址缺少 owner/repository。");
        }

        return new Uri($"https://github.com/{segments[0]}/{segments[1].TrimEnd(".git".ToCharArray())}/");
    }

    private static string ExtractTagFromLocation(Uri location)
    {
        var absolute = location.ToString();
        var tag = TryGetRedirectTag(absolute);
        if (string.IsNullOrWhiteSpace(tag))
        {
            throw new InvalidOperationException($"无法从 GitHub 返回地址识别版本：{absolute}");
        }

        return tag;
    }

    private static Uri MakeAbsolute(Uri baseUri, Uri location)
    {
        return location.IsAbsoluteUri ? location : new Uri(baseUri, location);
    }

    private static async Task DownloadFileAsync(
        string url,
        string destination,
        CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"ZCodePluginManager/{GetLocalVersion()}");
        await using var source = await httpClient.GetStreamAsync(url, cancellationToken);
        await using var target = File.Create(destination);
        await source.CopyToAsync(target, cancellationToken);
    }

    private static async Task VerifyChecksum(string filePath, string? expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            throw new InvalidOperationException("校验文件中没有更新包哈希。");
        }

        await using var stream = File.OpenRead(filePath);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("更新包 SHA-256 校验失败。");
        }
    }

    private static string? FindExecutable(string stageDirectory)
    {
        return Directory.EnumerateFiles(stageDirectory, "ZCodePluginManager.exe", SearchOption.AllDirectories)
            .FirstOrDefault();
    }

    private static string GetLocalVersion()
    {
        return typeof(UpdateService).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }
}
