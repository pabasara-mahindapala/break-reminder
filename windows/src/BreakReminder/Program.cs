namespace BreakReminder;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        using var mutex = new Mutex(true, @"Local\BreakReminder", out bool isFirstInstance);
        if (!isFirstInstance) return;

        ApplicationConfiguration.Initialize();

        var settings = SettingsStore.LoadSettings();
        // --fast shortens every interval 20x (eye break after 1 minute) for manual testing.
        double scale = args.Contains("--fast") ? 1.0 / 20 : 1;
        using var context = new TrayContext(settings ?? new Settings(), settingsInvalid: settings == null, scale);
        Application.Run(context);
    }
}
