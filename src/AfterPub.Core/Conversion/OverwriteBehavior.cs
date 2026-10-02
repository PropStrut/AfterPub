namespace AfterPub.Core.Conversion;

/// <summary>
/// How to handle converting a file whose output already exists. CLAUDE.md section 8:
/// a user setting, default Ask.
/// </summary>
public enum OverwriteBehavior
{
    /// <summary>Ask before overwriting any existing output (the default).</summary>
    Ask,

    /// <summary>Never overwrite; leave existing outputs alone.</summary>
    Skip,

    /// <summary>Always overwrite without asking.</summary>
    Overwrite
}
