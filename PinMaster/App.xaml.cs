using System.IO;
using System.Threading;
using System.Windows;
using PinMaster.Services;

namespace PinMaster;

public partial class App : System.Windows.Application
{
    private const string MutexName = "Global\\PinMaster.SingleInstance.2C5E8C1D-5908-4C69-91DA-0412A7DF3707";
    private const string OpenEventName = "Global\\PinMaster.OpenMainWindow.8F34C789-8D73-4CA4-9C3B-29A905D14315";
    private Mutex? _mutex;
    private bool _ownsMutex;
    private EventWaitHandle? _openEvent;
    private RegisteredWaitHandle? _openEventRegistration;
    private PersistenceService? _persistence;
    private WindowManager? _windowManager;
    private HotkeyService? _hotkeyService;
    private TrayService? _trayService;
    private MainWindow? _mainWindow;
    private SettingsWindow? _settingsWindow;

    public AppSettings Settings { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        _openEvent = new EventWaitHandle(false, EventResetMode.AutoReset, OpenEventName);
        _mutex = new Mutex(true, MutexName, out var createdNew);
        _ownsMutex = createdNew;
        if (!createdNew)
        {
            _openEvent.Set();
            _openEvent.Dispose();
            Shutdown();
            return;
        }

        base.OnStartup(e);
        ApplyTheme(Settings.ThemeOverride);

        _persistence = new PersistenceService();
        Settings = _persistence.Load();
        ApplyTheme(Settings.ThemeOverride);

        _windowManager = new WindowManager(Settings, _persistence);
        _windowManager.PinnedWindowsChanged += (_, _) => _trayService?.Refresh();

        _hotkeyService = new HotkeyService(_windowManager, Settings, _persistence);
        _hotkeyService.Start();

        _trayService = new TrayService(_windowManager, ShowMainWindow, ShowSettingsWindow, Shutdown);
        _trayService.Start();

        _windowManager.RestorePersistedPins();
        _openEventRegistration = ThreadPool.RegisterWaitForSingleObject(
            _openEvent,
            (_, _) => Dispatcher.BeginInvoke(ShowMainWindow),
            null,
            Timeout.Infinite,
            false);

        var startedFromWindowsStartup = e.Args.Any(arg => arg.Equals("--startup", StringComparison.OrdinalIgnoreCase));
        if (!startedFromWindowsStartup || e.Args.Any(arg => arg.Equals("--open", StringComparison.OrdinalIgnoreCase)))
            Dispatcher.BeginInvoke(ShowMainWindow);
    }

    public void SaveSettings()
    {
        _persistence?.Save(Settings);
        _hotkeyService?.Restart();
        _windowManager?.RefreshSettings(Settings);
        _trayService?.Refresh();
        ApplyTheme(Settings.ThemeOverride);
    }

    public void ShowMainWindow()
    {
        if (_mainWindow == null || !_mainWindow.IsLoaded)
            _mainWindow = new MainWindow(_windowManager!, ShowSettingsWindow);

        _mainWindow.ShowPanel();
    }

    public void ShowSettingsWindow()
    {
        if (_settingsWindow == null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = new SettingsWindow(Settings, SaveSettings);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }

        _settingsWindow.Owner = _mainWindow?.IsVisible == true ? _mainWindow : null;
        _settingsWindow.ShowDrawer();
    }

    private void ApplyTheme(string themeOverride)
    {
        var dark = themeOverride.Equals("Dark", StringComparison.OrdinalIgnoreCase) ||
                   (!themeOverride.Equals("Light", StringComparison.OrdinalIgnoreCase) && IsSystemDarkTheme());

        Resources["WindowBrush"] = dark ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(246, 30, 31, 34)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(246, 247, 248, 250));
        Resources["PanelBrush"] = dark ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(150, 45, 46, 51)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(210, 255, 255, 255));
        Resources["CardBrush"] = dark ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(190, 39, 40, 45)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(236, 255, 255, 255));
        Resources["SubtlePanelBrush"] = dark ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(90, 255, 255, 255)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(120, 255, 255, 255));
        Resources["TextBrush"] = dark ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 245, 247)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(32, 32, 36));
        Resources["MutedTextBrush"] = dark ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(184, 184, 190)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(92, 92, 100));
        Resources["BorderBrushSoft"] = dark ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(90, 255, 255, 255)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(90, 0, 0, 0));
        Resources["HoverBrush"] = dark ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(35, 255, 255, 255)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(22, 0, 0, 0));
        Resources["PressedBrush"] = dark ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(55, 255, 255, 255)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(36, 0, 0, 0));
        Resources["AccentBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212));
        Resources["AccentSoftBrush"] = dark ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(42, 0, 120, 212)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(30, 0, 120, 212));
        Resources["DangerBrush"] = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(196, 43, 28));
        Resources["ToggleOffBrush"] = dark ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(86, 86, 92)) : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(196, 196, 202));
    }

    private static bool IsSystemDarkTheme()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return Convert.ToInt32(key?.GetValue("AppsUseLightTheme", 1)) == 0;
        }
        catch
        {
            return false;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayService?.Dispose();
        _hotkeyService?.Dispose();
        _windowManager?.Dispose();
        _openEventRegistration?.Unregister(null);
        _openEvent?.Dispose();
        if (_ownsMutex)
            _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
