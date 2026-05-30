# Pin Master

Pin Master is a lightweight Windows tray utility that lets you pin desktop windows so they stay always on top. It is designed to feel native on Windows 10/11, run quietly in the background, and stay easy to reopen from the system tray.

Created by [github.com/viraj-rgb](https://github.com/viraj-rgb).

## Features

- Pin or unpin visible and minimized desktop windows.
- Reopen the floating Pin Master panel from the system tray.
- Right-click the tray icon for pinned-window controls, settings, and exit.
- Use `Win+Shift+P` to pin or unpin the active foreground window.
- Unpin everything at once with **Unpin All**.
- Save settings and restore matching pinned windows on startup.
- Optional launch on Windows startup.
- Light, Dark, and System theme modes.
- Quiet tray behavior: no flashing, blinking, or balloon notifications.

## Requirements

- Windows 10 version 1903 or newer, or Windows 11
- .NET 8 SDK or newer to build
- No administrator rights are required

## Build

```powershell
cd PinMaster
dotnet build -c Release
```

If you installed a local SDK into the project folder, you can also build with:

```powershell
.\.dotnet\dotnet.exe build -c Release
```

## Run

```powershell
dotnet run -c Release
```

Double-clicking the app opens the Pin Master panel. Closing or dismissing the panel keeps Pin Master running in the background. Reopen it from the system tray icon; if the icon is not visible, check the taskbar hidden-icons flyout.

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

When launch on startup is enabled, Pin Master starts quietly in the tray using the `--startup` argument. Normal double-click launch opens the panel.

The default global hotkey is `Win+Shift+P`.

Settings and pinned window preferences are saved to:

```text
%AppData%\PinMaster\settings.json
```

On startup, Pin Master attempts to re-pin matching windows that are already open by process name and window title.

## Publish A Production EXE

Create a self-contained single-file Windows x64 executable:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o .\publish\desktop
```

The final executable is:

```text
PinMaster\publish\desktop\PinMaster.exe
```

Copy it anywhere under the user's profile, such as the Desktop, and run it normally. No administrator rights are needed.

## Notes

- Pinning uses `SetWindowPos` with `HWND_TOPMOST` and unpinning uses `HWND_NOTOPMOST`.
- Minimized windows are listed too; Pin Master restores a minimized target before applying always-on-top.
- Window discovery uses `EnumWindows` and filters invisible, untitled, tool, system shell, Windows Settings host, and Pin Master windows.
- The tray icon changes color when windows are pinned.
- The tray icon is quiet by design. It does not flash, blink, or show balloon notifications. Only the hotkey action shows a brief toast-style confirmation.
