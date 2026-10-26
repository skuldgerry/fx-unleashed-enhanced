# FX Unleashed Enhanced: what is changed

A modified version of [FX Unleashed](https://github.com/fxunleashed/fx-unleashed) by ziadkadry99, kept in step with its
releases. Changed by [skuldgerry](https://github.com/skuldgerry) from 2026-10-12 on, under the same licence
(GPL-3.0 or later, see [LICENSE](LICENSE) and [NOTICE](NOTICE)). The DLL (`User.FXProRpmSync.dll`) and the plugin class keep
their names, so SimHub keeps its activation and control bindings; only the name it shows changes.

Based on FX Unleashed 0.7.6. Versions add a digit to the original's: 0.7.61 is FX Unleashed 0.7.6 with this edition's
first update.

## 0.7.62

- A custom alert's delete button sits on its second row (with its name and condition): on the alert's own row it went
  past the width and dropped under the arrows.
- The own colours' effect box is wide enough for "The group's effect, in this colour".

## 0.7.61

- **Buttons and encoders in colours of their own** while driving: picked on the drawing of the wheel under Buttons or
  Encoders; the group's effect in that colour (on the encoders' setting levels, dim low and full high) or an effect of
  their own in it. Over the group's effect, under the pit limiter, the rev bar's extras and the alerts; an encoder's blink
  after a change shows whatever they do.
- **Alerts with no colour**: their lights keep the colour they have and just blink or pulse in the alert's rhythm.
- **Custom alerts**: a note on what each is for; the condition in a bigger box (the pencil), to paste a long formula.
- **Any colour**: colour fields open the ready colours and a picker (saturation and brightness, hue).
- **Driver number screensaver**: a number and a name, each switched on and in its colour, a line under it that a light
  runs along or that breathes; the FX Unleashed Enhanced logo with neither. Painted savers' art is cached by what it
  shows, not only by its kind.
- **Dashes from a zip**: Import a file and dropping onto the Dashes tab take a zip of dashes, each checked like a file.
- A text box's text can start at the top (the theme pinned it to the middle).

## Lights

- **Play on the wheel, from where you edit.** Every part of the "Your lights" editor (driving, not driving, engine start and
  stop, pit limiter, while driving) has a play button that keeps the wheel in that state, following every change, until you
  press stop, leave the tab or start a game. "Show on the wheel", top right of the preview, does the same for the state picked
  under "Preview as". Each rev bar extra, the encoders' levels and the alerts play their scenario. An info mark next to each
  says what to look for.
- **Try one alert.** Each alert has a play button: the wheel shows it as if it were on, over the driving lights, and follows
  its colour, lights and rhythm as you change them.
- **An alert on a single button.** An alert can light one button's own light (or several), picked on a drawing of the wheel (click the lights, or press the buttons), next to or instead
  of the groups.
- **An alert that stays on.** A custom alert can stay on for a number of seconds after its condition was last true: for
  values that are only true for an instant, like iRacing's headlight flash (`dcHeadlightFlash`, about 70 ms a press).
- **A usual button map for the FX Pro**, so single-button alerts and "light a button while it's pressed" work without mapping
  first. The Button lights card now says what the map is for, apart from the pressed-button colour.
- **Clearer editor.** "Your lights" says it is the look while driving, with links to the other parts; the encoders' setting
  levels are a switch of their own instead of one more effect; an effect that "Hold still" or "Side lights dark" changes on
  track says so where it is picked; the two actions that move a car's lights from the wheel can be bound in the rev lights
  editor.

## Rev lights

- **Measured cars use measured data.** For the cars measured in the sim, this edition's own data comes before the game
  data FX Unleashed 0.7.6 reads from iRacing's files (which keeps only each light's first colour; on the BMW M4 GT3 the
  red and the white dots never showed):
  - **BMW M4 GT3**: its own profile, per gear (5 green together, 5 light blue, all red, steady), then two white dots in
    the middle 350 rpm past the red;
  - **McLaren 720S GT3**: Lovely Car Data's lights (checked against the sim), then red and blue on every other light one
    step of the car's light spacing past the all-blue.
  The game's own pit limiter lights are kept.
- **Stages past the shift point** for any car, in Car tuning: the whole bar in one colour, two colours on every other
  light, or only the middle lights, steady or blinking, each starting so many rpm (or light steps) past the shift point.

## Settings

- **Backup** (About tab): export the settings to a file, with the dash and screensaver files in a zip if wanted, and import
  them on this PC or another. What belongs to one PC and one wheel (firmware and screen state, SimPro's captured presets,
  where updates come from) is never exported or imported. An import is checked whole and applied at the next start, after a
  copy of the current settings.

## Updates

- The in-plugin updater looks for this edition's releases (`skuldgerry/fx-unleashed-enhanced`), not the official ones, which would
  replace it. Settings from the official plugin are pointed here on the first start.
