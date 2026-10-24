# FX Unleashed Enhanced: what is changed

A modified version of [FX Unleashed](https://github.com/fxunleashed/fx-unleashed) by ziadkadry99, kept in step with its
releases. Changed by Rafael Gomes ([skuldgerry](https://github.com/skuldgerry)) from 2026-10-12 on, under the same licence
(GPL-3.0 or later, see [LICENSE](LICENSE) and [NOTICE](NOTICE)). The DLL (`User.FXProRpmSync.dll`) and the plugin class keep
their names, so SimHub keeps its activation and control bindings; only the name it shows changes.

Based on FX Unleashed 0.7.6.

## Lights

- **Play on the wheel, from where you edit.** Every part of the "Your lights" editor (driving, not driving, engine start and
  stop, pit limiter, while driving) has a play button that keeps the wheel in that state, following every change, until you
  press stop, leave the tab or start a game. "Show on the wheel", top right of the preview, does the same for the state picked
  under "Preview as". Each rev bar extra, the encoders' levels and the alerts play their scenario. An info mark next to each
  says what to look for.
- **Try one alert.** Each alert has a play button: the wheel shows it as if it were on, over the driving lights, and follows
  its colour, lights and rhythm as you change them.
- **An alert on a single button.** An alert can light one button's own light (or several), by its number, next to or instead
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

- The in-plugin updater looks for this edition's releases (`skuldgerry/fx-unleashed`), not the official ones, which would
  replace it. Settings from the official plugin are pointed here on the first start.
