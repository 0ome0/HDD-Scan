namespace HddScan.Core;

/// <summary>A scanned directory with aggregated sizes.</summary>
public sealed class DirNode
{
    public string Name = "";
    public DirNode? Parent;
    public List<DirNode> Children = new();

    /// <summary>Bytes of files directly inside this folder.</summary>
    public long OwnSize;
    public int OwnFiles;

    /// <summary>Bytes of this folder including all subfolders.</summary>
    public long TotalSize;
    public long TotalFiles;
    public int TotalDirs;

    public DateTime LastWriteUtc;
    public bool AccessDenied;

    /// <summary>Usage category index (see <see cref="UsageCats"/>).</summary>
    public byte Usage;

    public string FullPath => Parent == null ? Name : Path.Combine(Parent.FullPath, Name);

    public int Depth
    {
        get { int d = 0; for (var p = Parent; p != null; p = p.Parent) d++; return d; }
    }

    /// <summary>Subtract bytes/files from this node and all ancestors (after a delete).</summary>
    public void Subtract(long bytes, long files)
    {
        for (var n = this; n != null; n = n.Parent)
        {
            n.TotalSize -= bytes;
            n.TotalFiles -= files;
        }
    }
}

public sealed record FileItem(string Path, long Size, DateTime ModifiedUtc);

/// <summary>Reference to something on disk shown in the UI.</summary>
public sealed record PathRef(string Path, bool IsDir, long Size, DirNode? Node = null);

public sealed class ExtStat
{
    public long Size;
    public long Count;
}

public sealed class ScanResult
{
    public required DirNode Root { get; init; }
    public required string RootPath { get; init; }

    /// <summary>Largest files, sorted descending.</summary>
    public List<FileItem> TopFiles { get; set; } = new();

    /// <summary>Files directly in the scan root (pagefile.sys, hiberfil.sys …).</summary>
    public List<FileItem> RootFiles { get; set; } = new();

    public Dictionary<string, ExtStat> Extensions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>OneDrive-style placeholders that are not stored locally (not counted).</summary>
    public long CloudOnlyBytes { get; set; }
    public long CloudOnlyFiles { get; set; }

    public int DeniedDirs { get; set; }
    public int LinkDirs { get; set; }

    public TimeSpan Elapsed { get; set; }
    public DateTime ScannedAtUtc { get; set; }

    public bool IsDriveRoot { get; set; }
    public long DriveTotal { get; set; }
    public long DriveFree { get; set; }

    public long DriveUsed => Math.Max(0, DriveTotal - DriveFree);

    /// <summary>Used space on the drive that the scan could not see.</summary>
    public long Unaccounted => IsDriveRoot ? Math.Max(0, DriveUsed - Root.TotalSize) : 0;
}
