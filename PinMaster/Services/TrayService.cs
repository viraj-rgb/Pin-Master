using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace PinMaster.Services;

public sealed class TrayService : IDisposable
{
    private readonly WindowManager _windowManager;
    private readonly Action _openMain;
    private readonly Action _openSettings;
    private readonly Action _exit;
    private NotifyIcon? _notifyIcon;

    public TrayService(WindowManager windowManager, Action openMain, Action openSettings, Action exit)
    {
        _windowManager = windowManager;
        _openMain = openMain;
        _openSettings = openSettings;
        _exit = exit;
    }

    public void Start()
    {
        _notifyIcon = new NotifyIcon
        {
            Text = "Pin Master",
            Visible = true,
            Icon = LoadIcon(false),
            ContextMenuStrip = BuildMenu()
        };

        _notifyIcon.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
                _openMain();
        };
        _notifyIcon.DoubleClick += (_, _) => _openMain();
    }

    public void Refresh()
    {
        if (_notifyIcon == null)
            return;

        var oldIcon = _notifyIcon.Icon;
        var oldMenu = _notifyIcon.ContextMenuStrip;
        _notifyIcon.Icon = LoadIcon(_windowManager.PinnedWindows.Count > 0);
        _notifyIcon.ContextMenuStrip = BuildMenu();
        oldIcon?.Dispose();
        oldMenu?.Dispose();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open Pin Master", null, (_, _) => _openMain());
        menu.Items.Add(new ToolStripSeparator());

        var pinned = _windowManager.PinnedWindows.ToList();
        if (pinned.Count == 0)
        {
            var empty = new ToolStripMenuItem("No pinned windows") { Enabled = false };
            menu.Items.Add(empty);
        }
        else
        {
            foreach (var window in pinned)
            {
                var item = new ToolStripMenuItem(Trim(window.Title));
                item.DropDownItems.Add("Unpin", null, (_, _) => _windowManager.UnpinWindow(window.Handle));
                menu.Items.Add(item);
            }
        }

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Settings", null, (_, _) => _openSettings());
        menu.Items.Add("Exit", null, (_, _) => _exit());
        return menu;
    }

    private static string Trim(string value) => value.Length <= 48 ? value : value[..45] + "...";

    private static Icon LoadIcon(bool active)
    {
        var baseDir = AppContext.BaseDirectory;
        var path = Path.Combine(baseDir, "Assets", active ? "pin_active.ico" : "pin_default.ico");
        if (File.Exists(path))
            return new Icon(path);

        using var stream = System.Windows.Application.GetResourceStream(new Uri($"pack://application:,,,/Assets/{(active ? "pin_active.ico" : "pin_default.ico")}"))?.Stream;
        return stream != null ? (Icon)new Icon(stream).Clone() : (Icon)SystemIcons.Application.Clone();
    }

    public void Dispose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Icon?.Dispose();
            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}
