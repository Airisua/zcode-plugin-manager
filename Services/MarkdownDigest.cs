using System.IO;
using System.Text.RegularExpressions;

namespace ZCodePluginManager.Services;

public static partial class MarkdownDigest
{
    [GeneratedRegex(@"^#{1,6}\s+(.+?)\s*#*\s*$", RegexOptions.Multiline)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^\s{0,3}(?:[-*+]\s+|\d+\.\s+)(.+)$", RegexOptions.Multiline)]
    private static partial Regex BulletRegex();

    public static string ReadText(string path)
    {
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    public static string Title(string markdown, string fallback)
    {
        var match = HeadingRegex().Match(markdown);
        return match.Success ? CleanText(match.Groups[1].Value) : fallback;
    }

    public static string Summary(string markdown, string fallback)
    {
        var firstHeading = HeadingRegex().Match(markdown);
        var content = firstHeading.Success ? markdown[(firstHeading.Index + firstHeading.Length)..] : markdown;
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 ||
                line.StartsWith('#') ||
                line.StartsWith("![", StringComparison.Ordinal) ||
                line.StartsWith("[![", StringComparison.Ordinal) ||
                line.StartsWith('|') ||
                line.StartsWith("```", StringComparison.Ordinal) ||
                line.StartsWith('>') ||
                line.StartsWith("[中文]", StringComparison.Ordinal) ||
                line.StartsWith("[English]", StringComparison.Ordinal))
            {
                continue;
            }

            return CleanText(line);
        }

        return fallback;
    }

    public static IReadOnlyList<string> Features(string markdown, IReadOnlyList<string> fallback)
    {
        var source = ExtractHeadingSection(markdown, "功能") ?? markdown;
        var bullets = BulletRegex().Matches(source)
            .Select(match => CleanText(match.Groups[1].Value))
            .Where(text => text.Length > 12)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();

        return bullets.Count > 0 ? bullets : fallback;
    }

    private static string? ExtractHeadingSection(string markdown, string heading)
    {
        var startMatch = HeadingRegex().Matches(markdown).FirstOrDefault(match =>
            match.Groups[1].Value.Trim().Equals(heading, StringComparison.OrdinalIgnoreCase));
        if (startMatch is null)
        {
            return null;
        }

        var start = startMatch.Index + startMatch.Length;
        var nextMatch = HeadingRegex().Matches(markdown[start..]).FirstOrDefault();
        return nextMatch is null ? markdown[start..] : markdown.Substring(start, nextMatch.Index);
    }

    private static string CleanText(string text)
    {
        var normalized = Regex.Replace(text.Trim(), @"\s+", " ");
        return Regex.Replace(normalized, @"\[([^\]]+)\]\([^)]+\)", "$1").Trim();
    }
}
