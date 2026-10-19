# Binlet

The Windows 11 Recycle Bin, one click away in the system tray.

[![Latest release](https://img.shields.io/github/v/release/LomakoDima/BinLet)](https://github.com/LomakoDima/BinLet/releases/latest)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

## Features

- Tray icon with separate empty and full states. The tooltip shows the item count across all drives, and the icon updates on its own.
- Opens the real Windows Recycle Bin. Binlet has no file browser or deletion logic of its own.
- Safe emptying: it always asks first (Cancel is the default), and Windows does the deleting.
- Optional start with Windows, per user, no admin rights.

## Install

Download [`Binlet-win-x64.exe`](https://github.com/LomakoDima/BinLet/releases/latest/download/Binlet-win-x64.exe) from the [latest release](https://github.com/LomakoDima/BinLet/releases/latest) and run it. It is self-contained, so no .NET install is needed.

Requires Windows 11 x64. If the icon is not visible, drag it out of the hidden-icons area of the taskbar.

The exe is not code-signed, so SmartScreen may warn on the first run. Choose **More info → Run anyway**, or unblock the file first:

```powershell
Unblock-File .\Binlet-win-x64.exe
```

## Usage

| Action | Result |
| --- | --- |
| Left-click the tray icon | Opens the Recycle Bin |
| Right-click the tray icon | Open Recycle Bin · Empty Recycle Bin · Start with Windows (checked when on) · Exit |

Only one instance runs at a time.

## Build from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
dotnet run --project BinLet/BinLet.csproj
```

Single-file release build (about 170 MB, because it bundles the .NET runtime):

```powershell
dotnet publish BinLet/BinLet.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The result is `BinLet/bin/Release/net10.0-windows10.0.22000.0/win-x64/publish/BinLet.exe`.

Pushing a `v*` tag builds and publishes a release (`.github/workflows/release.yml`). The icons in `BinLet/Assets` come from `tools/make-icons.ps1`.

## License

[MIT](LICENSE)
