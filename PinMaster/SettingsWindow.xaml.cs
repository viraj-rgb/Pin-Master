using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using PinMaster.Helpers;
using PinMaster.Services;

namespace PinMaster;

public partial class SettingsWindow : Window
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "PinMaster";
    private readonly AppSettings _settings;
    private readonly Action _save;
    private bool _loading = true;

    public SettingsWindow(AppSettings settings, Action save)
    {
        InitializeComponent();
        _settings = settings;
        _save = save;
        StartupToggle.IsChecked = IsStartupRegistered();
        _settings.LaunchAtStartup = StartupToggle.IsChecked == true;
        ThemeCombo.SelectedIndex = _settings.ThemeOverride switch
        {
            "Light" => 1,
            "Dark" => 2,
            _ => 0
        };
        _loading = false;
    }

    private void StartupToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading)
            return;

        _settings.LaunchAtStartup = StartupToggle.IsChecked == true;
        SetStartup(_settings.LaunchAtStartup);
        _save();
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ThemeCombo.SelectedItem is not ComboBoxItem item)
            return;

        _settings.ThemeOverride = item.Content?.ToString() ?? "System";
        _save();
    }

    private static bool IsStartupRegistered()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return !string.IsNullOrWhiteSpace(key?.GetValue(RunValueName)?.ToString());
    }

    private static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);

        if (enabled)
        {
            var exePath = GetExecutablePath();
            key.SetValue(RunValueName, $"\"{exePath}\" --startup");
        }
        else
        {
            key.DeleteValue(RunValueName, false);
        }
    }

    private static string GetExecutablePath()
    {
        var path = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            return path;

        return Path.Combine(AppContext.BaseDirectory, "PinMaster.exe");
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    public void ShowDrawer()
    {
        Topmost = true;
        if (!IsVisible)
            Show();

        Activate();
        Topmost = true;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var work = SystemParameters.WorkArea;
        var targetLeft = work.Right - Width - 18;
        Top = work.Top + (work.Height - Height) / 2;
        Left = work.Right + 12;

        var animation = new DoubleAnimation(targetLeft, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        BeginAnimation(LeftProperty, animation);
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var backdrop = WinApiInterop.DWMSBT_TRANSIENTWINDOW;
        WinApiInterop.DwmSetWindowAttribute(hwnd, WinApiInterop.DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
        Topmost = true;
    }
}
