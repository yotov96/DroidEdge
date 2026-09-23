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

- `desktopDisplay`: initial size and density in scrcpy's syntax, e.g. `"1920x1080"`, `"1440x1800/240"`, `"/240"` (phone size, 240 dpi) or `""` (phone size and density). Higher dpi means bigger UI.
- `desktopFlex` (default `true`): the virtual display keeps resizing to match the window, so it fits whatever size your window manager gives it.
- Both are re-read each time the window opens; close it and reopen from the tray to apply changes.
- For resizable app windows on the virtual display, enable the phone's developer option **Force desktop mode** and reboot the phone.

## Config
`phoneedge.json` next to the exe (tray → Open config creates it). Fields: `scrcpyDir`, `deviceSerial`, `edge` (left|right|top|bottom), `monitor` (primary|index), `enterDwellMs`, `exitDwellMs`, `sensitivity`, `forwardKeyboard`, `rightClickIsBack`, `middleClickIsDone`, `syncClipboard`, `openDesktopOnConnect`, `desktopDisplay`, `desktopFlex`, `panicHotkey`, `logLevel`.

## Logs
`phoneedge.log` next to the exe (tray → Open log).
