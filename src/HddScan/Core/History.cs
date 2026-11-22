using System.Globalization;
using System.Text.Json;

namespace HddScan.Core;

public sealed class Snapshot
{
    public string Root { get; set; } = "";
    public DateTime TimeUtc { get; set; }
    public long Total { get; set; }
    public Dictionary<string, long> Folders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record SnapshotInfo(string File, DateTime TimeUtc, long Total)
{
    public override string ToString() => $"{TimeUtc.ToLocalTime():yyyy-MM-dd HH:mm}   (รวม {Fmt.Size(Total)})";
}

public sealed record DiffRow(string Path, int Depth, long Before, long After)
{
    public long Delta => After - Before;
}

/// <summary>Stores a light summary of each scan so the next scan can show what grew.</summary>
public static class History
{
    const int MaxDepth = 6;
    const long MinSize = 10L << 20; // 10 MB
    const int Keep = 40;

    public static string BaseDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HddScan", "history");

    static string DirFor(string root)
    {
        var safe = new string(root.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray()).Trim('_');
        if (safe.Length == 0) safe = "root";
        return Path.Combine(BaseDir, safe);
    }

    public static Snapshot Build(ScanResult r)
    {
        var s = new Snapshot { Root = r.RootPath, TimeUtc = r.ScannedAtUtc, Total = r.Root.TotalSize };
        void Walk(DirNode n, string path, int depth)
        {
            s.Folders[path] = n.TotalSize;
            if (depth >= MaxDepth) return;
            foreach (var c in n.Children)
            {
                if (c.TotalSize < MinSize) break; // children are sorted desc
                Walk(c, Path.Combine(path, c.Name), depth + 1);
            }
        }
        Walk(r.Root, r.RootPath, 0);
        return s;
    }

    public static List<SnapshotInfo> List(string root)
    {
        var dir = DirFor(root);
        var list = new List<SnapshotInfo>();
        if (!Directory.Exists(dir)) return list;
        foreach (var f in Directory.EnumerateFiles(dir, "*.json"))
        {
            var parts = Path.GetFileNameWithoutExtension(f).Split('_');
            if (parts.Length < 3) continue;
            if (!DateTime.TryParseExact(parts[0] + parts[1], "yyyyMMddHHmmss", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var t)) continue;
            long.TryParse(parts[2], out var total);
            list.Add(new SnapshotInfo(f, t, total));
        }
        return list.OrderByDescending(x => x.TimeUtc).ToList();
    }

    public static void Save(Snapshot s)
    {
        var dir = DirFor(s.Root);
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"{s.TimeUtc:yyyyMMdd_HHmmss}_{s.Total}.json");
        File.WriteAllText(file, JsonSerializer.Serialize(s));
        foreach (var old in List(s.Root).Skip(Keep))
            try { File.Delete(old.File); } catch { }
    }

    public static Snapshot? Load(string file)
    {
        try
        {
            var s = JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(file));
            if (s == null) return null;
            s.Folders = new Dictionary<string, long>(s.Folders, StringComparer.OrdinalIgnoreCase);
            return s;
        }
        catch { return null; }
    }

    public static List<DiffRow> Diff(Snapshot before, Snapshot after, long minDelta)
    {
        var rows = new List<DiffRow>();
        var baseDepth = Analyzer.BaseSegments(after.Root).Count;
        foreach (var path in before.Folders.Keys.Union(after.Folders.Keys, StringComparer.OrdinalIgnoreCase))
        {
            before.Folders.TryGetValue(path, out var b);
            after.Folders.TryGetValue(path, out var a);
            // A folder missing from one snapshot may just have been under the 10 MB threshold.
            if (Math.Abs(a - b) < minDelta) continue;
            rows.Add(new DiffRow(path, Analyzer.BaseSegments(path).Count - baseDepth, b, a));
        }
        return rows.OrderByDescending(r => r.Delta).ToList();
    }

    /// <summary>Hide a parent row when one of its children explains ≥ 90% of the change.</summary>
    public static List<DiffRow> Collapse(List<DiffRow> rows)
    {
        var byParent = rows.GroupBy(r => Path.GetDirectoryName(r.Path) ?? "", StringComparer.OrdinalIgnoreCase)
                           .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        return rows.Where(r =>
        {
            if (!byParent.TryGetValue(r.Path, out var kids)) return true;
            return !kids.Any(k => Math.Sign(k.Delta) == Math.Sign(r.Delta) && Math.Abs(k.Delta) >= Math.Abs(r.Delta) * 0.9);
        }).ToList();
    }
}
