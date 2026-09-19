using ZCodePluginManager.Services;

namespace PluginManager.Tests;

internal static class MarkdownDigestTests
{
    public static Task ExtractsTitleSummaryAndFeatures()
    {
        const string markdown = """
            # Demo Plugin

            Adds a small demo capability.

            ## 功能

            - Shows a floating demo status.
            - Keeps local data read-only.
            """;

        TestRunner.Equal("Demo Plugin", MarkdownDigest.Title(markdown, "Fallback"));
        TestRunner.Equal("Adds a small demo capability.", MarkdownDigest.Summary(markdown, "Fallback"));
        TestRunner.Equal(
            "Shows a floating demo status.",
            MarkdownDigest.Features(markdown, ["Fallback feature"])[0]);
        TestRunner.Equal(
            "Keeps local data read-only.",
            MarkdownDigest.Features(markdown, ["Fallback feature"])[1]);
        return Task.CompletedTask;
    }
}
