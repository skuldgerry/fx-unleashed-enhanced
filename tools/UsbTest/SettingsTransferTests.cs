using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using User.FXProRpmSync;

/// <summary>
/// The settings as a file (SettingsTransfer.cs): what an export leaves out, what an import refuses, and what the next
/// start does with one that is waiting, in a scratch "SimHub folder".
/// </summary>
static class SettingsTransferTests
{
    static void Check(string name, bool ok, string detail = "") => FeatureTests.Check("backup: " + name, ok, detail);

    /// <summary>Settings with something in every part: a person's own, and this PC's.</summary>
    static FXProRpmSyncSettings Sample(string repo)
    {
        var s = new FXProRpmSyncSettings { SettingsVersion = FXProRpmSyncPlugin.CurrentSettingsVersion, Mode = WheelMode.Unlocked };
        s.Overrides["IRacing | bmwm4gt3"] = new CarOverride();
        s.Usb.UserLights.Add(new LightProfile { Id = "user-1", Name = "Mine" });
        s.Usb.CarDashes["IRacing | bmwm4gt3"] = new UsbCarDash { CarKey = "IRacing | bmwm4gt3", Dashes = new List<string> { DashRef.Custom("bmw-m4-gt3-iracing") } };
        s.Usb.ButtonLeds[4] = 17;
        s.Usb.LedCeiling = 55;
        s.Usb.Wheels["gtneo"] = new WheelSettings { LightPreset = "x", TurnedOffSimHubDevice = true };
        // this PC's
        s.Originals["P1"] = "{}";
        s.ScreensOriginals["P1"] = "{}";
        s.Base.Originals["B1"] = "{}";
        s.SimProUrl = "http://127.0.0.1:4999/x";
        s.Updates.Repo = repo;
        s.Usb.FirmwareConfirmed = true;
        s.Usb.ScreenRamDrive = true;
        s.Usb.ScreenFlashes.Add(new ScreenFlashRecord { ImageId = "img", Mode = "ramfs", Result = "ok" });
        s.Usb.LibraryUrl = "https://example.invalid/lib";
        s.Usb.TurnedOffSimHubDevice = true;
        return s;
    }

    static string Write(string dir, string name, JObject file)
    {
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, file.ToString());
        return path;
    }

    static string Refusal(string path)
    {
        try { SettingsTransfer.Read(path); return null; } catch (Exception ex) { return ex.Message; }
    }

    public static void Run(string dir)
    {
        string root = Path.Combine(dir, "backup-test"), simhub = Path.Combine(root, "simhub"), data = SettingsTransfer.DataFolder(simhub);
        if (Directory.Exists(root)) Directory.Delete(root, true);
        Directory.CreateDirectory(Path.Combine(data, "Dashes"));
        var now = new DateTime(2026, 10, 12, 20, 0, 0);

        // ----- export -----
        var export = SettingsTransfer.Export(Sample("someone/else"), now);
        var body = (JObject)export["Settings"];
        var left = SettingsTransfer.ThisPcOnly.Where(p => SettingsTransfer.Find(body, p) != null).ToList();
        Check("an export has none of this PC's own fields", left.Count == 0, string.Join(",", left));
        Check("nor which wheel had SimHub's device turned off", SettingsTransfer.Find(body, "Usb.TurnedOffSimHubDevice") == null && body["Usb"]["Wheels"]["gtneo"]["TurnedOffSimHubDevice"] == null);
        Check("an export says its settings format and plugin version", (int)export["SettingsVersion"] == FXProRpmSyncPlugin.CurrentSettingsVersion && (string)export["Format"] == SettingsTransfer.Marker && export["Plugin"] != null);

        var jsonPath = Path.Combine(root, "mine" + SettingsTransfer.JsonExtension);
        SettingsTransfer.ExportTo(jsonPath, export, simhub, false);
        var read = SettingsTransfer.Read(jsonPath);
        var back = read.Settings.ToObject<FXProRpmSyncSettings>();
        Check("a person's own settings come back whole", back.Mode == WheelMode.Unlocked && back.Overrides.ContainsKey("IRacing | bmwm4gt3") && back.Usb.UserLights.Count == 1 && back.Usb.UserLights[0].Name == "Mine"
              && back.Usb.CarDashes["IRacing | bmwm4gt3"].Dashes[0] == "c:bmw-m4-gt3-iracing" && back.Usb.ButtonLeds[4] == 17 && back.Usb.LedCeiling == 55 && back.Usb.Wheels["gtneo"].LightPreset == "x");
        Check("the summary counts them", read.Count("Usb.UserLights") == 1 && read.Count("Usb.CarDashes") == 1 && read.Count("Overrides") == 1 && read.Count("Nothing.Here") == 0);

        // ----- what a file can't do -----
        var edited = (JObject)export.DeepClone();
        edited["Settings"]["Updates"]["Repo"] = "evil/repo";
        edited["Settings"]["SimProUrl"] = "http://evil.invalid";
        edited["Settings"]["Usb"]["FirmwareConfirmed"] = true;
        var fromEdited = SettingsTransfer.Read(Write(root, "edited.json", edited)).Settings;
        Check("a file edited by hand still can't set where updates come from, SimPro's address or the firmware tick",
              SettingsTransfer.Find(fromEdited, "Updates.Repo") == null && fromEdited["SimProUrl"] == null && SettingsTransfer.Find(fromEdited, "Usb.FirmwareConfirmed") == null);

        var newer = (JObject)export.DeepClone();
        newer["SettingsVersion"] = FXProRpmSyncPlugin.CurrentSettingsVersion + 1;
        Check("a file from a newer settings format is refused whole", (Refusal(Write(root, "newer.json", newer)) ?? "").Contains("newer version"));
        var newerFile = (JObject)export.DeepClone();
        newerFile["FormatVersion"] = SettingsTransfer.FileFormat + 1;
        Check("a newer file layout is refused", (Refusal(Write(root, "newer-file.json", newerFile)) ?? "").Contains("newer version"));
        var unknown = (JObject)export.DeepClone();
        unknown["Settings"]["Mode"] = "Hyperspace";
        Check("a value this plugin can't read refuses the file (never half-loaded)", (Refusal(Write(root, "unknown.json", unknown)) ?? "").Contains("can't be read"));
        Check("the plugin's own settings file is not an export", Refusal(Write(root, "raw.json", JObject.FromObject(Sample("a/b")))) != null);
        File.WriteAllText(Path.Combine(root, "junk.json"), "not json {");
        Check("junk is refused", Refusal(Path.Combine(root, "junk.json")) != null && Refusal(Path.Combine(root, "missing.json")) != null);

        // ----- with the dash files -----
        var dash = BuiltInDashes.MustangGt3();
        dash.Id = "my-dash";
        string dashJson = DashTools.Serialize(dash);
        File.WriteAllText(Path.Combine(data, "Dashes", "my-dash.json"), dashJson);
        var zipPath = Path.Combine(root, "mine" + SettingsTransfer.ZipExtension);
        Check("the zip takes the dash files along", SettingsTransfer.ExportTo(zipPath, export, simhub, true) == 1);
        var zipped = SettingsTransfer.Read(zipPath);
        Check("and they come back byte for byte", zipped.Files.Count == 1 && zipped.Files[0].Folder == "Dashes" && zipped.Files[0].Name == "my-dash.json"
              && zipped.Files[0].Data.SequenceEqual(File.ReadAllBytes(Path.Combine(data, "Dashes", "my-dash.json"))) && zipped.Refused.Count == 0);
        Check("the same dash here is no conflict", SettingsTransfer.Conflicts(zipped, simhub).Count == 0);

        var future = BuiltInDashes.MustangGt3();
        future.FormatVersion = DashDefinition.CurrentFormat + 1;
        var badZip = Path.Combine(root, "bad.zip");
        using (var z = ZipFile.Open(badZip, ZipArchiveMode.Create))
        {
            void Add(string name, string text) { using (var w = new StreamWriter(z.CreateEntry(name).Open())) w.Write(text); }
            Add(SettingsTransfer.SettingsEntry, export.ToString());
            Add("Dashes/good.json", dashJson);
            Add("Dashes/../../escape.json", dashJson);
            Add("Dashes/sub/deep.json", dashJson);
            Add("Elsewhere/x.json", dashJson);
            Add("User.FXProRpmSync.dll", "MZ");
            Add("Dashes/run.exe", "MZ");
            Add("Dashes/future.json", DashTools.Serialize(future));
            Add("Dashes/notadash.json", "{\"hello\":1}");
            Add("Dashes/GOOD.json", dashJson);
        }
        var bad = SettingsTransfer.Read(badZip);
        Check("a zip only gives plain dash files in the known folders: the rest is left out, each with why",
              bad.Files.Count == 1 && bad.Files[0].Name == "good.json" && bad.Refused.Count == 8, bad.Files.Count + " taken, " + bad.Refused.Count + " refused");
        var noSettings = Path.Combine(root, "nosettings.zip");
        using (var z = ZipFile.Open(noSettings, ZipArchiveMode.Create)) { using (var w = new StreamWriter(z.CreateEntry("Dashes/good.json").Open())) w.Write(dashJson); }
        Check("a zip without settings is refused", Refusal(noSettings) != null);

        // ----- the next start -----
        var file = SettingsTransfer.SettingsFile(simhub);
        Check("nothing waiting: the start does nothing", SettingsTransfer.ApplyPending(simhub, now) == null && SettingsTransfer.Pending(simhub) == null);
        var mine = Sample("mine/repo");
        mine.Usb.UserLights.Clear();
        mine.Usb.LedCeiling = 90;
        mine.Usb.ActiveWheel = "gtneo";          // SimHub's device was turned off for the GT Neo, the active wheel here
        mine.Usb.Wheels.Clear();
        string before = JObject.FromObject(mine).ToString(Newtonsoft.Json.Formatting.None);
        File.WriteAllText(file, before);
        File.WriteAllText(Path.Combine(data, "Dashes", "my-dash.json"), "{\"Id\":\"my-dash\",\"Elements\":[]}"); // the same name, another dash
        Check("a different dash of the same name is a conflict", SettingsTransfer.Conflicts(zipped, simhub).SequenceEqual(new[] { "Dashes\\my-dash.json" }));

        SettingsTransfer.Stage(zipped, simhub, false, "mine.fxsettings.zip", now);
        Check("staging changes nothing yet", File.ReadAllText(file) == before && SettingsTransfer.Pending(simhub)?.From == "mine.fxsettings.zip");
        SettingsTransfer.CancelPending(simhub);
        Check("an import can be cancelled", SettingsTransfer.Pending(simhub) == null && SettingsTransfer.ApplyPending(simhub, now) == null && File.ReadAllText(file) == before);

        SettingsTransfer.Stage(zipped, simhub, false, "mine.fxsettings.zip", now);
        var said = SettingsTransfer.ApplyPending(simhub, now);
        var after = JObject.Parse(File.ReadAllText(file)).ToObject<FXProRpmSyncSettings>();
        var copy = Path.Combine(data, "settings-backup-before-import-20261012-200000.json");
        Check("the start applies it and says so", said != null && said.StartsWith("Settings imported") && SettingsTransfer.Pending(simhub) == null, said ?? "null");
        Check("the settings from before are copied aside first", File.Exists(copy) && File.ReadAllText(copy) == before);
        Check("the imported settings are in", after.Usb.UserLights.Count == 1 && after.Usb.LedCeiling == 55 && after.Usb.ButtonLeds[4] == 17 && after.Usb.ActiveWheel == "fxpro");
        Check("this PC's own fields stay", after.Updates.Repo == "mine/repo" && after.SimProUrl == "http://127.0.0.1:4999/x" && after.Originals.ContainsKey("P1") && after.Base.Originals.ContainsKey("B1")
              && after.Usb.FirmwareConfirmed && after.Usb.ScreenRamDrive && after.Usb.ScreenFlashes.Count == 1 && after.Usb.LibraryUrl == "https://example.invalid/lib");
        Check("SimHub's device stays turned off for the wheel it was turned off for", !after.Usb.TurnedOffSimHubDevice && after.Usb.Wheels["gtneo"].TurnedOffSimHubDevice);
        Check("a different dash here is kept unless asked", File.ReadAllText(Path.Combine(data, "Dashes", "my-dash.json")).Contains("\"Elements\":[]") && said.Contains("1 of yours kept"));

        SettingsTransfer.Stage(zipped, simhub, true, "mine.fxsettings.zip", now);
        said = SettingsTransfer.ApplyPending(simhub, now.AddMinutes(1));
        var old = Path.Combine(data, "backups", "before-import-20261012-200100", "Dashes", "my-dash.json");
        Check("asked to replace: the import's dash is in, the old one copied aside", File.ReadAllText(Path.Combine(data, "Dashes", "my-dash.json")) == dashJson && File.Exists(old) && File.ReadAllText(old).Contains("\"Elements\":[]"),
              said ?? "null");

        // no settings here yet (a new PC): this PC's fields get their defaults, never the file's
        string fresh = Path.Combine(root, "fresh");
        Directory.CreateDirectory(SettingsTransfer.DataFolder(fresh));
        SettingsTransfer.Stage(zipped, fresh, false, null, now);
        said = SettingsTransfer.ApplyPending(fresh, now);
        var onNew = JObject.Parse(File.ReadAllText(SettingsTransfer.SettingsFile(fresh))).ToObject<FXProRpmSyncSettings>();
        Check("on a PC without settings: imported, dashes added, this PC's fields at their defaults",
              said != null && onNew.Usb.UserLights.Count == 1 && !onNew.Usb.FirmwareConfirmed && onNew.Updates.Repo == Updater.DefaultRepo && onNew.Originals.Count == 0
              && File.Exists(Path.Combine(SettingsTransfer.DataFolder(fresh), "Dashes", "my-dash.json")), said ?? "null");

        // an import that can't be applied leaves everything as it was, once
        string current = File.ReadAllText(file);
        SettingsTransfer.Stage(zipped, simhub, false, "x", now);
        var staged = Path.Combine(data, "import-pending", "settings.json");
        var tampered = JObject.Parse(File.ReadAllText(staged));
        tampered["SettingsVersion"] = FXProRpmSyncPlugin.CurrentSettingsVersion + 1;
        File.WriteAllText(staged, tampered.ToString());
        said = SettingsTransfer.ApplyPending(simhub, now.AddMinutes(2));
        Check("a waiting import for a newer plugin (after a roll back) is not applied", said != null && said.Contains("not applied") && File.ReadAllText(file) == current, said ?? "null");
        Check("and is not tried again", SettingsTransfer.Pending(simhub) == null && SettingsTransfer.ApplyPending(simhub, now.AddMinutes(3)) == null && Directory.Exists(Path.Combine(data, "import-failed-20261012-200200")));

        Directory.CreateDirectory(Path.Combine(data, "import-pending"));
        File.WriteAllText(staged, tampered.ToString());
        Check("a half-written import is never applied", SettingsTransfer.ApplyPending(simhub, now.AddMinutes(4)) == null && File.ReadAllText(file) == current && !Directory.Exists(Path.Combine(data, "import-pending")));
    }
}
