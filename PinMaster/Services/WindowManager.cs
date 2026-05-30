using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PinMaster.Helpers;
using PinMaster.Models;

namespace PinMaster.Services;

public sealed class WindowManager : IDisposable
{
    private readonly Dictionary<nint, PinnedWindow> _pinned = new();
    private readonly DispatcherTimer _cleanupTimer;
    private readonly PersistenceService _persistence;
    private AppSettings _settings;

    public event EventHandler? PinnedWindowsChanged;

    public WindowManager(AppSettings settings, PersistenceService persistence)
    {
        _settings = settings;
        _persistence = persistence;
        _cleanupTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _cleanupTimer.Tick += (_, _) => CleanupClosedWindows();
        _cleanupTimer.Start();
    }

    public IReadOnlyCollection<PinnedWindow> PinnedWindows => _pinned.Values.ToList();

    public void RefreshSettings(AppSettings settings)
    {
        _settings = settings;
    }

    public List<WindowInfo> EnumerateWindows()
    {
        var windows = new List<WindowInfo>();
        var shellWindow = WinApiInterop.GetShellWindow();
        var currentProcessId = Environment.ProcessId;

        WinApiInterop.EnumWindows((hWnd, _) =>
        {
            if (hWnd == shellWindow || !IsCandidateWindow(hWnd))
                return true;

            WinApiInterop.GetWindowThreadProcessId(hWnd, out var processId);
            if (processId == currentProcessId)
                return true;

            var title = GetWindowTitle(hWnd);
            if (string.IsNullOrWhiteSpace(title))
                return true;

            if (!TryGetProcessDetails(processId, out var processName, out var processPath))
                return true;

            if (IsSystemWindow(processName, title))
                return true;

            windows.Add(new WindowInfo
            {
                Handle = hWnd,
                Title = title.Trim(),
                ProcessName = processName,
                ProcessPath = processPath,
                Icon = IconHelper.GetIcon(processPath),
                IsPinned = _pinned.ContainsKey(hWnd)
            });
            return true;
        }, nint.Zero);

        return windows.OrderByDescending(w => w.IsPinned).ThenBy(w => w.ProcessName).ThenBy(w => w.Title).ToList();
    }

    public bool IsPinned(nint hWnd) => _pinned.ContainsKey(hWnd);

    public bool TogglePin(nint hWnd)
    {
        if (IsPinned(hWnd))
        {
            UnpinWindow(hWnd);
            return false;
        }

        PinWindow(hWnd);
        return true;
    }

    public bool PinWindow(nint hWnd)
    {
        if (!WinApiInterop.IsWindow(hWnd) || !IsCandidateWindow(hWnd))
            return false;

        if (WinApiInterop.IsIconic(hWnd))
            WinApiInterop.ShowWindowAsync(hWnd, WinApiInterop.SW_RESTORE);

        if (!WinApiInterop.SetWindowPos(hWnd, WinApiInterop.HWND_TOPMOST, 0, 0, 0, 0, WinApiInterop.SWP_NOMOVE | WinApiInterop.SWP_NOSIZE | WinApiInterop.SWP_SHOWWINDOW))
            return false;

        WinApiInterop.GetWindowThreadProcessId(hWnd, out var processId);
        var title = GetWindowTitle(hWnd);
        if (!TryGetProcessDetails(processId, out var processName, out var processPath))
            processName = "Unknown";

        _pinned[hWnd] = new PinnedWindow
        {
            Handle = hWnd,
            Title = title,
            ProcessName = processName,
            ProcessPath = processPath,
            PinnedAt = DateTime.UtcNow
        };
        PersistAndNotify();
        return true;
    }

    public void UnpinWindow(nint hWnd)
    {
        if (WinApiInterop.IsWindow(hWnd))
            WinApiInterop.SetWindowPos(hWnd, WinApiInterop.HWND_NOTOPMOST, 0, 0, 0, 0, WinApiInterop.SWP_NOMOVE | WinApiInterop.SWP_NOSIZE | WinApiInterop.SWP_NOACTIVATE);

        _pinned.Remove(hWnd);
        PersistAndNotify();
    }

    public void UnpinAll()
    {
        foreach (var handle in _pinned.Keys.ToList())
            UnpinWindow(handle);
    }

    public void RestorePersistedPins()
    {
        if (_settings.PinnedWindows.Count == 0)
            return;

        var open = EnumerateWindows();
        foreach (var saved in _settings.PinnedWindows)
        {
            var match = open.FirstOrDefault(w =>
                w.ProcessName.Equals(saved.ProcessName, StringComparison.OrdinalIgnoreCase) &&
                w.Title.Equals(saved.Title, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                PinWindow(match.Handle);
        }
    }

    public WindowInfo? GetForegroundWindowInfo()
    {
        var hWnd = WinApiInterop.GetForegroundWindow();
        if (hWnd == nint.Zero || !IsCandidateWindow(hWnd))
            return null;

        return EnumerateWindows().FirstOrDefault(w => w.Handle == hWnd);
    }

    private void CleanupClosedWindows()
    {
        var changed = false;
        foreach (var handle in _pinned.Keys.ToList())
        {
            if (!WinApiInterop.IsWindow(handle))
            {
                _pinned.Remove(handle);
                changed = true;
            }
        }

        if (changed)
            PersistAndNotify();
    }

    private void PersistAndNotify()
    {
        _persistence.SavePinnedWindows(_pinned.Values, _settings);
        PinnedWindowsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool IsCandidateWindow(nint hWnd)
    {
        if (!WinApiInterop.IsWindowVisible(hWnd))
            return false;

        var exStyle = WinApiInterop.GetWindowLongPtr(hWnd, WinApiInterop.GWL_EXSTYLE).ToInt64();
        if ((exStyle & WinApiInterop.WS_EX_TOOLWINDOW) != 0)
            return false;

        return WinApiInterop.GetWindowTextLength(hWnd) > 0;
    }

    private static bool IsSystemWindow(string processName, string title)
    {
        string[] blocked =
        {
            "ShellExperienceHost", "SearchHost", "StartMenuExperienceHost", "TextInputHost",
            "SystemSettings", "PinMaster"
        };
        if (blocked.Contains(processName, StringComparer.OrdinalIgnoreCase))
            return true;

        return processName.Equals("ApplicationFrameHost", StringComparison.OrdinalIgnoreCase) &&
               title.Equals("Settings", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetProcessDetails(uint processId, out string processName, out string processPath)
    {
        processName = string.Empty;
        processPath = string.Empty;

        try
        {
            using var process = Process.GetProcessById((int)processId);
            processName = process.ProcessName;

            try
            {
                processPath = process.MainModule?.FileName ?? string.Empty;
            }
            catch
            {
                processPath = string.Empty;
            }

            return !string.IsNullOrWhiteSpace(processName);
        }
        catch
        {
            return false;
        }
    }

    private static string GetWindowTitle(nint hWnd)
    {
        var length = Math.Max(WinApiInterop.GetWindowTextLength(hWnd), 0);
        var builder = new StringBuilder(length + 1);
        WinApiInterop.GetWindowText(hWnd, builder, builder.Capacity);
        return builder.ToString();
    }

    public void Dispose()
    {
        _cleanupTimer.Stop();
    }

    private static class IconHelper
    {
        public static System.Windows.Media.ImageSource? GetIcon(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            var info = new WinApiInterop.SHFILEINFO();
            var result = WinApiInterop.SHGetFileInfo(path, WinApiInterop.FILE_ATTRIBUTE_NORMAL, ref info, (uint)Marshal.SizeOf<WinApiInterop.SHFILEINFO>(), WinApiInterop.SHGFI_ICON | WinApiInterop.SHGFI_SMALLICON);
            if (result == nint.Zero || info.hIcon == nint.Zero)
                return null;

            try
            {
                var source = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(16, 16));
                source.Freeze();
                return source;
            }
            finally
            {
                WinApiInterop.DestroyIcon(info.hIcon);
            }
        }
    }
}
