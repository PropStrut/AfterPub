namespace AfterPub.Core.Scanning;

/// <summary>
/// Finds .pub files under a folder and reports which derivative files (PDF, ODG, SLA)
/// already exist for each. This is the read side only: it never writes, deletes, or
/// converts anything (CLAUDE.md section 13: the app is read-only on sources).
/// </summary>
public interface IPubFileScanner
{
    /// <summary>
    /// Scans according to <paramref name="options"/> and returns one row per .pub file found.
    /// </summary>
    /// <param name="options">Where to look and where outputs are expected.</param>
    /// <param name="cancellationToken">Stops the scan; throws <see cref="OperationCanceledException"/>.</param>
    /// <param name="progress">Receives the number of .pub files found so far, after each one.</param>
    IReadOnlyList<ConversionRow> Scan(
        ScanOptions options,
        CancellationToken cancellationToken = default,
        IProgress<int>? progress = null);
}
