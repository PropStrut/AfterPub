namespace AfterPub.Core.Scanning;

/// <summary>
/// Finds .pub files under a folder and reports their conversion status per
/// enabled output type. This is the read side only: it never writes, deletes, or
/// converts anything (CLAUDE.md section 13: the app is read-only on sources).
/// </summary>
public interface IPubFileScanner
{
    /// <summary>Scans according to <paramref name="options"/> and returns one row per .pub file found.</summary>
    IReadOnlyList<ConversionRow> Scan(ScanOptions options);
}
