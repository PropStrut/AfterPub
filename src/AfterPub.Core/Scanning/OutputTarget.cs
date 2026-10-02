namespace AfterPub.Core.Scanning;

/// <summary>
/// The kinds of output AfterPub can produce from a source .pub file.
/// See CLAUDE.md section 3.2.
/// </summary>
public enum OutputTarget
{
    /// <summary>A faithful visual copy of the source. Always available. File extension: .pdf</summary>
    Pdf,

    /// <summary>An editable companion, produced via LibreOffice Draw. File extension: .odg</summary>
    Odg,

    Sla
}
