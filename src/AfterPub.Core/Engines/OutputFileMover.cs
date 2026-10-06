namespace AfterPub.Core.Engines;

/// <summary>
/// Puts a finished, temporary output file into its final place without ever leaving a partial file
/// there. Every engine writes its result to a private temporary folder first and calls this only
/// after the conversion succeeded, so an existing output file always means a complete one, and a
/// good file that was already there is replaced only at the very last moment (CLAUDE.md section 3.3).
/// </summary>
public static class OutputFileMover
{
    /// <summary>Ends the temporary name used when the final folder is on a different drive.</summary>
    public const string StagingSuffix = ".afterpub-tmp";

    /// <summary>
    /// Moves <paramref name="tempPath"/> to <paramref name="finalPath"/>, replacing any file already
    /// there and creating the final folder if needed. On the same drive this is a rename. On a
    /// different drive (or a network share) a plain move would be a copy followed by a delete, which
    /// could leave a half-written file under the real name if interrupted, so the file is copied to
    /// a temporary name beside the destination first and renamed into place.
    /// </summary>
    public static void MoveIntoPlace(string tempPath, string finalPath)
    {
        string? folder = Path.GetDirectoryName(finalPath);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        if (OnSameVolume(tempPath, finalPath))
        {
            File.Move(tempPath, finalPath, overwrite: true);
            return;
        }

        string stagingPath = finalPath + StagingSuffix;
        try
        {
            File.Copy(tempPath, stagingPath, overwrite: true);
            File.Move(stagingPath, finalPath, overwrite: true);
        }
        catch
        {
            TryDeleteFile(stagingPath);
            throw;
        }

        TryDeleteFile(tempPath);
    }

    private static bool OnSameVolume(string firstPath, string secondPath)
    {
        string? firstRoot = Path.GetPathRoot(Path.GetFullPath(firstPath));
        string? secondRoot = Path.GetPathRoot(Path.GetFullPath(secondPath));
        return string.Equals(firstRoot, secondRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup; the temporary folder is deleted by the caller anyway.
        }
    }
}
