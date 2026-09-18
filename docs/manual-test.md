# PhoneEdge manual end-to-end checklist

Setup: phone plugged in, unlocked, USB debugging authorized. Start `PhoneEdge.exe`. Tray icon turns green and a balloon says the phone connected.

1. **Cross** — push the mouse against the left edge of the primary monitor and keep pushing for ~1 s. Expected: Windows cursor stops at the edge, a cursor appears at the right edge of the phone at the matching height, tray icon turns blue.
2. **Move/click** — move around, left-click an app icon, long-press. Expected: 1:1 feel, no drift at edges.
3. **Right-click** — expected: Android Back.
4. **Scroll** — wheel in a list; horizontal wheel in a horizontally scrolling view.
5. **Type** — click a text field, type letters, Shift+letters, Backspace, Enter, arrows, Esc (Back). Expected: correct characters.
6. **Return** — push against the phone's right edge for ~1 s. Expected: Windows cursor reappears at the left edge at the matching height, icon green, no stuck buttons or keys on either side.
7. **Accidental prevention** — tap the edge briefly (<1 s) several times. Expected: no crossing.
8. **Panic** — cross, then hold Left Ctrl + Right Ctrl. Expected: instant return.
9. **Cable pull while on phone** — cross, then unplug. Expected: mouse returns to the PC immediately, balloon "Phone disconnected", icon grey. Plug back in: balloon "Phone connected" within ~5 s, crossing works again.
10. **Rotation** — rotate the phone to landscape, wait 3 s, cross. Expected: cursor lands on the correct (right, now longer) edge and clamps at the new bounds.
11. **Diagnostics** — tray → Run diagnostics. Expected: dialog starts with PASS; the phone cursor tours the corners; the session resumes afterwards.
12. **Exit** — tray → Exit, then `adb shell settings get system pointer_speed` and `... mouse_pointer_acceleration_enabled`. Expected: original values (or `null`) restored; `adb shell ps -A | grep scrcpy` shows nothing.
13. **Second instance** — start PhoneEdge.exe again. Expected: message "already running".
