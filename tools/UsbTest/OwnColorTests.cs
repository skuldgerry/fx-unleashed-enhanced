using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using User.FXProRpmSync;

/// <summary>Buttons' and encoders' lights with a colour of their own while driving (LightProfile.OwnColors).</summary>
static class OwnColorTests
{
    static void Check(string name, bool ok, string detail = "") => FeatureTests.Check("own colour: " + name, ok, detail);

    static string Show(LedColor c) => $"{c.R},{c.G},{c.B}/{c.Brightness}";

    public static void Run()
    {
        var engine = new LightEngine();
        var p = new LightProfile { MaxBrightness = 90 };
        // the buttons alternate cyan and blue, steady; the encoders a red breath
        p.Groups[LedGroup.Buttons] = new GroupLighting { Effect = LightEffect.Solid, Colors = new List<string> { "#00FFFF", "#0000FF" }, Brightness = 50 };
        p.Groups[LedGroup.Encoders] = new GroupLighting { Effect = LightEffect.Solid, Colors = new List<string> { "#FF0000" } };
        p.Groups[LedGroup.Rev] = new GroupLighting { Effect = LightEffect.Off };
        p.StillWhileDriving = true;
        var v = new DashValues { Running = true, MaxRpm = 8000, Rpm = 3000 };
        LedColor[] Frame(CarState s = CarState.Driving) => engine.Render(p, v, null, 0.01, false, new LightMoment { State = s });

        var before = Frame();
        p.OwnColors.Add(new OwnColor { Leds = { 0, 1 }, Color = "#FF0000", Effect = LightEffect.Solid });
        p.OwnColors.Add(new OwnColor { Leds = { 2, 3, 12 }, Color = "#00FF00" }); // the group's effect, the default
        var f = Frame();
        Check("steady: the colour, at the group's brightness", f[0].R == 255 && f[0].G == 0 && f[0].B == 0 && f[0].Brightness == before[0].Brightness && f[1].R == 255, Show(f[0]) + " " + Show(f[1]));
        // the effect in green: light 2 is cyan (the brightest of the group's colours), light 3 blue (far darker)
        Check("the effect in its colour follows how bright it is there", f[2].G == 255 && f[2].R == 0 && f[3].G > 0 && f[3].G < 40, Show(f[2]) + " " + Show(f[3]));
        Check("an encoder's light takes it too", f[12].G == 255 && f[12].R == 0, Show(f[12]));
        Check("the other lights keep their effect", f[4].R == before[4].R && f[4].G == before[4].G && f[4].B == before[4].B && f[13].R == before[13].R);

        // not when parked: the state's look is the whole wheel's
        var idle = Frame(CarState.Idle);
        var idleBefore = new LightEngine().Render(Without(p), v, null, 0.01, false, new LightMoment { State = CarState.Idle });
        Check("not with the car parked (the look's own)", idle[0].R == idleBefore[0].R && idle[0].G == idleBefore[0].G && idle[0].B == idleBefore[0].B);

        // the first that has a light wins it; only the buttons and encoders; another wheel's numbers mean nothing here
        p.OwnColors.Add(new OwnColor { Leds = { 0, 30 }, Color = "#FFFFFF" });
        p.OwnColors.Add(new OwnColor { Wheel = "gtneo", Leds = { 5 }, Color = "#FFFFFF" });
        f = Frame();
        Check("the first one to name a light wins it", f[0].R == 255 && f[0].G == 0);
        Check("never the rev bar", f[30].R == before[30].R && f[30].G == before[30].G && f[30].B == before[30].B);
        Check("another wheel's lights are left alone", f[5].R == before[5].R && f[5].G == before[5].G && f[5].B == before[5].B);

        // alerts still show over them
        v.PitLimiter = true;
        p.Alerts.Add(new AlertRule { Trigger = AlertTrigger.PitLimiter, Color = "#0000FF", BlinkHz = 0, Groups = { LedGroup.Buttons } });
        f = Frame();
        Check("an alert shows over them", f[0].B == 255 && f[0].R == 0, Show(f[0]));
        v.PitLimiter = false;
        p.Alerts.Clear();

        // kept in a copy and in the settings file, and a file from before them still loads
        var copy = p.Clone();
        copy.OwnColors[0].Leds.Add(9);
        Check("a copy has its own list", p.OwnColors[0].Leds.Count == 2 && copy.OwnColors.Count == p.OwnColors.Count);
        var back = JsonConvert.DeserializeObject<LightProfile>(JsonConvert.SerializeObject(p));
        Check("saved and read back", back.OwnColors.Count == 4 && back.OwnColors[1].Look == null && back.OwnColors[0].Look == LightEffect.Solid &&
                                     back.OwnColors[1].Leds.SequenceEqual(new[] { 2, 3, 12 }) && back.OwnColors[3].Wheel == "gtneo");
        var old = JsonConvert.DeserializeObject<LightProfile>("{\"Id\":\"x\",\"Name\":\"Old\"}");
        Check("lights saved before them have none", old.OwnColors != null && old.OwnColors.Count == 0);
        var first = JsonConvert.DeserializeObject<OwnColor>("{\"Leds\":[1],\"Color\":\"#FF0000\",\"KeepEffect\":false}");
        Check("the first test version's steady still reads as steady", first.Look == LightEffect.Solid);

        Levels();
    }

    /// <summary>On the encoders' setting levels: dim for a low setting, full for a high one, and the blink after a change.</summary>
    static void Levels()
    {
        var engine = new LightEngine();
        var p = new LightProfile { MaxBrightness = 90, StillWhileDriving = true };
        p.Groups[LedGroup.Encoders] = new GroupLighting { Effect = LightEffect.Levels, Colors = new List<string> { "#00FF40", "#FFB000", "#FF0020" }, FlashOnChange = true };
        p.Groups[LedGroup.Buttons] = new GroupLighting { Effect = LightEffect.Solid, Colors = new List<string> { "#FFFFFF" } };
        p.OwnColors.Add(new OwnColor { Leds = { 12, 13 }, Color = "#FF0000" });                              // ABS and TC: the levels in red
        p.OwnColors.Add(new OwnColor { Leds = { 14 }, Color = "#0000FF", Effect = LightEffect.Solid });       // brake bias: steady blue
        p.OwnColors.Add(new OwnColor { Leds = { 0 }, Color = "#00FF00", Effect = LightEffect.Breathe });      // a button: a breath of its own
        var v = new DashValues { Running = true, MaxRpm = 8000, Rpm = 3000 };
        v.Set("absLevel", 1); v.Set("tcLevel", 11); v.Set("brakeBias", 57);
        LedColor[] At(double t) => engine.Render(p, v, null, t, false, new LightMoment { State = CarState.Driving });
        At(0);
        var f = At(0.5);
        Check("levels: a low setting dim, a high one full", f[12].R > 40 && f[12].R < 90 && f[13].R == 255 && f[12].G == 0, $"ABS {f[12].R}, TC {f[13].R}");
        Check("levels: steady stays its colour", f[14].B == 255 && f[14].R == 0);
        v.Set("brakeBias", 57.5); At(1.0);
        var blinkOff = Enumerable.Range(0, 16).Select(k => At(1.0 + k / 32.0)[14].B).ToList();
        Check("levels: a change blinks it, steady or not", blinkOff.Min() < 40 && blinkOff.Max() == 255, string.Join(",", blinkOff));
        Check("its own effect, held still while driving", f[0].G == 255 && f[0].R == 0, Show(f[0]));
        p.StillWhileDriving = false;
        var breath = Enumerable.Range(0, 8).Select(k => At(3 + k * 0.5)[0].G).ToList();
        Check("its own effect moves when not held still", breath.Max() - breath.Min() > 100, string.Join(",", breath));
    }

    static LightProfile Without(LightProfile p) { var c = p.Clone(); c.OwnColors.Clear(); return c; }
}
