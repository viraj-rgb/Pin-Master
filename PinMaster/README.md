# Pin Master

Pin Master is a lightweight Windows tray utility for pinning any normal desktop window so it stays always on top.

## Requirements

- Windows 10 version 1903 or newer, or Windows 11
- .NET 8 SDK or newer to build
- No administrator rights are required

## Build

```powershell
cd PinMaster
dotnet build -c Release
```

The app is a WPF Windows application and does not open a console window.
This workspace also includes a local SDK under `.dotnet`, so you can build with:

```powershell
.\.dotnet\dotnet.exe build -c Release
```

## Run

```powershell
dotnet run -c Release
```

After launch, Pin Master runs from the system tray. If the icon is not visible, open the taskbar hidden-icons flyout.

## Usage

- Left-click or double-click the tray icon to open the Pin Master panel.
- Toggle a listed window to pin or unpin it.
- Right-click the tray icon to open the live context menu and unpin pinned windows.
- Press the global hotkey `Win+Shift+P` to pin or unpin the active foreground window.
- Use **Unpin All** to clear every pinned window.

## Settings

The settings panel includes:

- Launch on Windows startup using `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- System, Light, or Dark theme selection

The default global hotkey is `Win+Shift+P`.

Settings and pinned window preferences are saved to:

```text
%AppData%\PinMaster\settings.json
```

On startup, Pin Master attempts to re-pin matching windows that are already open by process name and window title.

## Install

Publish a self-contained or framework-dependent release build:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false
```

Copy the publish folder anywhere under your user profile and run `PinMaster.exe`. Enable startup from the settings panel if desired.

## Notes

- Pinning uses `SetWindowPos` with `HWND_TOPMOST` and unpinning uses `HWND_NOTOPMOST`.
- Window discovery uses `EnumWindows` and filters invisible, minimized, untitled, tool, system shell, and Pin Master windows.
- The tray icon is quiet by design. It does not flash, blink, or show balloon notifications. Only the hotkey action shows a brief toast-style confirmation.
