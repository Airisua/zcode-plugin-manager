using ZCodePluginManager.Services;

namespace PluginManager.Tests;

internal static class GitServiceTests
{
    public static Task NormalizesPlainGitHubRepository()
    {
        var valid = GitService.TryNormalizeGitHubUrl(
            "https://github.com/owner/repo",
            out var url,
            out var branch,
            out var name);

        TestRunner.True(valid);
        TestRunner.Equal("https://github.com/owner/repo.git", url);
        TestRunner.Null(branch);
        TestRunner.Equal("repo", name);
        return Task.CompletedTask;
    }

    public static Task NormalizesBranchUrlAndSshUrl()
    {
        GitService.TryNormalizeGitHubUrl(
            "https://github.com/owner/repo/tree/v1.0",
            out var branchUrl,
            out var branch,
            out var _);
        TestRunner.Equal("https://github.com/owner/repo.git", branchUrl);
        TestRunner.Equal("v1.0", branch);

        GitService.TryNormalizeGitHubUrl(
            "git@github.com:owner/repo.git",
            out var sshUrl,
            out var sshBranch,
            out var _);
        TestRunner.Equal("https://github.com/owner/repo.git", sshUrl);
        TestRunner.Null(sshBranch);
        return Task.CompletedTask;
    }

    public static Task RejectsInvalidRepositoryInputs(string input)
    {
        TestRunner.False(GitService.TryNormalizeGitHubUrl(input, out _, out _, out _));
        return Task.CompletedTask;
    }
}
