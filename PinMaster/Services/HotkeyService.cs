using System.Windows;
using System.Windows.Interop;
using PinMaster.Helpers;
using Forms = System.Windows.Forms;

namespace PinMaster.Services;

public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0x504D;
    private readonly WindowManager _windowManager;
    private readonly PersistenceService _persistence;
    private AppSettings _settings;
    private HotkeyMessageWindow? _messageWindow;

    public HotkeyService(WindowManager windowManager, AppSettings settings, PersistenceService persistence)
    {
        _windowManager = windowManager;
        _settings = settings;
        _persistence = persistence;
    }

    public void Start()
    {
        _messageWindow ??= new HotkeyMessageWindow(HandleHotkey);
        Register();
    }

    public void Restart()
    {
        if (_messageWindow == null)
            return;

        WinApiInterop.UnregisterHotKey(_messageWindow.Handle, HotkeyId);
        Register();
    }

    private void Register()
    {
        if (_messageWindow == null)
            return;

        var gesture = HotkeyGesture.Parse(_settings.HotkeyGesture);
        if (!WinApiInterop.RegisterHotKey(_messageWindow.Handle, HotkeyId, gesture.Modifiers | WinApiInterop.MOD_NOREPEAT, gesture.VirtualKey))
        {
            var fallback = HotkeyGesture.Default;
            WinApiInterop.RegisterHotKey(_messageWindow.Handle, HotkeyId, fallback.Modifiers | WinApiInterop.MOD_NOREPEAT, fallback.VirtualKey);
        }
    }

    private void HandleHotkey()
    {
        var foreground = _windowManager.GetForegroundWindowInfo();
        if (foreground == null)
            return;

        var pinned = _windowManager.TogglePin(foreground.Handle);
        ToastWindow.Show($"{foreground.DisplayTitle} - {(pinned ? "Pinned" : "Unpinned")} {(pinned ? "\u2713" : string.Empty)}");
        _persistence.Save(_settings);
    }

    public void Dispose()
    {
        if (_messageWindow != null)
        {
            WinApiInterop.UnregisterHotKey(_messageWindow.Handle, HotkeyId);
            _messageWindow.Dispose();
        }
    }

    private sealed class HotkeyMessageWindow : Forms.NativeWindow, IDisposable
    {
        private readonly Action _callback;

        public HotkeyMessageWindow(Action callback)
        {
            _callback = callback;
            CreateHandle(new Forms.CreateParams { Caption = "PinMasterHotkeySink" });
        }

        protected override void WndProc(ref Forms.Message m)
        {
            if (m.Msg == WinApiInterop.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
                _callback();

            base.WndProc(ref m);
        }

        public void Dispose() => DestroyHandle();
    }
}

internal readonly record struct HotkeyGesture(uint Modifiers, uint VirtualKey)
{
    public static HotkeyGesture Default => new(WinApiInterop.MOD_WIN | WinApiInterop.MOD_SHIFT, (uint)System.Windows.Input.KeyInterop.VirtualKeyFromKey(System.Windows.Input.Key.P));

    public static HotkeyGesture Parse(string value)
    {
        var modifiers = 0u;
        var keyToken = "P";
        foreach (var part in value.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (part.ToUpperInvariant())
            {
                case "WIN":
                case "WINDOWS":
                    modifiers |= WinApiInterop.MOD_WIN;
                    break;
                case "SHIFT":
                    modifiers |= WinApiInterop.MOD_SHIFT;
                    break;
                case "CTRL":
                case "CONTROL":
                    modifiers |= WinApiInterop.MOD_CONTROL;
                    break;
                case "ALT":
                    modifiers |= WinApiInterop.MOD_ALT;
                    break;
                default:
                    keyToken = part;
                    break;
            }
        }

        if (!Enum.TryParse<System.Windows.Input.Key>(keyToken, true, out var key))
            return Default;

        var virtualKey = System.Windows.Input.KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey == 0)
            return Default;

        return new HotkeyGesture(modifiers == 0 ? WinApiInterop.MOD_WIN | WinApiInterop.MOD_SHIFT : modifiers, (uint)virtualKey);
    }
}

public sealed class ToastWindow : Window
{
    private readonly System.Windows.Threading.DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1.6) };

    private ToastWindow(string message)
    {
        Width = 280;
        Height = 64;
        ShowInTaskbar = false;
        Topmost = true;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = System.Windows.Media.Brushes.Transparent;
        Content = new System.Windows.Controls.Border
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(235, 36, 36, 40)),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 10, 16, 10),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 22, ShadowDepth = 4, Opacity = 0.28 },
            Child = new System.Windows.Controls.TextBlock
            {
                Text = message,
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 14,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        Loaded += (_, _) =>
        {
            var work = SystemParameters.WorkArea;
            Left = work.Right - Width - 24;
            Top = work.Bottom - Height - 24;
            _timer.Tick += (_, _) => Close();
            _timer.Start();
        };
        Closed += (_, _) => _timer.Stop();
    }

    public static void Show(string message)
    {
        var toast = new ToastWindow(message);
        toast.Show();
    }
}
