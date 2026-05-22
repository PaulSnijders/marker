using System.Windows;
using System.Windows.Threading;
using Marker.App.Services;

namespace Marker.App;

/// <summary>
/// Application entry point and composition root. Wires up Marker.Core
/// services, applies the saved theme, then shows the main window.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Compose services and load settings before any window appears.
        AppServices.Initialize();
        HighlightingSetup.RegisterCustomDefinitions();
        ThemeManager.Apply(AppServices.Settings.Theme);

        DispatcherUnhandledException += OnUnhandledException;

        new MainWindow().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        AppServices.SaveSettings();
        base.OnExit(e);
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            "An unexpected error occurred:\n\n" + e.Exception.Message,
            "Marker", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
