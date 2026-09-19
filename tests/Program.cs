using PluginManager.Tests;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        _ = args;
        return await TestRunner.RunAsync([
            GitServiceTests.NormalizesPlainGitHubRepository,
            GitServiceTests.NormalizesBranchUrlAndSshUrl,
            () => GitServiceTests.RejectsInvalidRepositoryInputs("https://gitlab.com/owner/repo"),
            () => GitServiceTests.RejectsInvalidRepositoryInputs("not-a-url"),
            () => GitServiceTests.RejectsInvalidRepositoryInputs("https://github.com/owner"),
            MarkdownDigestTests.ExtractsTitleSummaryAndFeatures,
            PluginCatalogTests.DiscoversGenericPythonPlugin,
            UpdateServiceTests.ParsesVersionTags,
            UpdateServiceTests.ReadsChecksumManifest,
            UpdateServiceTests.ExtractsTagFromRedirect
        ]);
    }
}
