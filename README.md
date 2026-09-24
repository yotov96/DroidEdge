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

- `desktopDisplay`: initial size and density in scrcpy's syntax, e.g. `"1920x1080"`, `"1440x1800/240"`, `"/240"` (phone size, 240 dpi) or `""` (phone size and density). Higher dpi means bigger UI.
- `desktopFlex` (default `true`): the virtual display keeps resizing to match the window, so it fits whatever size your window manager gives it.
- Both are re-read each time the window opens; close it and reopen from the tray to apply changes.

### Desktop windows (Android 17)
With stock scrcpy, clicking the taskbar or app list on the virtual display opens nothing: Android only lets a secondary display host desktop windows if the display can switch between mirroring and extending, and scrcpy's virtual display can't. `scrcpy-server/android17-desktop.patch` makes scrcpy's server create one that can, on Android 17 and later. Apps then open as desktop windows (move, resize, minimize, maximize, close) on the virtual display.

- Build the patched server into `dist\scrcpy-server-desktop` (below). PhoneEdge uses it for the desktop window whenever the file is there; the log says which server was used. On Android 16 and older, or if Android rejects the new flag, it behaves like stock scrcpy.
- One-time phone setup:
  - Keep the phone screen in the display arrangement: `adb shell settings put secure include_default_display_in_topology 1`. Without it Android drops the phone screen from the arrangement while the desktop is open, and the phone cursor lands on the desktop instead.
  - Open the desktop, then in Settings → Connected devices → External display drag it **below** the phone. Android first places it above the phone, where the cursor would slide onto it when PhoneEdge positions it; the position is remembered afterwards.

### Building the desktop server
Needs JDK 17 (e.g. Temurin, unzipped to `C:\tools\jdk-17…`) and the Android SDK packages `platforms;android-36` and `build-tools;36.0.0` in `C:\tools\android-sdk` (`build.ps1` takes `-ScrcpySource`, `-Jdk` and `-Sdk` for other locations).
```powershell
git clone --branch v4.1 https://github.com/Genymobile/scrcpy.git C:\tools\scrcpy
git -C C:\tools\scrcpy am C:\tools\PhoneEdge\scrcpy-server\android17-desktop.patch
.\scrcpy-server\build.ps1
```
The server must match scrcpy.exe's version, which refuses a server built for another one. After updating scrcpy, apply the patch to the new version and rebuild, or delete `dist\scrcpy-server-desktop` to fall back to the stock server.

## Config
`phoneedge.json` next to the exe (tray → Open config creates it). Fields: `scrcpyDir`, `deviceSerial`, `edge` (left|right|top|bottom), `monitor` (primary|index), `enterDwellMs`, `exitDwellMs`, `sensitivity`, `forwardKeyboard`, `rightClickIsBack`, `middleClickIsDone`, `syncClipboard`, `openDesktopOnConnect`, `desktopDisplay`, `desktopFlex`, `panicHotkey`, `logLevel`.

## Logs
`phoneedge.log` next to the exe (tray → Open log).
