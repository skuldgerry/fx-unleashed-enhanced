<p align="center">
  <img src="../assets/brand/lockup-enhanced.png" alt="FX Unleashed Enhanced" width="460">
</p>

<p align="center">
  An enhanced edition of <a href="https://github.com/fxunleashed/fx-unleashed"><b>FX Unleashed</b></a>, the free SimHub
  plugin for the Simagic FX Pro, GT Neo and FX: more ways to see your lights on the wheel while you set them up, alerts on
  single buttons, buttons and encoders in colours of their own, a driver number screensaver, measured shift lights for
  some iRacing cars, and a backup of your settings.
</p>

> [!IMPORTANT]
> **Use the original FX Unleashed.** It is ziadkadry99's work, and the project to use and support:
> [fxunleashed/fx-unleashed](https://github.com/fxunleashed/fx-unleashed) and [fxunleashed.com](https://fxunleashed.com).
> This edition is just my own changes for my own use case; I decided to make it public in case someone finds them
> helpful. My dashes and my lights profile are in [fx-unleashed-dashes](https://github.com/skuldgerry/fx-unleashed-dashes).

> **An independent community project.** FX Unleashed Enhanced is a modified version of FX Unleashed by ziadkadry99. It
> is not made or supported by the FX Unleashed project, and neither is affiliated with, endorsed by or supported by
> Simagic. Simagic, FX Pro and SimPro Manager are trademarks of their owners, used only to say which hardware this works
> with. The software is provided "as is", without warranty of any kind. Use it at your own risk.

## Built on FX Unleashed

All the real work behind this plugin is **[FX Unleashed](https://github.com/fxunleashed/fx-unleashed)** by
**ziadkadry99** and its contributors: the plugin itself, Unleashed mode and its custom firmware, the dash format and
designer, the library, the lights engine and everything else. Its website is **[fxunleashed.com](https://fxunleashed.com)**.
This edition follows each FX Unleashed release (currently **0.7.6**) and adds the changes below on top. Its versions add
a digit to the original's: **0.7.61** is FX Unleashed 0.7.6 with this edition's first update, 0.7.62 the next, 0.7.70
the first on FX Unleashed 0.7.7.

For setup, the custom firmware, dashes, the library and everything not listed here, the original documentation applies:
its **[README](../README.md)**, its **[setup guide](../docs/setup.md)** and its website. Every change of this edition is
listed in [CHANGES-ENHANCED.md](../CHANGES-ENHANCED.md).

## [Dashes and tools](https://github.com/skuldgerry/fx-unleashed-dashes)

My dashes for the FX Pro's screen (replicas of iRacing GT3 displays and a default dash based on the Lovely Dashboard)
and my lights profile, ready to import into the plugin.

## What's added

### See it on the wheel, right where you edit it

Every part of the lights editor has a **play** button. Press it and the wheel stays in that state (driving, a menu, the
engine off, the start-up, the pit limiter...) with your lights, and **follows every change you make**: pick another effect,
colour or brightness and the wheel shows it at once. Press **stop**, leave the tab or start a game to give the wheel back.
An info mark next to each one says what you should see.

- **Show on the wheel**, top right of the preview, holds the state picked under "Preview as".
- Each **rev bar extra** (pit speed, refuelling, brake bias, launch aid...), the **encoder levels** and **every alert in
  turn** play their scripted scenario.

![The lights editor: "Your lights" with its play button and links, and a section with play and info](../docs/enhanced/play-buttons.png)

### Try any alert

Each alert has its own play button: the wheel shows it as if its moment had come, over the driving lights, and follows its
colour, lights, rhythm and style as you change them. No need to wait for a blue flag to see what it looks like.

![The alerts list with a play button on each alert, the button picker open, and a custom alert that stays on](../docs/enhanced/alerts.png)

### Alerts on single buttons

An alert can light **single buttons' own lights**, next to or instead of the light groups: press the alert's buttons icon
and pick them on a drawing of the wheel (click their lights, or press the buttons on the wheel). And a custom alert can
**stay on** for a number of seconds after its condition was last true, for values that only flicker on for an instant.

The example this was made for: the button you flash your headlights with lights up when the car really flashes. In
iRacing, `DataCorePlugin.GameRawData.Telemetry.dcHeadlightFlash` is true for only about 70 ms per press; with
**Stays on: 2.5 s** the button lights for the whole flash. Another: two buttons blinking while the pit limiter is on (a
custom alert on `pitLimiter`, with those two buttons picked).

![The headlight button lights up for the whole flash in iRacing](../docs/enhanced/flash.gif)

The FX Pro comes with its **usual button map**, so a button's light works without mapping the buttons first (map them
yourself if one lights the wrong light). The Button lights card now says what the map is for, separately from the colour
of "Light a button while it's pressed".

![The Button lights card](../docs/enhanced/button-lights.png)

### Buttons and encoders in colours of their own

Under **Buttons** and **Encoders** in "Your lights", pick lights on the drawing of the wheel (click them, or press their
buttons) and give them a colour that stays theirs while you drive: the pit limiter button always red, the TC encoder
always blue, easy to find. Each row either keeps **the group's effect in that colour** (a plasma ripples, a breath fades,
in shades of that one colour; on the encoders' setting levels, dim for a low setting and full for a high one) or gets
**an effect of its own** in it: steady, breathing, heartbeat, strobes, sparkle or twinkling stars. Alerts and the pit
limiter still show over them, and so does an encoder's blink when its setting changes.

### Alerts: no colour, a note, long conditions

- An alert's colour can be **No colour**: its lights keep the colour they have (the effect's, or one of their own) and
  just blink or pulse in the alert's rhythm.
- Each custom alert has a **note** for what it's for, under its row and on its name.
- The pencil next to **On while** opens the condition in a bigger box, to paste and read a long formula.

The example these were made for: the voice button keeps its colour and blinks while someone is talking on iRacing's radio,
so you know even with the sound muted or on the wrong device. A custom alert with No colour, the voice button picked, and
this condition:

```
[DataCorePlugin.GameRawData.Telemetry.RadioTransmitCarIdx] >= 0 && [DataCorePlugin.GameRawData.Telemetry.RadioTransmitCarIdx] != [DataCorePlugin.GameRawData.Telemetry.PlayerCarIdx]
```

### Any colour

Every colour field opens the ready colours and a picker for any other: a square of saturation and brightness over a strip
of hues. An alert's also offers No colour.

![The colour picker: no colour, ready colours, and any colour](../docs/enhanced/colour-picker.png)

### Driver number screensaver

Your racing number and your name, each switched on on its own and each in its colour, like the Lovely Dashboard's idle
screen: built in (Idle & sleep tab) instead of a picture to make and upload. It can have a light running along a line
under the number, or that line breathing. With neither the number nor the name, it shows the FX Unleashed Enhanced logo.

![The driver number screensaver: a number and a name, and the logo](../docs/enhanced/driver-number.png)

### Dashes from a zip

The Dashes tab's **Import a file…** (and dropping onto the tab) takes a zip of dashes too, such as the all-dashes pack in
my dashes' releases: each dash in it is checked like a dash file of its own.

### Measured shift lights

FX Unleashed 0.7.6 reads iRacing cars' shift lights from the sim's files, but it keeps only each light's first colour:
on the BMW M4 GT3 the red and the two white dots never show. For these cars, this edition uses the lights as they are in
the sim:

- **BMW M4 GT3**: per gear, five green together, five light blue, all red, then two white dots in the middle;
- **McLaren 720S GT3**: the lights up to all blue, then red and blue on every other light one step later in every gear.

The game's own pit limiter lights are kept. Any car can also get **stages past the shift point** in Car tuning: the whole
bar in one colour, two colours on every other light, or only the middle lights, steady or blinking.

### A clearer lights editor

- "Your lights" says it is the look **while driving**, with links to every other part of the editor (not driving, engine
  start and stop, pit limiter, while driving, rev bar extras, alerts).
- The encoders' **setting levels** (ABS, TC, brake bias, DIFF, map) are a switch of their own, not one more effect in the
  list.
- An effect that **Hold still** or **Side lights dark** changes on track says so where you pick it.
- The two actions that move a car's shift lights earlier or later from a wheel button can be bound right in the rev lights
  editor.

### Backup and move your settings

**About > Backup**: export your lights, alerts, dashes per car, car tuning, buttons and options to one file, with your
dash and screensaver files in a zip if you like, and import them on this PC or another. What belongs to one PC and one wheel
(firmware and screen state, presets captured from SimPro, where updates come from) is never exported or imported. An
import is applied the next time SimHub starts, after a copy of your current settings is kept.

![About > Backup: export and import settings](../docs/enhanced/backup.png)

## Install

1. Close SimHub.
2. Copy `User.FXProRpmSync.dll` from a release zip of this repository into your SimHub folder (normally
   `C:\Program Files (x86)\SimHub\`), replacing the one there. It is the same file name as the official plugin: **use one
   or the other**, not both.
3. Start SimHub. The plugin shows as **FX Unleashed Enhanced**; your settings, dashes and bindings are kept.

Updates come from this repository's releases (About tab). Settings from the official plugin are pointed here on the first
start, so an official update doesn't replace this edition by surprise.

**Going back to the official plugin:** install its DLL the same way, and in About > More set "Releases from" back to
`fxunleashed/fx-unleashed`. Your settings load there too; it ignores what it doesn't know (single-button alerts and their
hold time, colours of their own, the driver number and alerts' notes are dropped the next time it saves). Export a backup
first.

## License and credits

- **[FX Unleashed](https://github.com/fxunleashed/fx-unleashed)** by ziadkadry99 and contributors, GPL-3.0 or later.
  **This edition** by [skuldgerry](https://github.com/skuldgerry), under the same licence: see
  [LICENSE](../LICENSE), [NOTICE](../NOTICE) and [CHANGES-ENHANCED.md](../CHANGES-ENHANCED.md).
- Car rev light data, dashes and other credits: as listed in the [NOTICE](../NOTICE).
- No Simagic software is in this repository or its releases. The custom firmware for Unleashed mode is FX Unleashed's,
  from [fx-unleashed-firmware](https://github.com/fxunleashed/fx-unleashed-firmware).
