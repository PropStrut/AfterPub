namespace AfterPub.Core.Scanning;

/// <summary>
/// Where converted output files are written, relative to their source .pub files.
/// See CLAUDE.md section 3.3 and section 8 (settings).
/// </summary>
public enum OutputLocationMode
{
    /// <summary>Write each output next to its source .pub file.</summary>
    SameFolder,

    /// <summary>
    /// Write outputs under a separate root folder, mirroring the source folder
    /// structure so files with the same name in different source folders never collide.
    /// </summary>
    SeparateFolder
}
