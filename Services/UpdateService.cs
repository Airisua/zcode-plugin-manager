using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

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
        var apiUrl = BuildReleaseApiUrl(repository);
        using var httpClient = CreateHttpClient();
        using var response = await httpClient.GetAsync(apiUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
        if (!TryParseVersion(tag, out var version) || version <= currentVersion)
        {
            return null;
        }

        var zipAsset = FindAsset(root, ZipAssetName);
        var checksumAsset = FindAsset(root, ChecksumAssetName);
        if (zipAsset is null || checksumAsset is null)
        {
            throw new InvalidOperationException("最新 Release 缺少 ZCodePluginManager.zip 或 SHA256SUMS。");
        }

        return new UpdateCheckResult(
            version,
            tag,
            root.GetProperty("html_url").GetString() ?? string.Empty,
            zipAsset.Value.GetProperty("browser_download_url").GetString() ?? string.Empty,
            checksumAsset.Value.GetProperty("browser_download_url").GetString() ?? string.Empty,
            root.TryGetProperty("body", out var body) ? body.GetString() ?? string.Empty : string.Empty);
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

    private static HttpClient CreateHttpClient()
    {
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"ZCodePluginManager/{GetLocalVersion()}");
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return httpClient;
    }

    private static string BuildReleaseApiUrl(string repository)
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

        return $"https://api.github.com/repos/{segments[0]}/{segments[1].TrimEnd(".git".ToCharArray())}/releases/latest";
    }

    private static JsonElement? FindAsset(JsonElement release, string name)
    {
        if (!release.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.GetProperty("name").GetString()?.Equals(name, StringComparison.OrdinalIgnoreCase) == true)
            {
                return asset;
            }
        }

        return null;
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
