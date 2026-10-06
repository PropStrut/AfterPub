using AfterPub.Core.Settings;

namespace AfterPub.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // The color mode must be set before any window is created, so the saved choice is read
        // here. If the settings file cannot be read, the app simply starts in light mode.
        Application.SetColorMode(ReadColorMode() == AppColorMode.Dark
            ? SystemColorMode.Dark
            : SystemColorMode.Classic);

        Application.Run(new MainForm());
    }

    private static AppColorMode ReadColorMode()
    {
        try
        {
            AppSettingsStore store = new AppSettingsStore(AppSettingsStore.GetDefaultFilePath());
            return store.Load().ColorMode;
        }
        catch (Exception)
        {
            return AppColorMode.Light;
        }
    }
}
