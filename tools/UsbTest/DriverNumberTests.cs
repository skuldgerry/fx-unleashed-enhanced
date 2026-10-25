using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using User.FXProRpmSync;

/// <summary>The driver number screensaver (DriverNumberSaver): drawn for the number and name it's given, its looks' cost.</summary>
static class DriverNumberTests
{
    static void Check(string name, bool ok, string detail = "") => FeatureTests.Check("driver number: " + name, ok, detail);

    sealed class Sink : IScreenSink
    {
        public PreviewScreen P = new PreviewScreen();
        public long Bytes;
        public List<string> Fills = new List<string>();
        public bool Log;
        public void Cmd(string c) { Bytes += c.Length + 3; P.Cmd(c); if (Log && c.StartsWith("fill ")) Fills.Add(c); }
        public void Flush() { }
    }

    /// <summary>The saver drawn in full, then animated for `seconds`: the screen, the first draw's bytes, the animation's bytes a second.</summary>
    static (Sink Screen, long First, double PerSecond) Draw(DriverCard card, bool tiles = false, double seconds = 6)
    {
        var s = new Sink();
        var saver = IdleScreens.Animated(new SaverItem { Id = IdleScreens.DriverNumberId, Kind = SaverKind.Builtin, Driver = card });
        if (tiles && saver is ITiledSaver ts) { var t = ts.Tiles; ts.UseTiles = true; }
        saver.Start();
        while (saver.Drawing) saver.Step(s, 0, 1000);
        long first = s.Bytes;
        s.Log = true;
        for (double t = 0; t < seconds; t += 0.02) saver.Step(s, t, 1000);
        return (s, first, (s.Bytes - first) / seconds);
    }

    static int Count(Bitmap b, Color c)
    {
        int n = 0;
        for (int y = 0; y < b.Height; y += 2) for (int x = 0; x < b.Width; x += 2) { var p = b.GetPixel(x, y); if (Math.Abs(p.R - c.R) < 24 && Math.Abs(p.G - c.G) < 24 && Math.Abs(p.B - c.B) < 24) n++; }
        return n;
    }

    public static void Run(string dir)
    {
        var u = new UsbSettings();
        Check("in the gallery, with the settings' number", IdleScreens.All(u).Any(x => x.Id == IdleScreens.DriverNumberId && x.Driver != null) &&
                                                           IdleScreens.Find(u, IdleScreens.DriverNumberId).Name == "Driver number");

        var card = new DriverCard { ShowNumber = true, Number = "18", NumberColor = "#2E86FF", ShowName = true, Name = "a. driver", NameColor = "#FFFFFF", Look = DriverNumberLook.Flat };
        var flat = Draw(card);
        flat.Screen.P.Bitmap.Save(Path.Combine(dir, "saver_driver_flat.png"), ImageFormat.Png);
        int blue = Count(flat.Screen.P.Bitmap, Color.FromArgb(0x2E, 0x86, 0xFF)), white = Count(flat.Screen.P.Bitmap, Color.White);
        Check("draws the number in its colour and the name in its own", blue > 2000 && white > 200, $"{blue} blue, {white} white samples");
        Check("first draw fits the screen's link (under 15 s)", flat.First < 15 * 25000, $"{flat.First / 1024.0:0.0} KB");
        Check("flat: nothing more is sent", flat.PerSecond == 0);

        card.Look = DriverNumberLook.Sweep;
        var sweep = Draw(card);
        sweep.Screen.P.Bitmap.Save(Path.Combine(dir, "saver_driver_sweep.png"), ImageFormat.Png);
        Check("sweep: a few fills a frame", sweep.PerSecond > 0 && sweep.PerSecond < 2000, $"{sweep.PerSecond:0} B/s");
        card.Look = DriverNumberLook.Pulse;
        var pulse = Draw(card);
        Check("pulse: one fill when the shade changes", pulse.PerSecond > 0 && pulse.PerSecond < 600, $"{pulse.PerSecond:0} B/s");

        // the line's animation lands on the line, also when the art comes from the cache or the picture memory (it isn't
        // painted again then: the first version drew its light on the screen's top row)
        int Lowest(Sink s) => s.Fills.Count == 0 ? -1 : s.Fills.Min(f => int.Parse(f.Substring(5).Split(',')[1]));
        card.Look = DriverNumberLook.Sweep;
        var cachedArt = Draw(card, tiles: true);
        Check("the line's light runs under the number, from the picture memory too", Lowest(sweep.Screen) > 250 && Lowest(cachedArt.Screen) == Lowest(sweep.Screen),
              $"y {Lowest(sweep.Screen)} / {Lowest(cachedArt.Screen)}");

        // the picture follows the number: another number is another picture, not the one cached for the first
        var other = Draw(new DriverCard { ShowNumber = true, Number = "7", NumberColor = "#FF2030", Look = DriverNumberLook.Flat });
        Check("another number, another picture", Count(other.Screen.P.Bitmap, Color.FromArgb(0xFF, 0x20, 0x30)) > 1500 && Count(other.Screen.P.Bitmap, Color.FromArgb(0x2E, 0x86, 0xFF)) < 50);
        var tiled = Draw(new DriverCard { ShowNumber = true, Number = "7", NumberColor = "#FF2030" }, tiles: true);
        Check("from the screen's picture memory too", tiled.First < other.First && Count(tiled.Screen.P.Bitmap, Color.FromArgb(0xFF, 0x20, 0x30)) > 1500, $"{tiled.First / 1024.0:0.0} KB vs {other.First / 1024.0:0.0} KB");

        // each switched on on its own; neither: the logo, still
        var nameOnly = Draw(new DriverCard { ShowName = true, Name = "a. driver", Number = "18", Look = DriverNumberLook.Pulse });
        nameOnly.Screen.P.Bitmap.Save(Path.Combine(dir, "saver_driver_name.png"), ImageFormat.Png);
        Check("the name alone, without the number", Count(nameOnly.Screen.P.Bitmap, Color.White) > 600 && Count(nameOnly.Screen.P.Bitmap, Color.FromArgb(0x2E, 0x86, 0xFF)) < 200);
        var logo = Draw(new DriverCard { Number = "18", Name = "a. driver", Look = DriverNumberLook.Sweep });
        logo.Screen.P.Bitmap.Save(Path.Combine(dir, "saver_driver_logo.png"), ImageFormat.Png);
        Check("neither: the logo, and nothing moving", Count(logo.Screen.P.Bitmap, Color.FromArgb(0xFF, 0x1F, 0x2D)) > 400 && logo.PerSecond == 0);

        Check("text as shown: trimmed, capitals, cut to fit", DriverCard.Clean("  a. driver  ", 24) == "A. DRIVER" && DriverCard.Clean("1234", 3) == "123");
        var keyA = new DriverCard { ShowNumber = true, Number = "18" }.Key; var keyB = new DriverCard { ShowNumber = true, Number = "19" }.Key;
        Check("a new number starts it again (its key changes)", keyA != keyB && new DriverCard { ShowNumber = true, Number = " 18 " }.Key == keyA &&
                                                                 new DriverCard { Number = "18" }.Key != keyA && new DriverCard { ShowNumber = true, Number = "18", Look = DriverNumberLook.Pulse }.Key != keyA);

        u.Driver = new DriverCard { Number = "44", Name = "x", Look = DriverNumberLook.Pulse };
        var back = JsonConvert.DeserializeObject<UsbSettings>(JsonConvert.SerializeObject(u));
        Check("saved and read back", back.Driver.Number == "44" && back.Driver.Look == DriverNumberLook.Pulse);
        var old = JsonConvert.DeserializeObject<UsbSettings>("{\"ScreenSaver\":true}");
        Check("settings from before it get the default", old.Driver != null && old.Driver.Number == "1");
    }
}
