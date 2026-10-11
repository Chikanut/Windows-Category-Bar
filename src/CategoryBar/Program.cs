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
        // A pinned category icon starts a second process; it only passes the request to the running one.
        if (Services.TaskbarGroups.Forward(Environment.GetCommandLineArgs())) return;
        // Without an id of its own Windows files this application's windows under one of the pinned category
        // icons, which start the same program; the icon then counts as running and cannot be unpinned.
        _ = Interop.Native.SetCurrentProcessExplicitAppUserModelID(Services.TaskbarGroups.ProcessAppId);
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
