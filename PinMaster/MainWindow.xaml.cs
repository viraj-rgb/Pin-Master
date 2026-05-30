using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Threading;
using PinMaster.Helpers;
using PinMaster.Models;
using PinMaster.Services;

namespace PinMaster;

public partial class MainWindow : Window
{
    private readonly WindowManager _windowManager;
    private readonly Action _showSettings;
    private readonly ObservableCollection<WindowInfo> _windows = new();
    private readonly DispatcherTimer _outsideClickTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private List<WindowInfo> _allWindows = new();
    private DateTime _shownAt = DateTime.MinValue;

    public MainWindow(WindowManager windowManager, Action showSettings)
    {
        InitializeComponent();
        _windowManager = windowManager;
        _showSettings = showSettings;
        WindowList.ItemsSource = _windows;
        _outsideClickTimer.Tick += (_, _) => HideIfAnotherWindowIsActive();
        _windowManager.PinnedWindowsChanged += (_, _) => Dispatcher.Invoke(RefreshWindows);
    }

    public void ShowPanel()
    {
        Topmost = true;
        if (!IsVisible)
        {
            RefreshWindows();
            var area = SystemParameters.WorkArea;
            Left = area.Left + (area.Width - Width) / 2;
            Top = area.Top + (area.Height - Height) / 2;
            Show();
        }
        else
        {
            RefreshWindows();
        }

        Activate();
        Topmost = true;
        _shownAt = DateTime.UtcNow;
        _outsideClickTimer.Start();
        SearchBox.Focus();
    }

    private void RefreshWindows()
    {
        _allWindows = _windowManager.EnumerateWindows();
        ApplyFilter();
        StatusText.Text = $"{_windowManager.PinnedWindows.Count} pinned";
    }

    private void ApplyFilter()
    {
        var query = SearchBox.Text.Trim();
        var filtered = string.IsNullOrEmpty(query)
            ? _allWindows
            : _allWindows.Where(w => w.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                     w.ProcessName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        _windows.Clear();
        foreach (var window in filtered)
            _windows.Add(window);

        EmptyState.Visibility = _windows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PinToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton { DataContext: WindowInfo info })
        {
            _windowManager.TogglePin(info.Handle);
            RefreshWindows();
        }
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => ApplyFilter();

    private void RefreshButton_Click(object sender, RoutedEventArgs e) => RefreshWindows();

    private void SettingsButton_Click(object sender, RoutedEventArgs e) => _showSettings();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();

    private void UnpinAllButton_Click(object sender, RoutedEventArgs e)
    {
        _windowManager.UnpinAll();
        RefreshWindows();
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        HideIfAnotherWindowIsActive();
    }

    private void Window_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
            _outsideClickTimer.Start();
        else
            _outsideClickTimer.Stop();
    }

    private void HideIfAnotherWindowIsActive()
    {
        if (!IsVisible || DateTime.UtcNow - _shownAt < TimeSpan.FromMilliseconds(250))
            return;

        if (OwnedWindows.Cast<Window>().Any(w => w.IsVisible && w.IsActive))
            return;

        var foreground = WinApiInterop.GetForegroundWindow();
        var ownHandle = new WindowInteropHelper(this).Handle;
        var ownerChildActive = OwnedWindows.Cast<Window>()
            .Where(w => w.IsVisible)
            .Select(w => new WindowInteropHelper(w).Handle)
            .Any(handle => handle == foreground);

        if (foreground != nint.Zero && foreground != ownHandle && !ownerChildActive)
            Hide();
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var backdrop = WinApiInterop.DWMSBT_TRANSIENTWINDOW;
        WinApiInterop.DwmSetWindowAttribute(hwnd, WinApiInterop.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
        Topmost = true;
    }
}
