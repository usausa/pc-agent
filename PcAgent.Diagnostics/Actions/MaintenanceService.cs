namespace PcAgent.Diagnostics.Actions;

using System.IO;

// 削除候補 1 件。
public readonly record struct CleanupItem(string Path, long Bytes);

// 削除計画(列挙のみ。実削除はしない)。
public sealed record CleanupPlan(IReadOnlyList<CleanupItem> Items, long TotalBytes);

// 削除結果。
public sealed record CleanupResult(int Deleted, int Failed, long BytesFreed);

// 一時ファイル / bin・obj のクリーンアップ。列挙(Plan)と削除(Execute)を分離し、承認後にのみ削除する。
public static class MaintenanceService
{
    public static CleanupPlan PlanTemp() => Plan(EnumerateTemp());

    public static CleanupPlan PlanBinObj(IEnumerable<string> roots) => Plan(EnumerateBinObj(roots));

    public static CleanupResult Execute(CleanupPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var deleted = 0;
        var failed = 0;
        var freed = 0L;
        foreach (var item in plan.Items)
        {
            try
            {
                if (File.Exists(item.Path))
                {
                    File.Delete(item.Path);
                    deleted++;
                    freed += item.Bytes;
                }
                else if (Directory.Exists(item.Path))
                {
                    Directory.Delete(item.Path, recursive: true);
                    deleted++;
                    freed += item.Bytes;
                }
            }
            catch (IOException)
            {
                failed++;
            }
            catch (UnauthorizedAccessException)
            {
                failed++;
            }
        }

        return new CleanupResult(deleted, failed, freed);
    }

    private static CleanupPlan Plan(IEnumerable<string> paths)
    {
        var items = new List<CleanupItem>();
        var total = 0L;
        foreach (var path in paths)
        {
            var size = SizeOf(path);
            items.Add(new CleanupItem(path, size));
            total += size;
        }

        return new CleanupPlan(items, total);
    }

    private static IEnumerable<string> EnumerateTemp()
    {
        var temp = Path.GetTempPath();
        if (!Directory.Exists(temp))
        {
            yield break;
        }

        string[] entries;
        try
        {
            entries = Directory.GetFileSystemEntries(temp);
        }
        catch (IOException)
        {
            yield break;
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var entry in entries)
        {
            yield return entry;
        }
    }

    private static IEnumerable<string> EnumerateBinObj(IEnumerable<string> roots)
    {
        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            var stack = new Stack<string>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var current = stack.Pop();
                List<string> subdirectories;
                try
                {
                    subdirectories = Directory.EnumerateDirectories(current).ToList();
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (var subdirectory in subdirectories)
                {
                    var name = Path.GetFileName(subdirectory);
                    if ((String.Equals(name, "bin", StringComparison.OrdinalIgnoreCase) || String.Equals(name, "obj", StringComparison.OrdinalIgnoreCase)) && HasProject(current))
                    {
                        yield return subdirectory;
                    }
                    else
                    {
                        stack.Push(subdirectory);
                    }
                }
            }
        }
    }

    private static bool HasProject(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory, "*.csproj").Any() || Directory.EnumerateFiles(directory, "*.vbproj").Any();
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static long SizeOf(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return new FileInfo(path).Length;
            }

            if (Directory.Exists(path))
            {
                var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
                var total = 0L;
                foreach (var file in Directory.EnumerateFiles(path, "*", options))
                {
                    total += FileLength(file);
                }

                return total;
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return 0L;
    }

    private static long FileLength(string file)
    {
        try
        {
            return new FileInfo(file).Length;
        }
        catch (IOException)
        {
            return 0L;
        }
        catch (UnauthorizedAccessException)
        {
            return 0L;
        }
    }
}
