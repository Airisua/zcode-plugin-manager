namespace PluginManager.Tests;

internal static class TestRunner
{
    public static async Task<int> RunAsync(IReadOnlyList<Func<Task>> tests)
    {
        var passed = 0;
        foreach (var test in tests)
        {
            try
            {
                await test();
                passed++;
                Console.WriteLine($"PASS {passed:00}");
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"FAIL {passed + 1:00}: {exception}");
                return 1;
            }
        }

        Console.WriteLine($"全部通过：{passed} 个测试。");
        return 0;
    }

    public static void Equal<T>(T expected, T actual, string? message = null)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message ?? "值不相等"}：期望 {expected}，实际 {actual}");
        }
    }

    public static void True(bool value, string? message = null)
    {
        if (!value)
        {
            throw new InvalidOperationException(message ?? "条件应为 true");
        }
    }

    public static void False(bool value, string? message = null)
    {
        if (value)
        {
            throw new InvalidOperationException(message ?? "条件应为 false");
        }
    }

    public static void Null<T>(T? value, string? message = null)
    {
        if (value is not null)
        {
            throw new InvalidOperationException(message ?? "值应为 null");
        }
    }

    public static void Single<T>(IReadOnlyList<T> items, out T item)
    {
        if (items.Count != 1)
        {
            throw new InvalidOperationException($"应只有 1 个项目，实际 {items.Count} 个");
        }

        item = items[0];
    }
}
