# DroidEdge

Use your Android phone as a screen beside your Windows PC. Push the mouse past a monitor edge and it becomes a real
mouse cursor on the phone; the keyboard, clipboard and files follow. Push back to return. Nothing is mirrored and
nothing is installed on the phone: DroidEdge drives [scrcpy](https://github.com/Genymobile/scrcpy)'s server over adb
and shows up on the phone as a USB mouse and keyboard.

Optionally, a second window shows a virtual display on the phone with Android's desktop mode: a taskbar, and apps in
movable, resizable windows (Android 17, with the patched scrcpy described below).

## Features
- **Edge crossing** between a monitor edge and the phone, after pushing against the edge for a moment, so the cursor
  does not slip over by accident. Never while a mouse button is held.
- **Keyboard** goes to the phone while the mouse is there.
- **Mouse buttons:** right-click is Android Back, middle-click turns the phone screen off and returns the mouse.
  Both can be switched back to plain buttons.
- **Clipboard sync:** text copied on the phone lands in the Windows clipboard; the Windows clipboard is sent to the
  phone when you cross over.
- **File sending** to the phone's Download folder, by dragging files to the edge or from the tray menu.
- **Phone desktop window** showing a virtual display on the phone, with its own taskbar.
- **Panic hotkey** (Left Ctrl + Right Ctrl) that always returns the mouse.

## Requirements
- Windows 11 x64. Building needs the .NET 10 SDK; the published `DroidEdge.exe` is self-contained.
- [scrcpy 4.1](https://github.com/Genymobile/scrcpy/releases/tag/v4.1), unzipped. DroidEdge looks in
  `%LOCALAPPDATA%\Programs\scrcpy` unless `scrcpyDir` says otherwise.
- An Android phone with USB debugging enabled, connected by cable and unlocked.

Tested on Windows 11 with a Pixel 8 Pro on Android 16 and 17.

## Build and run
```powershell
.\publish.ps1
.\dist\DroidEdge.exe
```
`publish.ps1` runs the tests and publishes a single executable into `dist`. DroidEdge lives in the tray; the icon is
green when the phone is connected and blue while the mouse is on the phone.

## Using it
- Push the mouse against the **left edge of the primary monitor for 1 s** to cross (configurable). Push against the
  phone's opposite edge for 1 s to come back.
- **Right-click** is Android **Back** (`rightClickIsBack: false` for a normal secondary button).
- **Middle-click** turns the phone screen off and returns the mouse to the PC (`middleClickIsDone: false` for a normal
  middle button). It does not lock the phone; crossing back turns the screen on again.
- **Panic:** hold **Left Ctrl + Right Ctrl** to return instantly.
- `DroidEdge.exe --diag` (or tray → Run diagnostics) runs an end-to-end check and shows a report.

### Clipboard
Text copied on the phone lands in the Windows clipboard straight away. The Windows clipboard is sent to the phone when
you cross over. Clips that password managers mark as private (`ExcludeClipboardContentFromMonitorProcessing`) are never
sent. Text only; `syncClipboard: false` turns it off.

### Sending files
Files go to the phone's `Download` folder; name clashes get " (1)" instead of overwriting.
- Drag files to the phone edge and keep pushing with the button held: a drop zone appears. Drop onto it.
- Tray → **Send files to phone…**
- Or drop them onto the phone desktop window (scrcpy's own drop; APKs get installed).

Tiling window managers (e.g. GlazeWM) may try to tile the drop zone; add an ignore rule for the window titled
`DroidEdge drop zone`.

### Keyboard layouts
DroidEdge sends key presses as a USB keyboard would, so typing follows the phone's physical keyboard layouts, not the
PC's language. Set them once in Settings → System → Keyboard → Physical keyboard → **DroidEdge Keyboard**, and switch
language on the phone side.

## Phone desktop
A second window (`scrcpy.exe`) shows a virtual display created on the phone, with its own taskbar and launcher. Open
it from the tray (**Open phone desktop**), or on every connect with `openDesktopOnConnect`. Minimize or close it
freely: it has its own connection. Closing it removes the virtual display; apps that were open there move to the phone
screen.

Each behaviour below can be turned off in [the configuration](#configuration); the `desktop…` settings are read
each time the window opens.

**Keeping the phone awake** (`desktopKeepAwake`): while the window is open the phone is kept awake (scrcpy
`--stay-awake`, restored when the window closes), and its own screen is dark except while the mouse is on it.
**The phone stays unlocked the whole time the desktop is open**; the power button still locks it. With this off, the
phone sleeps and locks after its normal screen timeout, and the desktop stops responding until you unlock it.

**Typing** (`desktopKeyboard`): the window has its own USB keyboard (scrcpy `--keyboard=uhid`), listed on the phone as
**scrcpy**; set its layouts like DroidEdge's. scrcpy's `sdk` mode types through Android's US layout and drops
non-Latin characters such as Cyrillic.

**Mouse** (`desktopMouse`): the window has its own USB mouse too (scrcpy `--mouse=uhid`). Click into the window and it
captures the Windows cursor and sends relative movement, so apps that take over the pointer, such as Moonlight, show a
single pointer instead of a remote one trailing the Windows cursor. The phone cursor starts where it last was; if it
is on the phone screen, move it down onto the desktop. All buttons go through unchanged. With `sdk`, the window works
like a normal window instead: no capture, right-click is Back.
- Release the mouse with the **tilde key** (left of 1) or **Right Ctrl**. Right Ctrl is scrcpy's shortcut key here
  (`desktopShortcutMod`, scrcpy's `--shortcut-mod`), so Alt and Super reach the phone. scrcpy only accepts Ctrl, Alt
  or Super keys for this, so DroidEdge turns a plain tilde press into the first shortcut key while the window is
  active (`desktopTildeReleasesMouse`). That press never reaches the phone; Shift+tilde and other combinations do.
- scrcpy's other shortcuts use the shortcut key too, e.g. Right Ctrl+F for fullscreen.
- With the patched scrcpy client (`desktopPatchedClient`), the window also takes the keyboard while it holds the mouse:
  Ctrl, Alt, Win, Tab and Esc go to the window first, so other programs' global shortcuts (window managers, launchers)
  reach the phone instead of firing on Windows. Releasing the mouse gives them back.

**Closing** (`desktopKeepAppsOnClose`): closing the window moves its apps to the phone screen; with this off, they are
closed with the display.

**Size:** `desktopDisplay` sets the initial size and density in scrcpy's syntax, e.g. `"1920x1080"`,
`"1440x1800/240"`, `"/240"` (phone size, 240 dpi) or `""` (phone size and density); higher dpi means bigger UI. With
`desktopFlex` (default) the display resizes to match the window. Both are read each time the window opens.

### Patched scrcpy
`scrcpy/droidedge.patch` holds DroidEdge's changes to scrcpy v4.1. DroidEdge uses the patched builds for the desktop
window when they are in `dist` (unless `desktopPatchedServer` or `desktopPatchedClient` is off), and stock scrcpy
otherwise; the log says which were used.
- **Server** (`dist\scrcpy-server-desktop`): with stock scrcpy, the taskbar and app list on the virtual display open
  nothing, because Android only lets a secondary display host desktop windows if it can switch between mirroring and
  extending. On Android 17 the patched server creates such a display, so apps open as desktop windows. On older
  Android, or if Android rejects it, the server behaves like stock scrcpy.
- **Client** (`dist\scrcpy-desktop\scrcpy.exe`): takes the keyboard while it holds the mouse (see above).

One-time phone setup for desktop windows on Android 17:
- Keep the phone screen in the display arrangement:
  `adb shell settings put secure include_default_display_in_topology 1`. Without it Android drops the phone screen
  from the arrangement while the desktop is open, and the phone cursor lands on the desktop instead.
- Open the desktop, then in Settings → Connected devices → External display drag it **below** the phone. Android
  first places it above the phone, where the cursor would slide onto it; the position is remembered afterwards.

### Building the patched scrcpy
Get scrcpy v4.1 with the patch applied:
```powershell
git clone --branch v4.1 https://github.com/Genymobile/scrcpy.git C:\tools\scrcpy
git -C C:\tools\scrcpy am <this repo>\scrcpy\droidedge.patch
```
- **Server:** needs JDK 17 and the Android SDK packages `platforms;android-36` and `build-tools;36.0.0`. Run
  `.\scrcpy\build-server.ps1 -Jdk <jdk folder> -Sdk <android sdk folder>`.
- **Client:** needs [MSYS2](https://www.msys2.org/) with the MinGW64 packages listed at the top of
  `scrcpy\build-client.ps1`. Run `.\scrcpy\build-client.ps1 -Msys <msys2 folder>`. It copies the ~100 DLLs the
  client needs from MSYS2 next to it (about 140 MB), so later MSYS2 updates cannot break it.

Both scripts default to `C:\tools\scrcpy` for the checkout (`-ScrcpySource` to change). The builds must match the
installed scrcpy's version (4.1). After updating scrcpy, apply the patch to the new version and rebuild, or delete
`dist\scrcpy-server-desktop` and `dist\scrcpy-desktop` to fall back to stock scrcpy.

## Configuration
`droidedge.json` next to the executable; tray → Open config creates it. Every field is optional.

| Field | Default | Meaning |
|---|---|---|
| `scrcpyDir` | `%LOCALAPPDATA%\Programs\scrcpy` | Unzipped scrcpy 4.1 release (scrcpy.exe, adb.exe, scrcpy-server). |
| `deviceSerial` | first phone found | adb serial of the phone to use when several are connected. |
| `edge` | `left` | Monitor edge that leads to the phone: `left`, `right`, `top`, `bottom`. |
| `monitor` | `primary` | `primary` or a zero-based monitor index. |
| `enterDwellMs` | `1000` | How long to push against the edge before crossing to the phone. |
| `exitDwellMs` | `1000` | How long to push against the phone's edge before coming back. |
| `sensitivity` | `1.0` | Multiplier for mouse movement on the phone, up to 20. |
| `forwardKeyboard` | `true` | Send the keyboard to the phone while the mouse is there. |
| `rightClickIsBack` | `true` | Right-click is Android Back. |
| `middleClickIsDone` | `true` | Middle-click turns the phone screen off and returns the mouse. |
| `syncClipboard` | `true` | Sync the clipboard as described above. |
| `openDesktopOnConnect` | `true` | Open the phone desktop window whenever the phone connects. |
| `desktopDisplay` | `"1920x1080"` | Phone desktop size and density, in scrcpy's `--new-display` syntax. |
| `desktopFlex` | `true` | Resize the phone desktop to follow its window. |
| `desktopKeyboard` | `uhid` | The window's keyboard (scrcpy `--keyboard`): `uhid`, `sdk` or `disabled`. |
| `desktopMouse` | `uhid` | The window's mouse (scrcpy `--mouse`): `uhid` (captured), `sdk` (plain window) or `disabled`. |
| `desktopShortcutMod` | `rctrl` | scrcpy's shortcut key (`--shortcut-mod`), which also releases the mouse: comma-separated `lctrl`, `rctrl`, `lalt`, `ralt`, `lsuper`, `rsuper`. |
| `desktopTildeReleasesMouse` | `true` | The tilde key taps the first shortcut key while the window is active (with `desktopMouse: uhid`). |
| `desktopKeepAwake` | `true` | Keep the phone awake and unlocked while the window is open, its screen dark unless the mouse is on it. |
| `desktopKeepAppsOnClose` | `true` | Closing the window moves its apps to the phone screen instead of closing them. |
| `desktopPatchedServer` | `true` | Use the patched scrcpy server when it is built (desktop windows on Android 17). |
| `desktopPatchedClient` | `true` | Use the patched scrcpy client when it is built (keyboard grab while the mouse is captured). |
| `panicHotkey` | `["LControl", "RControl"]` | Keys held together to return the mouse: `LControl`, `RControl`, `LShift`, `RShift`, `LAlt`, `RAlt`, `LWin`, `RWin`, `Escape`, `Pause`, `ScrollLock`, `F12`. |
| `logLevel` | `Info` | `Debug`, `Info`, `Warning` or `Error`. |

Logs go to `droidedge.log` next to the executable (tray → Open log).

## Known limitations
- Windows only, one phone at a time, and tested only over USB.
- On Android 17, the phone screen and the phone desktop share one cursor. If it ends up on the desktop display, the
  next crossing can land there too; moving it up brings it back to the phone screen.
- Apps that take over the pointer (such as Moonlight) work poorly through edge crossing, because DroidEdge cannot see
  where the phone cursor really is. Use them in the phone desktop window instead.

## How it works
DroidEdge starts scrcpy's server on the phone over adb, with video and audio off, and talks to it over scrcpy's control
protocol. The server creates a virtual USB mouse and keyboard on the phone (UHID), and DroidEdge sends them HID reports.
On Windows, low-level mouse and keyboard hooks decide whether input belongs to the PC or the phone; while it belongs to
the phone, the hooks block it from Windows and DroidEdge converts raw mouse movement and key presses into HID reports.

## License
Apache License 2.0; see [LICENSE](LICENSE). DroidEdge includes code derived from
[scrcpy](https://github.com/Genymobile/scrcpy) (Apache License 2.0); see [NOTICE](NOTICE).
