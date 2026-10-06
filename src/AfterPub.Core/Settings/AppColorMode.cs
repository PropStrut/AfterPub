namespace AfterPub.Core.Settings;

/// <summary>
/// The app's color theme. Kept in Core, free of WinForms types, so the settings file does not
/// depend on the UI library; the app maps it to a WinForms color mode at startup.
/// </summary>
public enum AppColorMode
{
    /// <summary>The standard light appearance. The default.</summary>
    Light,

    /// <summary>Dark appearance. Works on Windows 10 and 11.</summary>
    Dark
}
