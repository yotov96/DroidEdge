# PhoneEdge

Move your mouse past a monitor edge and it appears on your USB-connected Android phone as a real cursor. Keyboard follows. No app on the phone: PhoneEdge drives scrcpy 4.1's server over adb.

## Requirements
- Windows 11, scrcpy 4.1 in `C:\Users\<you>\AppData\Local\Programs\scrcpy` (or set `scrcpyDir` in `phoneedge.json`)
- Phone with USB debugging enabled, connected by cable, unlocked

## Run
- `.\publish.ps1` then start `dist\PhoneEdge.exe`; it lives in the tray.
- Push the mouse against the **left edge of the primary monitor for 1 s** to cross. Push against the phone's right edge for 1 s to come back.
- Right-click is Android **Back** (set `rightClickIsBack` to `false` to send it as a normal secondary button instead).
- Middle-click (wheel click) is **"I'm done"**: it locks the phone with a Power press and returns the mouse to the PC (set `middleClickIsDone` to `false` for a normal middle button). Requires the phone's "Power button instantly locks" setting, which is the default.
- Panic: hold **Left Ctrl + Right Ctrl** to return instantly.
- `PhoneEdge.exe --diag` runs an end-to-end check and shows a report.

## Config
`phoneedge.json` next to the exe (tray → Open config creates it). Fields: `scrcpyDir`, `deviceSerial`, `edge` (left|right|top|bottom), `monitor` (primary|index), `enterDwellMs`, `exitDwellMs`, `sensitivity`, `forwardKeyboard`, `rightClickIsBack`, `middleClickIsDone`, `panicHotkey`, `logLevel`.

## Logs
`phoneedge.log` next to the exe (tray → Open log).
