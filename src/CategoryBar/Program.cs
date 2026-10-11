using Velopack;

namespace CategoryBar;

internal static class Program
{
    [STAThread]
    public static void Main()
    {
        // Installer hooks must run before WPF, configuration and the single-instance mutex.
        VelopackApp.Build().SetAutoApplyOnStartup(false)
            .OnBeforeUninstallFastCallback(_ => StartupRegistration.RemoveOwnEntry()).Run();
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
