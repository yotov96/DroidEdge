# PhoneEdge

Move your mouse past a monitor edge and it appears on your USB-connected Android phone as a real cursor. Keyboard follows. No app on the phone: PhoneEdge drives scrcpy 4.1's server over adb.

## Requirements
- Windows 11, scrcpy 4.1 in `C:\Users\<you>\AppData\Local\Programs\scrcpy` (or set `scrcpyDir` in `phoneedge.json`)
- Phone with USB debugging enabled, connected by cable, unlocked

## Run
- `.\publish.ps1` then start `dist\PhoneEdge.exe`; it lives in the tray.
- Push the mouse against the **left edge of the primary monitor for 1 s** to cross. Push against the phone's right edge for 1 s to come back.
- Right-click is Android **Back** (set `rightClickIsBack` to `false` to send it as a normal secondary button instead).
- Middle-click (wheel click) is **"I'm done"**: it turns the phone screen off and returns the mouse to the PC (set `middleClickIsDone` to `false` for a normal middle button). It does not lock the phone: without the phone desktop open, the phone locks after its normal screen timeout. Crossing back turns the screen on again.
- Panic: hold **Left Ctrl + Right Ctrl** to return instantly.
- Crossing never happens while a mouse button is held.
- `PhoneEdge.exe --diag` runs an end-to-end check and shows a report.

## Clipboard
Text copied on the phone lands in the Windows clipboard straight away. The Windows clipboard is sent to the phone when you cross over. Clips marked private by password managers (`ExcludeClipboardContentFromMonitorProcessing`) are never sent. Text only; set `syncClipboard` to `false` to turn it off.

## Sending files
Files go to the phone's `Download` folder; name clashes get " (1)" instead of overwriting.
- Drag files to the phone edge and keep pushing with the button held: a drop zone appears. Drop onto it.
- Tray → **Send files to phone…**
- Or drop them onto the phone desktop window (scrcpy's own drop; APKs get installed).

GlazeWM may try to tile the drop zone; if so, add an ignore rule for the window titled `PhoneEdge drop zone`.

## Phone desktop
A second window (`scrcpy.exe`) shows a virtual display created on the phone, with its own taskbar and launcher. It opens when the phone connects (`openDesktopOnConnect`). Minimize or close it freely: it has its own connection, and tray → **Open phone desktop** reopens it. Closing it removes the virtual display; apps that were open there move to the phone screen.

While the desktop window is open the phone is kept awake (scrcpy `--stay-awake`, restored when the window closes), and its own screen is dark except while the mouse is on it. **The phone stays unlocked the whole time the desktop is open**; the physical power button still locks it. Clicking or touching the dark screen does not light it: Android considers it on, so input goes to the app underneath. Cross the mouse over to light it.

Typing follows the phone's keyboard layouts, not the PC's language: both the desktop window (scrcpy `--keyboard=uhid`) and PhoneEdge send key presses as a USB keyboard would. Set the layouts once in Settings → System → Keyboard → Physical keyboard, for **PhoneEdge Keyboard** (mouse on the phone screen) and **scrcpy** (desktop window), and switch language on the phone side. scrcpy's other keyboard modes type through Android's US layout and drop non-Latin characters such as Cyrillic.

The window's mouse is a USB mouse too (scrcpy `--mouse=uhid`): click into the window and it captures the Windows cursor, sending relative movement to the phone. Apps that take over the pointer, such as Moonlight, then show a single pointer instead of a remote one trailing the Windows cursor. Movement is relative, so the phone cursor starts where it last was; if it is on the phone screen, move it down onto the desktop first. All buttons go through unchanged (right-click is a right-click, not Back).
- Release the mouse with the **tilde key** (left of 1) or **Right Ctrl**. Right Ctrl is scrcpy's shortcut key here (`--shortcut-mod=rctrl`), so Alt and Super reach the phone. scrcpy only accepts Ctrl, Alt or Super keys for this, so PhoneEdge turns a plain tilde press into Right Ctrl while the window is active and the mouse is on the PC. That press never reaches the phone; Shift+tilde and other combinations do. If a phone layout puts a letter on that key, its unshifted form cannot be typed in the desktop window.
- scrcpy's other shortcuts use Right Ctrl too, e.g. Right Ctrl+F for fullscreen.
- With the patched client (below), the window also grabs the keyboard while it holds the mouse: Ctrl, Alt, Win, Tab and Esc go to the window first, so other programs' global shortcuts (GlazeWM, PowerToys Run) reach the phone instead of firing on Windows. Releasing the mouse gives them back. PhoneEdge's panic hotkey does not work while the window holds the mouse.

- `desktopDisplay`: initial size and density in scrcpy's syntax, e.g. `"1920x1080"`, `"1440x1800/240"`, `"/240"` (phone size, 240 dpi) or `""` (phone size and density). Higher dpi means bigger UI.
- `desktopFlex` (default `true`): the virtual display keeps resizing to match the window, so it fits whatever size your window manager gives it.
- Both are re-read each time the window opens; close it and reopen from the tray to apply changes.

### Patched scrcpy
`scrcpy/phoneedge.patch` holds PhoneEdge's changes to scrcpy v4.1. PhoneEdge uses the patched builds for the desktop window whenever they are in `dist`, and the stock ones otherwise; the log says which were used.
- **Server** (`dist\scrcpy-server-desktop`): with stock scrcpy, clicking the taskbar or app list on the virtual display opens nothing. Android only lets a secondary display host desktop windows if the display can switch between mirroring and extending, and scrcpy's virtual display can't. The patched server creates one that can, on Android 17 and later, so apps open as desktop windows (move, resize, minimize, maximize, close). On Android 16 and older, or if Android rejects the new flag, it behaves like stock scrcpy.
- **Client** (`dist\scrcpy-desktop\scrcpy.exe`): grabs the keyboard while it holds the mouse (see above).

### Desktop windows (Android 17)
- One-time phone setup:
  - Keep the phone screen in the display arrangement: `adb shell settings put secure include_default_display_in_topology 1`. Without it Android drops the phone screen from the arrangement while the desktop is open, and the phone cursor lands on the desktop instead.
  - Open the desktop, then in Settings → Connected devices → External display drag it **below** the phone. Android first places it above the phone, where the cursor would slide onto it when PhoneEdge positions it; the position is remembered afterwards.

### Building the patched scrcpy
Get scrcpy v4.1 with the patch applied:
```powershell
git clone --branch v4.1 https://github.com/Genymobile/scrcpy.git C:\tools\scrcpy
git -C C:\tools\scrcpy am C:\tools\PhoneEdge\scrcpy\phoneedge.patch
```
- **Server:** needs JDK 17 (e.g. Temurin, unzipped to `C:\tools\jdk-17…`) and the Android SDK packages `platforms;android-36` and `build-tools;36.0.0` in `C:\tools\android-sdk`. Run `.\scrcpy\build-server.ps1` (`-ScrcpySource`, `-Jdk` and `-Sdk` for other locations).
- **Client:** needs MSYS2 in `C:\tools\msys64` with the MinGW64 packages listed at the top of `build-client.ps1`. Run `.\scrcpy\build-client.ps1`. It copies the ~100 DLLs the client needs from MSYS2 next to it (about 140 MB), so later MSYS2 updates cannot break it.

The server and client must match the installed scrcpy.exe's version (4.1): the client refuses a server built for another version. After updating scrcpy, apply the patch to the new version and rebuild both, or delete `dist\scrcpy-server-desktop` and `dist\scrcpy-desktop` to fall back to stock scrcpy.

## Config
`phoneedge.json` next to the exe (tray → Open config creates it). Fields: `scrcpyDir`, `deviceSerial`, `edge` (left|right|top|bottom), `monitor` (primary|index), `enterDwellMs`, `exitDwellMs`, `sensitivity`, `forwardKeyboard`, `rightClickIsBack`, `middleClickIsDone`, `syncClipboard`, `openDesktopOnConnect`, `desktopDisplay`, `desktopFlex`, `panicHotkey`, `logLevel`.

## Logs
`phoneedge.log` next to the exe (tray → Open log).
