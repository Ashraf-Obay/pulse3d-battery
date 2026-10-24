# Pulse 3D Battery

Windows tray icon showing the battery level of a Sony PULSE 3D wireless headset (USB dongle
`VID_054C&PID_0D5E`), which Windows and existing tools such as HeadsetControl don't support.

- Number = battery % (10% steps); amber at 30% or less, red at 20% or less, plus a one-time low-battery notification
- Green with a bar = charging · grey = headset off · `?` = waiting for first report · `x` = dongle not found
- Right-click: **Start with Windows** (HKCU Run key) and **Exit**
- About 1–3 MB in Task Manager, 0% CPU while waiting

## Build

```
build.cmd
```

Uses the C# compiler that ships with Windows (.NET Framework 4.x), so nothing needs installing.

## Protocol

Reverse-engineered by passive listening only; nothing is ever written to the device. The dongle sends an 8-byte
input report on HID collection `MI_03&COL04` (usage page `0xFF01`) whenever headset state changes:

| Byte | Meaning |
|---|---|
| 0 | `0xB0` report ID |
| 3 | battery 0–100 in steps of 10, or `0x80` while charging |
| 4 | bit `0x08` set = headset connected (cleared on power-off) |

The other bytes (a 0–100 level that is probably volume or chat mix, and button/mute flags) aren't used yet.
The dongle doesn't answer feature-report queries, so the level is unknown until the first report after startup.

## Files

- `Pulse3DBattery.cs`: the app (HID listener, tray icon, menu)
- `IconArt.cs`: hand-drawn 16 px pixel font for a sharp tray icon
- `IconPreview.cs`: dev tool that renders every icon state to a PNG
