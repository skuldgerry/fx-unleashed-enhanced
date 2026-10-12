using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using User.FXProRpmSync;
using static FeatureTests;

/// <summary>
/// The rev bar past the shift point (RpmLayout.After, RevPhase): the stages a car's own lights go through after the
/// flash, which the light data has no place for. Drawn through the light engine as a session would.
/// </summary>
static class RevPhaseTests
{
    static LedColor[] Rev(LedColor[] f) => f.Skip(23).Take(15).ToArray();
    static bool Is(LedColor c, int r, int g, int b) => c.R == r && c.G == g && c.B == b;
    static bool Dark(LedColor c) => c.R + c.G + c.B == 0;

    public static void Run()
    {
        // a car with a steady red flash at 7000, per gear: 4th shifts at 7200
        var car = RpmLayout.FromPattern(LedPatterns.Build(new FallbackStyle { Pattern = PatternKind.LeftToRight }), 7000, 0);
        car.FlashRpm = 7000; car.FlashColor = "#FF0000"; car.FlashBlinkUnits = 0;
        car.Gears = new Dictionary<string, GearCurve>();
        foreach (var gear in RpmLightsMapper.SimProGears)
            car.Gears[gear] = new GearCurve { Rpm = (int[])car.Rpm.Clone(), FlashRpm = gear == "4" ? 7200 : 7000 };

        var engine = new LightEngine(WheelModel.FxPro);
        var p = LightPresets.Find("synthwave").Clone();
        p.Rev.UseCarData = true; p.RevTint = 0;
        Func<RpmLayout, double, string, double, LedColor[]> at = (layout, rpm, gear, now) =>
            Rev(engine.Render(p, new DashValues { Running = true, EngineOn = true, MaxRpm = 8000, Rpm = rpm, GearKey = gear, SpeedKmh = 150 }, layout, now, false, LightMoment.Of(CarState.Driving)));

        Check("phases: none = the flash to the limiter, as before", car.PhaseAt(7900) == null && at(car, 7900, "3", 0).All(c => Is(c, 255, 0, 0)));

        // BMW M4 GT3: the bar goes out but for two white dots in the middle, 350 rpm above the shift point
        var m4 = new CarOverride { After = new List<RevPhase> { new RevPhase { AboveShiftRpm = 350, Look = PhaseLook.Centre, Count = 2, Color1 = "#FFFFFF" } } }.Apply(car, null);
        var dots = at(m4, 7400, "3", 0);
        Check("phases: below the stage the flash is untouched", at(m4, 7300, "3", 0).All(c => Is(c, 255, 0, 0)) && at(m4, 6000, "3", 0).Any(Dark));
        Check("phases: two dots in the middle, the rest dark", dots.Count(c => Is(c, 255, 255, 255)) == 2 && dots.Count(Dark) == 13 && Is(dots[6], 255, 255, 255) && Is(dots[7], 255, 255, 255));
        Check("phases: a stage follows the gear's own shift point", at(m4, 7400, "4", 0).All(c => Is(c, 255, 0, 0)) && at(m4, 7560, "4", 0).Count(c => Is(c, 255, 255, 255)) == 2);
        var later = new CarOverride { OffsetRpm = 100, After = m4.After }.Apply(car, null);
        Check("phases: and an offset of the lights", at(later, 7400, "3", 0).All(c => Is(c, 255, 0, 0)) && at(later, 7460, "3", 0).Count(c => Is(c, 255, 255, 255)) == 2);

        // McLaren 720S GT3: blue and red taking turns at the shift point, in place of the flash
        var mcl = car.Clone();
        mcl.After = new List<RevPhase> { new RevPhase { AboveShiftRpm = 0, Look = PhaseLook.Alternate, Color1 = "#0000FF", Color2 = "#FF0000", BlinkHz = 4 } };
        var a = at(mcl, 7000, "3", 0.0); var b = at(mcl, 7000, "3", 0.13);
        Check("phases: two colours on every other light", Enumerable.Range(0, 15).All(i => i % 2 == 0 ? Is(a[i], 0, 0, 255) : Is(a[i], 255, 0, 0)));
        Check("phases: trading places as they blink", Enumerable.Range(0, 15).All(i => i % 2 == 0 ? Is(b[i], 255, 0, 0) : Is(b[i], 0, 0, 255)) && at(mcl, 6990, "3", 0).Any(Dark));

        // Corvette Z06 GT3.R: red at the shift point, blue near the limiter; the highest stage reached wins
        var vette = car.Clone();
        vette.After = new List<RevPhase> { new RevPhase { AboveShiftRpm = 600, Color1 = "#FFFFFF" }, new RevPhase { AboveShiftRpm = 300, Color1 = "#0000FF" }, null };
        Check("phases: one colour over the whole bar, the highest stage reached", at(vette, 7200, "3", 0).All(c => Is(c, 255, 0, 0)) && at(vette, 7350, "3", 0).All(c => Is(c, 0, 0, 255)) && at(vette, 7700, "3", 0).All(c => Is(c, 255, 255, 255)));
        vette.After[1].BlinkHz = 2;
        Check("phases: one colour blinking goes dark between", at(vette, 7350, "3", 0.0).All(c => Is(c, 0, 0, 255)) && at(vette, 7350, "3", 0.3).All(Dark));

        // kept with the car's adjustment, and adjustments saved before it read as before
        var o = new CarOverride { CarKey = "IRacing | bmwm4gt3", After = m4.After };
        var back = JsonConvert.DeserializeObject<CarOverride>(JsonConvert.SerializeObject(o));
        var old = JsonConvert.DeserializeObject<CarOverride>("{\"CarKey\":\"IRacing | x\",\"Kind\":0,\"OffsetRpm\":50}");
        Check("phases: saved with the car and read back; older adjustments have none", back.After.Count == 1 && back.After[0].Look == PhaseLook.Centre && back.After[0].AboveShiftRpm == 350 && back.Clone().After[0] != back.After[0]
              && old.After == null && old.Apply(car, null).After == null && old.Summary == "Offset +50 rpm");
        Check("phases: named in the adjustment's summary", o.Summary.Contains("past the shift point") && o.Summary.Contains("2 lights in the middle"), o.Summary);
        Check("phases: the car's layout isn't changed by the adjustment", car.After == null && o.Apply(car, null).After[0] != o.After[0]);

        // the cars whose stages were measured have them without any adjustment; an adjustment keeps, replaces or removes them
        var known = KnownRevPhases.For("iracing | BMWM4GT3");
        Check("phases: the BMW M4 GT3's two dots are known; an unmeasured car has none", known != null && known.Count == 1 && known[0].Look == PhaseLook.Centre && known[0].Count == 2
              && KnownRevPhases.For("IRacing | mclaren720sgt3") == null && KnownRevPhases.For(null) == null && KnownRevPhases.For("IRacing | bmwm4gt3")[0] != known[0]);
        var withKnown = car.Clone(); withKnown.After = known;
        Check("phases: an offset adjustment keeps the car's own stages", new CarOverride { OffsetRpm = 50 }.Apply(withKnown, null).After?.Count == 1);
        Check("phases: so does a pattern for that car", new CarOverride { Kind = OverrideKind.Pattern, Style = new FallbackStyle { Pattern = PatternKind.LeftToRight } }.Apply(withKnown, null).After?.Count == 1);
        Check("phases: an adjustment's own stages replace them, and an empty list switches them off",
              new CarOverride { After = new List<RevPhase> { new RevPhase { AboveShiftRpm = 100 }, new RevPhase { AboveShiftRpm = 500 } } }.Apply(withKnown, null).After.Count == 2
              && new CarOverride { After = new List<RevPhase>() }.Apply(withKnown, null).After == null);
    }
}
