using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace User.FXProRpmSync
{
    /// <summary>
    /// The settings as one file: a copy to keep, or to take to another PC (About tab, "Backup").
    ///
    /// The file carries what a person set up (lights, alerts, dashes per car, car tuning, buttons, options) and the
    /// settings' format (SettingsVersion). What belongs to one PC and one wheel is never written and never read from a
    /// file (<see cref="ThisPcOnly"/>): the firmware and screen state, SimPro's captured presets, and where updates,
    /// the library and SimPro are reached (a file from someone else must not be able to point the updater elsewhere).
    /// With the dash files it is a zip: the settings plus Dashes\ and Savers\.
    ///
    /// An import never changes the running plugin. It is checked whole (a file written by a newer plugin is refused,
    /// as everywhere), then kept in the data folder until the next start (<see cref="ApplyPending"/>), where the
    /// current settings file is copied aside, this PC's own fields are carried over, and the result goes through the
    /// same migrations as any settings file.
    /// </summary>
    public static class SettingsTransfer
    {
        public const string Marker = "fx-unleashed-settings";
        /// <summary>The file's own layout (not the settings' format, which is SettingsVersion).</summary>
        public const int FileFormat = 1;
        public const string JsonExtension = ".fxsettings.json", ZipExtension = ".fxsettings.zip";
        public const string SettingsEntry = "settings" + JsonExtension;
        public const int MaxSettingsBytes = 8 * 1024 * 1024, MaxFiles = 500;
        /// <summary>The folders of the data folder whose files travel with the settings.</summary>
        public static readonly string[] FileFolders = { "Dashes", "Savers" };

        private const string PendingFolder = "import-pending", PendingSettings = "settings.json", PendingInfo = "import.json";

        /// <summary>
        /// Settings that stay with the PC and the wheel they were made on (dotted paths into the settings file). Left out
        /// of an export, ignored in an imported file, and kept as they are here when an import is applied.
        /// (Usb.TurnedOffSimHubDevice is one more, kept per wheel: see <see cref="Merge"/>.)
        /// </summary>
        public static readonly string[] ThisPcOnly =
        {
            // what SimPro's presets were before the plugin touched them (preset ids of this PC)
            "Originals", "ScreensOriginals", "Base.Originals",
            // the wheel's firmware and the screen's state
            "Usb.FirmwareConfirmed", "Usb.ScreenFlashes", "Usb.ScreenCheck", "Usb.ScreenCheckWhen", "Usb.ScreenFirmwareAck",
            "Usb.ScreenRamDrive", "Usb.ScreenRam",
            // where things are reached from, and the local server
            "SimProUrl", "Updates.Repo", "Usb.LibraryUrl", "Usb.DesignerServer", "Usb.DesignerPort",
            // what was accepted, installed and seen here
            "UnleashedAccepted", "Usb.LibraryTermsAccepted", "Usb.LibraryInstalled", "Usb.LastSession",
            "Updates.LastCheckUtc", "Updates.SkippedVersion",
        };

        private const string DeviceFlag = "TurnedOffSimHubDevice";

        /// <summary>A file of <see cref="FileFolders"/> that travels with the settings.</summary>
        public sealed class PackedFile
        {
            public string Folder, Name;
            public byte[] Data;
        }

        /// <summary>A settings file that was read and checked: nothing of it is in use yet.</summary>
        public sealed class Package
        {
            /// <summary>The settings, without <see cref="ThisPcOnly"/>.</summary>
            public JObject Settings;
            public int SettingsVersion;
            public string Plugin;
            public DateTime ExportedUtc;
            public List<PackedFile> Files = new List<PackedFile>();
            /// <summary>Files in the zip that were left out, each with why.</summary>
            public List<string> Refused = new List<string>();

            public int Count(string path) => (Find(Settings, path) as JContainer)?.Count ?? 0;
        }

        /// <summary>An import waiting for the next start.</summary>
        public sealed class PendingImport
        {
            public string From;
            public DateTime StagedUtc;
            /// <summary>A dash file here with the same name and other content is replaced (the old one is copied aside).</summary>
            public bool ReplaceFiles;
        }

        public static string DataFolder(string simHubFolder) => Path.Combine(simHubFolder, "PluginsData", "Common", "FXProRpmSync");
        public static string SettingsFile(string simHubFolder) => Path.Combine(simHubFolder, "PluginsData", "Common", "FXProRpmSyncPlugin.GeneralSettings.json");

        // ---------- export ----------

        /// <summary>The settings as an export file's content.</summary>
        public static JObject Export(FXProRpmSyncSettings s, DateTime nowUtc)
        {
            var body = JObject.FromObject(s);
            Strip(body);
            return new JObject
            {
                ["Format"] = Marker, ["FormatVersion"] = FileFormat, ["SettingsVersion"] = s.SettingsVersion,
                ["Plugin"] = Updater.CurrentVersion, ["ExportedUtc"] = nowUtc, ["Settings"] = body,
            };
        }

        /// <summary>
        /// Writes the export to <paramref name="path"/>: a JSON file, or with <paramref name="withFiles"/> a zip with the
        /// dash and screensaver files next to it. Returns how many files went in.
        /// </summary>
        public static int ExportTo(string path, JObject export, string simHubFolder, bool withFiles)
        {
            var json = Encoding.UTF8.GetBytes(export.ToString(Formatting.Indented));
            if (!withFiles) { File.WriteAllBytes(path, json); return 0; }
            int files = 0;
            var tmp = path + ".tmp";
            if (File.Exists(tmp)) File.Delete(tmp);
            using (var z = ZipFile.Open(tmp, ZipArchiveMode.Create))
            {
                using (var es = z.CreateEntry(SettingsEntry).Open()) es.Write(json, 0, json.Length);
                foreach (var folder in FileFolders)
                {
                    var dir = Path.Combine(DataFolder(simHubFolder), folder);
                    if (!Directory.Exists(dir)) continue;
                    foreach (var file in Directory.GetFiles(dir, "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
                    {
                        z.CreateEntryFromFile(file, folder + "/" + Path.GetFileName(file));
                        files++;
                    }
                }
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
            return files;
        }

        // ---------- reading a file ----------

        /// <summary>
        /// Reads and checks an exported file (JSON or zip). Throws with a reason a person can act on; nothing is written.
        /// </summary>
        public static Package Read(string path)
        {
            FileInfo info;
            try { info = new FileInfo(path); } catch (Exception) { throw new Exception("that isn't a usable file name"); }
            if (!info.Exists) throw new Exception("the file isn't there any more");
            if (!IsZip(path))
            {
                if (info.Length > MaxSettingsBytes) throw new Exception($"too big for a settings file ({info.Length / 1024} KB)");
                return Parse(File.ReadAllBytes(path));
            }
            try
            {
                using (var z = ZipFile.OpenRead(path))
                {
                    var entry = z.Entries.FirstOrDefault(e => string.Equals(e.FullName, SettingsEntry, StringComparison.OrdinalIgnoreCase));
                    if (entry == null) throw new Exception("this zip has no FX Unleashed settings in it");
                    var p = Parse(ReadEntry(entry, MaxSettingsBytes) ?? throw new Exception("the settings in this zip are too big"));
                    foreach (var e in z.Entries)
                    {
                        if (e == entry || e.FullName.EndsWith("/")) continue;
                        if (p.Files.Count >= MaxFiles) { p.Refused.Add(e.FullName + ": more files than an export has"); continue; }
                        var why = TakeFile(e, p.Files);
                        if (why != null) p.Refused.Add(e.FullName + ": " + why);
                    }
                    return p;
                }
            }
            catch (InvalidDataException) { throw new Exception("this zip can't be read (damaged, or not a zip)"); }
        }

        private static bool IsZip(string path)
        {
            using (var f = File.OpenRead(path)) return f.ReadByte() == 'P' && f.ReadByte() == 'K';
        }

        /// <summary>The entry's bytes, null when it's larger than <paramref name="max"/> (whatever its header says).</summary>
        private static byte[] ReadEntry(ZipArchiveEntry e, int max)
        {
            if (e.Length > max) return null;
            using (var s = e.Open())
            using (var m = new MemoryStream())
            {
                var buffer = new byte[81920];
                int n;
                while ((n = s.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (m.Length + n > max) return null;
                    m.Write(buffer, 0, n);
                }
                return m.ToArray();
            }
        }

        /// <summary>Checks one file of the zip (a plain name in a known folder, a dash by the library's rules); why not, or null when it was taken.</summary>
        private static string TakeFile(ZipArchiveEntry e, List<PackedFile> into)
        {
            var parts = e.FullName.Split('/');
            var folder = parts.Length == 2 ? FileFolders.FirstOrDefault(f => string.Equals(f, parts[0], StringComparison.OrdinalIgnoreCase)) : null;
            if (folder == null) return "not a dash or screensaver file";
            var name = parts[1];
            if (name.Length == 0 || name.Length > 120 || name != Path.GetFileName(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                name.StartsWith(".") || !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                return "not a usable file name";
            if (into.Any(f => f.Folder == folder && string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))) return "there twice";
            var data = ReadEntry(e, LibraryClient.MaxDashBytes);
            if (data == null) return $"too big (the limit is {LibraryClient.MaxDashBytes / 1024} KB)";
            DashDefinition d;
            try { d = JsonConvert.DeserializeObject<DashDefinition>(Encoding.UTF8.GetString(data)); }
            catch (Exception) { return "not a file the plugin wrote"; }
            if (d == null) return "not a file the plugin wrote";
            // a dash gets the rules of any dash that comes from a file; a screensaver's picture data has no elements and nothing to run
            if (folder == "Dashes" || d.Elements != null)
            {
                var problems = LibraryClient.Check(d, data.Length);
                if (problems.Count > 0) return string.Join("; ", problems);
            }
            into.Add(new PackedFile { Folder = folder, Name = name, Data = data });
            return null;
        }

        private static Package Parse(byte[] bytes)
        {
            JObject file;
            try { file = JObject.Parse(Encoding.UTF8.GetString(bytes).TrimStart('﻿')); }
            catch (Exception) { throw new Exception("this isn't an FX Unleashed settings file"); }
            if ((string)file["Format"] != Marker || !(file["Settings"] is JObject body))
                throw new Exception("this isn't an FX Unleashed settings file (export one from the About tab)");
            string plugin = (string)file["Plugin"];
            string newer = "it was saved by a newer version of the plugin" + (plugin != null ? " (v" + plugin + ")" : "") + ": update this one first (About tab)";
            if (((int?)file["FormatVersion"] ?? 0) > FileFormat) throw new Exception(newer);
            int version = (int?)file["SettingsVersion"] ?? (int?)body["SettingsVersion"] ?? 0;
            if (version > FXProRpmSyncPlugin.CurrentSettingsVersion) throw new Exception(newer);
            Strip(body);
            body["SettingsVersion"] = version;
            // everything in it must load as settings: one value this plugin can't read would lose the whole file at the next start
            try { if (body.ToObject<FXProRpmSyncSettings>() == null) throw new Exception("empty"); }
            catch (Exception ex) { throw new Exception("the settings in it can't be read (" + ex.Message + ")"); }
            DateTime exported = DateTime.MinValue;
            try { exported = (DateTime?)file["ExportedUtc"] ?? DateTime.MinValue; } catch (Exception) { }
            return new Package { Settings = body, SettingsVersion = version, Plugin = plugin, ExportedUtc = exported };
        }

        // ---------- this PC's own fields ----------

        internal static JToken Find(JObject root, string path)
        {
            JToken t = root;
            foreach (var part in path.Split('.'))
            {
                t = (t as JObject)?[part];
                if (t == null) return null;
            }
            return t;
        }

        private static void Remove(JObject root, string path)
        {
            int dot = path.LastIndexOf('.');
            var parent = dot < 0 ? root : Find(root, path.Substring(0, dot)) as JObject;
            parent?.Remove(path.Substring(dot + 1));
        }

        private static void Set(JObject root, string path, JToken value)
        {
            var parts = path.Split('.');
            var o = root;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (!(o[parts[i]] is JObject next)) o[parts[i]] = next = new JObject();
                o = next;
            }
            o[parts[parts.Length - 1]] = value.DeepClone();
        }

        /// <summary>Takes this PC's own fields out of a settings object.</summary>
        public static void Strip(JObject settings)
        {
            foreach (var path in ThisPcOnly) Remove(settings, path);
            Remove(settings, "Usb." + DeviceFlag);
            if (Find(settings, "Usb.Wheels") is JObject wheels)
                foreach (var w in wheels.Properties()) (w.Value as JObject)?.Remove(DeviceFlag);
        }

        /// <summary>
        /// The settings file an import leaves: the imported settings with this PC's own fields as they are in
        /// <paramref name="current"/> (null = no settings here yet: those fields get their defaults).
        /// </summary>
        public static JObject Merge(JObject imported, JObject current)
        {
            var merged = (JObject)imported.DeepClone();
            Strip(merged);
            if (current == null) return merged;
            foreach (var path in ThisPcOnly)
            {
                var mine = Find(current, path);
                if (mine != null) Set(merged, path, mine);
            }
            // "the plugin turned SimHub's own device off" belongs to a wheel on this PC, and the two files may not have
            // the same wheel active: read it per wheel here, put it per wheel there
            string defaultWheel = new UsbSettings().ActiveWheel;
            var turnedOff = new HashSet<string>();
            if ((bool?)Find(current, "Usb." + DeviceFlag) == true) turnedOff.Add((string)Find(current, "Usb.ActiveWheel") ?? defaultWheel);
            if (Find(current, "Usb.Wheels") is JObject mineWheels)
                foreach (var w in mineWheels.Properties())
                    if ((bool?)(w.Value as JObject)?[DeviceFlag] == true) turnedOff.Add(w.Name);
            if (turnedOff.Count == 0) return merged;
            if (turnedOff.Contains((string)Find(merged, "Usb.ActiveWheel") ?? defaultWheel)) Set(merged, "Usb." + DeviceFlag, true);
            if (Find(merged, "Usb.Wheels") is JObject wheels)
                foreach (var w in wheels.Properties())
                    if (turnedOff.Contains(w.Name) && w.Value is JObject o) o[DeviceFlag] = true;
            return merged;
        }

        // ---------- import: kept for the next start ----------

        /// <summary>The files of the package that would replace a different file of the same name here.</summary>
        public static List<string> Conflicts(Package p, string simHubFolder)
        {
            var list = new List<string>();
            foreach (var f in p.Files)
            {
                var target = Path.Combine(DataFolder(simHubFolder), f.Folder, f.Name);
                try { if (File.Exists(target) && !File.ReadAllBytes(target).SequenceEqual(f.Data)) list.Add(f.Folder + "\\" + f.Name); }
                catch (Exception) { list.Add(f.Folder + "\\" + f.Name); }
            }
            return list;
        }

        /// <summary>Keeps a checked package for the next start (replacing one that was already waiting).</summary>
        public static void Stage(Package p, string simHubFolder, bool replaceFiles, string from, DateTime nowUtc)
        {
            var data = DataFolder(simHubFolder);
            string pending = Path.Combine(data, PendingFolder), tmp = pending + ".tmp";
            if (Directory.Exists(tmp)) Directory.Delete(tmp, true);
            Directory.CreateDirectory(tmp);
            File.WriteAllText(Path.Combine(tmp, PendingSettings), p.Settings.ToString(Formatting.None), new UTF8Encoding(false));
            foreach (var f in p.Files)
            {
                Directory.CreateDirectory(Path.Combine(tmp, f.Folder));
                File.WriteAllBytes(Path.Combine(tmp, f.Folder, f.Name), f.Data);
            }
            // written last: a folder without it is an unfinished one and is never applied
            File.WriteAllText(Path.Combine(tmp, PendingInfo), JsonConvert.SerializeObject(new PendingImport { From = from, StagedUtc = nowUtc, ReplaceFiles = replaceFiles }));
            if (Directory.Exists(pending)) Directory.Delete(pending, true);
            Directory.Move(tmp, pending);
        }

        /// <summary>The import waiting for the next start, or null.</summary>
        public static PendingImport Pending(string simHubFolder)
        {
            try
            {
                var info = Path.Combine(DataFolder(simHubFolder), PendingFolder, PendingInfo);
                return File.Exists(info) ? JsonConvert.DeserializeObject<PendingImport>(File.ReadAllText(info)) : null;
            }
            catch (Exception) { return null; }
        }

        public static void CancelPending(string simHubFolder)
        {
            var pending = Path.Combine(DataFolder(simHubFolder), PendingFolder);
            if (Directory.Exists(pending)) Directory.Delete(pending, true);
        }

        // ---------- import: applied at start-up ----------

        /// <summary>
        /// Applies the import that is waiting, before the settings are read (FXProRpmSyncPlugin.InitCore). The settings
        /// file here is copied aside first; without that copy nothing is changed. Returns what happened in a sentence
        /// (null = nothing was waiting). Never throws.
        /// </summary>
        public static string ApplyPending(string simHubFolder, DateTime now)
        {
            string data = DataFolder(simHubFolder), pending = Path.Combine(data, PendingFolder);
            try
            {
                if (!Directory.Exists(pending)) return null;
                var info = Pending(simHubFolder);
                if (info == null) { SetAside(pending, data, now); return null; } // unfinished: never applied
                string stamp = now.ToString("yyyyMMdd-HHmmss");
                try
                {
                    var imported = JObject.Parse(File.ReadAllText(Path.Combine(pending, PendingSettings)));
                    int version = (int?)imported["SettingsVersion"] ?? 0;
                    if (version > FXProRpmSyncPlugin.CurrentSettingsVersion)
                        throw new Exception($"it needs a newer version of the plugin (settings format {version})");

                    var file = SettingsFile(simHubFolder);
                    JObject current = null;
                    string copy = null;
                    if (File.Exists(file))
                    {
                        copy = Path.Combine(data, $"settings-backup-before-import-{stamp}.json");
                        File.Copy(file, copy, true);
                        try { current = JObject.Parse(File.ReadAllText(file)); } catch (Exception) { } // unreadable: this PC's fields start again
                    }
                    var merged = Merge(imported, current);

                    // files first: a dash nobody uses is harmless, settings that name a missing dash are not
                    int added = 0, replaced = 0, kept = 0;
                    foreach (var folder in FileFolders)
                    {
                        var from = Path.Combine(pending, folder);
                        if (!Directory.Exists(from)) continue;
                        Directory.CreateDirectory(Path.Combine(data, folder));
                        foreach (var f in Directory.GetFiles(from, "*.json"))
                        {
                            var target = Path.Combine(data, folder, Path.GetFileName(f));
                            if (!File.Exists(target)) { File.Copy(f, target); added++; continue; }
                            if (File.ReadAllBytes(target).SequenceEqual(File.ReadAllBytes(f))) continue;
                            if (!info.ReplaceFiles) { kept++; continue; }
                            var old = Path.Combine(data, "backups", "before-import-" + stamp, folder);
                            Directory.CreateDirectory(old);
                            File.Copy(target, Path.Combine(old, Path.GetFileName(f)), true);
                            File.Copy(f, target, true);
                            replaced++;
                        }
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(file));
                    var tmp = file + ".import";
                    File.WriteAllText(tmp, merged.ToString(Formatting.None), new UTF8Encoding(false));
                    if (File.Exists(file)) File.Delete(file);
                    File.Move(tmp, file);
                    Directory.Delete(pending, true);

                    var said = "Settings imported" + (string.IsNullOrEmpty(info.From) ? "" : " from " + info.From) +
                               (added + replaced + kept > 0 ? $" ({added} dash file{(added == 1 ? "" : "s")} added" + (replaced > 0 ? $", {replaced} replaced" : "") +
                                                              (kept > 0 ? $", {kept} of yours kept" : "") + ")" : "") +
                               (copy != null ? ". The settings from before are in " + copy : "") + ".";
                    SimHub.Logging.Current.Info("[FXProRpmSync] " + said);
                    return said;
                }
                catch (Exception ex)
                {
                    SetAside(pending, data, now);
                    var said = "The settings import was not applied: " + ex.Message + ". Your settings were not changed.";
                    SimHub.Logging.Current.Warn("[FXProRpmSync] " + said);
                    return said;
                }
            }
            catch (Exception ex)
            {
                try { SimHub.Logging.Current.Warn("[FXProRpmSync] settings import: " + ex.Message); } catch (Exception) { }
                return null;
            }
        }

        /// <summary>An import that can't be applied leaves the way, so it isn't tried again at every start.</summary>
        private static void SetAside(string pending, string data, DateTime now)
        {
            try { Directory.Move(pending, Path.Combine(data, "import-failed-" + now.ToString("yyyyMMdd-HHmmss"))); }
            catch (Exception) { try { Directory.Delete(pending, true); } catch (Exception) { } }
        }
    }
}
