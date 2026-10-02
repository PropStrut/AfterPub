namespace AfterPub.Core.Engines;

/// <summary>
/// The user's engine preference, as a setting. "Auto" is not an engine itself — it
/// tells <see cref="EngineResolver"/> to pick the best available one (Publisher if
/// present, otherwise LibreOffice), per CLAUDE.md section 3.1.
/// </summary>
public enum EngineSelection
{
    Auto,
    Publisher,
    LibreOffice,
    Scribus
}
