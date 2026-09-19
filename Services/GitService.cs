using System.IO;
using ZCodePluginManager.Models;

namespace ZCodePluginManager.Services;

public static class GitService
{
    public static bool TryNormalizeGitHubUrl(
        string input,
        out string repositoryUrl,
        out string? branch,
        out string repositoryName)
    {
        repositoryUrl = string.Empty;
        branch = null;
        repositoryName = string.Empty;

        var source = input.Trim();
        if (source.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
        {
            source = $"https://github.com/{source["git@github.com:".Length..].TrimEnd('/')}";
        }

        if (!Uri.TryCreate(source, UriKind.Absolute, out var uri) ||
            uri.Host != "github.com" ||
            uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return false;
        }

        var owner = segments[0];
        var repository = segments[1].TrimEnd(".git".ToCharArray());
        if (segments.Length > 2 && segments[2].Equals("tree", StringComparison.OrdinalIgnoreCase))
        {
            branch = string.Join('/', segments.Skip(3));
        }

        repositoryUrl = $"https://github.com/{owner}/{repository}.git";
        repositoryName = repository;
        return true;
    }

    public static async Task<ProcessResult> CloneAsync(
        string repositoryUrl,
        string? branch,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "clone", "--depth", "1" };
        if (!string.IsNullOrWhiteSpace(branch))
        {
            args.Add("--branch");
            args.Add(branch);
        }

        args.Add(repositoryUrl);
        args.Add(destinationDirectory);
        return await ProcessRunner.RunAsync("git", args, Path.GetDirectoryName(destinationDirectory)!, cancellationToken);
    }

    public static async Task<ProcessResult> PullAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        return await ProcessRunner.RunAsync("git", ["pull", "--ff-only"], workingDirectory, cancellationToken);
    }
}
