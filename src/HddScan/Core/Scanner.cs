using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Enumeration;

namespace HddScan.Core;

/// <summary>
/// Fast parallel directory scanner. Uses FileSystemEnumerable (NtQueryDirectoryFile on Windows)
/// without allocating FileInfo objects. Skips junctions/symlinks so nothing is counted twice,
/// and does not count cloud-only (OneDrive Files On-Demand) placeholders.
/// </summary>
public sealed class Scanner
{
    public const int TopN = 1000;

    long _files, _dirs, _bytes, _cloudBytes, _cloudFiles;
    int _denied, _links;

    public long Files => Interlocked.Read(ref _files);
    public long Dirs => Interlocked.Read(ref _dirs);
    public long Bytes => Interlocked.Read(ref _bytes);
    public volatile string CurrentPath = "";

    readonly ConcurrentBag<PriorityQueue<FileItem, long>> _heaps = new();
    readonly ConcurrentBag<Dictionary<string, ExtStat>> _exts = new();
    ThreadLocal<PriorityQueue<FileItem, long>>? _heap;
    ThreadLocal<Dictionary<string, ExtStat>>? _ext;
    readonly List<FileItem> _rootFiles = new();

    const FileAttributes RecallOnDataAccess = (FileAttributes)0x00400000;
    const FileAttributes RecallOnOpen = (FileAttributes)0x00040000;

    readonly record struct Entry(string Name, bool IsDir, long Length, FileAttributes Attr, DateTime MtimeUtc);

    static readonly EnumerationOptions Opts = new()
    {
        IgnoreInaccessible = false,
        RecurseSubdirectories = false,
        AttributesToSkip = 0,
        ReturnSpecialDirectories = false,
        BufferSize = 64 * 1024,
    };

    public ScanResult Scan(string rootPath, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        rootPath = Path.GetFullPath(rootPath);

        _heap = new(() => { var h = new PriorityQueue<FileItem, long>(); _heaps.Add(h); return h; });
        _ext = new(() => { var d = new Dictionary<string, ExtStat>(StringComparer.OrdinalIgnoreCase); _exts.Add(d); return d; });

        var root = new DirNode { Name = rootPath };
        try { root.LastWriteUtc = Directory.GetLastWriteTimeUtc(rootPath); } catch { }

        ScanDir(root, rootPath, 0, ct);
        ct.ThrowIfCancellationRequested();

        // merge per-thread top-N heaps
        var all = new PriorityQueue<FileItem, long>();
        foreach (var h in _heaps)
            while (h.TryDequeue(out var f, out _)) Push(all, f);
        var top = new List<FileItem>(all.Count);
        while (all.TryDequeue(out var f, out _)) top.Add(f);
        top.Reverse();

        // merge per-thread extension stats
        var exts = new Dictionary<string, ExtStat>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in _exts)
            foreach (var (k, v) in d)
            {
                if (!exts.TryGetValue(k, out var s)) exts[k] = s = new ExtStat();
                s.Size += v.Size;
                s.Count += v.Count;
            }

        _heap.Dispose();
        _ext.Dispose();

        var result = new ScanResult
        {
            Root = root,
            RootPath = rootPath,
            TopFiles = top,
            RootFiles = _rootFiles.OrderByDescending(f => f.Size).ToList(),
            Extensions = exts,
            CloudOnlyBytes = _cloudBytes,
            CloudOnlyFiles = _cloudFiles,
            DeniedDirs = _denied,
            LinkDirs = _links,
            ScannedAtUtc = DateTime.UtcNow,
        };

        var driveRoot = Path.GetPathRoot(rootPath) ?? rootPath;
        result.IsDriveRoot = string.Equals(
            rootPath.TrimEnd('\\', '/'), driveRoot.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
        try
        {
            var di = new DriveInfo(driveRoot);
            result.DriveTotal = di.TotalSize;
            result.DriveFree = di.TotalFreeSpace;
        }
        catch { }

        result.Elapsed = sw.Elapsed;
        return result;
    }

    static void Push(PriorityQueue<FileItem, long> heap, FileItem f)
    {
        if (heap.Count < TopN) heap.Enqueue(f, f.Size);
        else if (heap.TryPeek(out _, out var min) && f.Size > min) heap.EnqueueDequeue(f, f.Size);
    }

    void ScanDir(DirNode node, string path, int depth, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;
        CurrentPath = path;
        Interlocked.Increment(ref _dirs);

        var subdirs = new List<(DirNode Node, string Path)>();
        long own = 0;
        int ownFiles = 0;
        DateTime last = node.LastWriteUtc;
        var heap = _heap!.Value!;
        var ext = _ext!.Value!;

        try
        {
            var en = new FileSystemEnumerable<Entry>(path,
                (ref FileSystemEntry e) => new Entry(
                    e.FileName.ToString(),
                    e.IsDirectory,
                    e.IsDirectory ? 0 : e.Length,
                    e.Attributes,
                    e.LastWriteTimeUtc.UtcDateTime),
                Opts);

            foreach (var e in en)
            {
                if (e.IsDir)
                {
                    if ((e.Attr & FileAttributes.ReparsePoint) != 0)
                    {
                        Interlocked.Increment(ref _links);
                        continue;
                    }
                    var c = new DirNode { Name = e.Name, Parent = node, LastWriteUtc = e.MtimeUtc };
                    subdirs.Add((c, Path.Combine(path, e.Name)));
                    continue;
                }

                if ((e.Attr & (RecallOnDataAccess | RecallOnOpen | FileAttributes.Offline)) != 0)
                {
                    Interlocked.Add(ref _cloudBytes, e.Length);
                    Interlocked.Increment(ref _cloudFiles);
                    continue;
                }

                own += e.Length;
                ownFiles++;
                if (e.MtimeUtc > last) last = e.MtimeUtc;

                var x = Path.GetExtension(e.Name);
                var key = x.Length == 0 ? "" : x.ToLowerInvariant();
                if (!ext.TryGetValue(key, out var st)) ext[key] = st = new ExtStat();
                st.Size += e.Length;
                st.Count++;

                if (heap.Count < TopN)
                    heap.Enqueue(new FileItem(Path.Combine(path, e.Name), e.Length, e.MtimeUtc), e.Length);
                else if (heap.TryPeek(out _, out var min) && e.Length > min)
                    heap.EnqueueDequeue(new FileItem(Path.Combine(path, e.Name), e.Length, e.MtimeUtc), e.Length);

                if (depth == 0)
                    lock (_rootFiles) _rootFiles.Add(new FileItem(Path.Combine(path, e.Name), e.Length, e.MtimeUtc));
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            node.AccessDenied = true;
            Interlocked.Increment(ref _denied);
        }

        Interlocked.Add(ref _files, ownFiles);
        Interlocked.Add(ref _bytes, own);
        node.OwnSize = own;
        node.OwnFiles = ownFiles;

        var children = new List<DirNode>(subdirs.Count);
        foreach (var s in subdirs) children.Add(s.Node);
        node.Children = children;

        if (subdirs.Count > 0)
        {
            if (depth < 4 && subdirs.Count > 1)
                Parallel.ForEach(subdirs, s => ScanDir(s.Node, s.Path, depth + 1, ct));
            else
                foreach (var s in subdirs) ScanDir(s.Node, s.Path, depth + 1, ct);
        }

        long total = own, files = ownFiles;
        int dirs = children.Count;
        foreach (var c in children)
        {
            total += c.TotalSize;
            files += c.TotalFiles;
            dirs += c.TotalDirs;
            if (c.LastWriteUtc > last) last = c.LastWriteUtc;
        }
        node.TotalSize = total;
        node.TotalFiles = files;
        node.TotalDirs = dirs;
        node.LastWriteUtc = last;
        children.Sort((a, b) => b.TotalSize.CompareTo(a.TotalSize));
    }
}
