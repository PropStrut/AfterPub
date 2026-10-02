namespace AfterPub.Core.Scanning;

/// <summary>
/// A single .pub file found by a scan. Immutable snapshot of what was on disk at
/// scan time; the app is read-only on sources (CLAUDE.md section 13).
/// </summary>
public sealed class SourceFile
{
    /// <summary>Full absolute path to the .pub file.</summary>
    public string FullPath { get; }

    /// <summary>Path relative to the folder the scan started from. Used for display and for mirroring output folders.</summary>
    public string RelativePath { get; }

    /// <summary>File name including extension (e.g. "Newsletter.pub").</summary>
    public string FileName { get; }

    /// <summary>File name without extension (e.g. "Newsletter"). Used to build output file names.</summary>
    public string BaseName { get; }

    /// <summary>The folder the .pub file lives in, as an absolute path.</summary>
    public string SourceFolder { get; }

    /// <summary>Size of the source file in bytes, at scan time.</summary>
    public long SizeBytes { get; }

    /// <summary>Last write time of the source file (UTC), at scan time.</summary>
    public DateTime LastWriteTimeUtc { get; }

    public SourceFile(string fullPath, string relativePath, long sizeBytes, DateTime lastWriteTimeUtc)
    {
        this.FullPath = fullPath;
        this.RelativePath = relativePath;
        this.FileName = Path.GetFileName(fullPath);
        this.BaseName = Path.GetFileNameWithoutExtension(fullPath);
        this.SourceFolder = Path.GetDirectoryName(fullPath) ?? string.Empty;
        this.SizeBytes = sizeBytes;
        this.LastWriteTimeUtc = lastWriteTimeUtc;
    }
}
