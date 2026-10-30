# FlicKey for Windows

A tray app that fixes text typed in the wrong keyboard layout. It is a Windows
port of the core FlicKey gesture, written in C# (.NET 8, WinForms).

## Use

1. Run `FlicKey-windows-x64.exe`. A blue **FK** icon appears in the system tray.
2. Type something in the wrong language, for example `akuo` instead of `שלום`.
3. **Double-tap Shift.** FlicKey replaces what you just typed and switches the
   keyboard layout. Double-tap Shift again to cycle to the next layout (with two
   layouts that undoes the fix).
4. With text **selected** and nothing typed since the caret moved, double-tap
   Shift converts the selection instead.

Right-click the tray icon to disable FlicKey, start it with Windows, or exit.
You need at least two keyboard layouts installed (Windows Settings > Time &
language > Language & region).

## What is not in the Windows port

The macOS app has more than this. Not ported: automatic fixing while you type
(it relies on the macOS spell checker), per-app / per-site / per-chat layout
memory, sounds and haptics, and the Settings window.

## Limits

- Windows running as administrator ignore the hook unless FlicKey is also run as
  administrator.
- Console windows may not switch layout; text replacement still works.
- Conversion follows physical keys, so it works for any layout pair installed on
  the machine, not only Hebrew and English.

## Build

```sh
dotnet test windows/tests/FlicKey.Core.Tests
dotnet publish windows/src/FlicKey.Windows -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -o publish
```

CI (`.github/workflows/windows.yml`) builds it on a Windows runner and attaches
the exe to the release when a `win-v*` tag is pushed.
