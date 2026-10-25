using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace User.FXProRpmSync
{
    /// <summary>
    /// Sharing a dash as one file (<c>&lt;id&gt;.fxdash.json</c>, images inside) and taking one in. A dash from a file gets
    /// the library's rules (format, size, checked scripts) and never overwrites anything: if its id is taken it gets a new one.
    /// Links to library items (<see cref="LinkFor"/>) are the other way to share; see docs/dash-format.md "Sharing".
    /// </summary>
    public static class DashShare
    {
        public const string Extension = ".fxdash.json";
        public const string LibraryLinkBase = "https://fxunleashed.com/library/#";

        public sealed class Imported
        {
            public DashDefinition Dash;
            public string File;
            /// <summary>Its id was taken, so it was saved under another one.</summary>
            public bool Renamed;
            /// <summary>The same dash is already installed: nothing was written.</summary>
            public bool AlreadyThere;
        }

        /// <summary>The permanent page of a library item (kind "dash" or "saver"): preview and an Install button.</summary>
        public static string LinkFor(string kind, string id) => LibraryLinkBase + (kind == "saver" ? "saver" : "dash") + "-" + id;

        /// <summary>Lower case letters, digits and dashes, 2-64 characters; null when nothing usable is left.</summary>
        public static string Slug(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var s = Regex.Replace(text.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            if (s.Length > 64) s = s.Substring(0, 64).Trim('-');
            return s.Length >= 2 ? s : null;
        }

        public static string FileName(string id) => (Slug(id) ?? "dash") + Extension;

        /// <summary>Most dash files taken from one zip.</summary>
        public const int MaxZipEntries = 200;

        public static bool IsZip(string path) => path != null && path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The dash files in a zip (a pack of dashes), written into <paramref name="folder"/> to be imported one by one like
        /// any dash file. Only <c>.json</c> files are taken, each under its own name (never the path it has in the zip),
        /// within the dash size limit; what is left out goes into <paramref name="refused"/> with why. Nothing is installed here.
        /// </summary>
        public static List<string> Unzip(string zipPath, string folder, List<string> refused)
        {
            var taken = new List<string>();
            string zipName = Path.GetFileName(zipPath);
            Directory.CreateDirectory(folder);
            try
            {
                using (var z = System.IO.Compression.ZipFile.OpenRead(zipPath))
                {
                    foreach (var e in z.Entries)
                    {
                        if (e.FullName.EndsWith("/") || e.FullName.EndsWith("\\")) continue;
                        string name = Path.GetFileName(e.FullName.Replace('\\', '/').Split('/').Last());
                        if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;   // pictures, readmes: not dashes
                        string shown = zipName + " › " + name;
                        if (taken.Count >= MaxZipEntries) { refused.Add(shown + ": more dash files than one zip may bring (" + MaxZipEntries + ")"); continue; }
                        if (name.Length > 120 || name.StartsWith(".") || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) { refused.Add(shown + ": not a usable file name"); continue; }
                        if (e.Length > LibraryClient.MaxDashBytes) { refused.Add(shown + $": too big ({(e.Length + 1023) / 1024} KB; the limit is {LibraryClient.MaxDashBytes / 1024} KB)"); continue; }
                        // two files of the same name in different folders of the zip: the second gets a number
                        string target = Path.Combine(folder, name);
                        for (int n = 2; File.Exists(target); n++) target = Path.Combine(folder, Path.GetFileNameWithoutExtension(name) + "-" + n + ".json");
                        using (var from = e.Open())
                        using (var to = File.Create(target))
                        {
                            // the size the zip claims isn't trusted: stop at the limit whatever it says
                            var buffer = new byte[81920]; long total = 0; int read;
                            while ((read = from.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                total += read;
                                if (total > LibraryClient.MaxDashBytes) break;
                                to.Write(buffer, 0, read);
                            }
                            if (total > LibraryClient.MaxDashBytes) { to.Dispose(); File.Delete(target); refused.Add(shown + ": too big"); continue; }
                        }
                        taken.Add(target);
                    }
                }
                if (taken.Count == 0 && !refused.Any(r => r.StartsWith(zipName + " › "))) refused.Add(zipName + ": no dash files in it");
            }
            catch (InvalidDataException) { refused.Add(zipName + ": can't be read (damaged, or not a zip)"); }
            catch (IOException ex) { refused.Add(zipName + ": " + ex.Message); }
            return taken;
        }

        /// <summary>
        /// Reads a dash file and checks it with the library's rules. Throws with a reason a person can act on.
        /// Nothing is written.
        /// </summary>
        public static DashDefinition Read(string path)
        {
            FileInfo info;
            try { info = new FileInfo(path); } catch (Exception) { throw new Exception("that isn't a usable file name"); }
            if (!info.Exists) throw new Exception("the file isn't there any more");
            if (info.Length > LibraryClient.MaxDashBytes)
                throw new Exception($"too big ({info.Length / 1024} KB; the limit is {LibraryClient.MaxDashBytes / 1024} KB)");
            var bytes = File.ReadAllBytes(path);
            DashDefinition d;
            try { d = JsonConvert.DeserializeObject<DashDefinition>(Encoding.UTF8.GetString(bytes)); }
            catch (Exception) { throw new Exception("this isn't a dash file"); }
            if (d == null) throw new Exception("this isn't a dash file");
            var problems = LibraryClient.Check(d, bytes.Length);
            if (problems.Count > 0) throw new Exception(string.Join("; ", problems));
            return d;
        }

        /// <summary>Reads a dash file and saves it into the dashes folder. Returns what was done.</summary>
        public static Imported Import(string path)
        {
            var d = Read(path);
            // library installs own the "lib-" ids (their updates replace them), so a shared copy never takes one
            string id = Slug(d.Id) ?? Slug(d.Name) ?? Slug(Path.GetFileName(path).Replace(Extension, "")) ?? "shared-dash";
            if (id.StartsWith("lib-") && id.Length > 6) id = id.Substring(4);
            d.Name = string.IsNullOrWhiteSpace(d.Name) ? id : d.Name.Trim();
            d.ScriptsFolder = null;
            d.FormatVersion = d.RequiredFormat;
            d.Source = string.IsNullOrWhiteSpace(d.Source) ? "Imported from a file" : d.Source;

            var existing = DashLibrary.Load(null);
            var same = existing.FirstOrDefault(x => !x.BuiltIn && x.Id == id && Fingerprint(x) == Fingerprint(d));
            if (same != null) return new Imported { Dash = same, File = same.FilePath, AlreadyThere = true };

            string unique = id; int n = 2;
            while (existing.Any(x => x.Id == unique)) unique = id + "-" + n++;
            d.Id = unique;
            return new Imported { Dash = d, File = DashTools.Save(d), Renamed = unique != id };
        }

        /// <summary>
        /// Writes a dash as a shareable file at <paramref name="path"/> (".fxdash.json" is added when missing). Refuses what
        /// the receiver's plugin would refuse, so a file that can't be imported is never made.
        /// </summary>
        public static string Export(DashDefinition d, string path)
        {
            var copy = d.Clone();
            copy.FormatVersion = copy.RequiredFormat;
            copy.ScriptsFolder = null;
            if (!path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)) path = Regex.Replace(path, @"(\.json)?$", "", RegexOptions.IgnoreCase) + Extension;
            var json = DashTools.Serialize(copy);
            var problems = LibraryClient.Check(copy, Encoding.UTF8.GetByteCount(json));
            if (problems.Count > 0) throw new Exception("this dash can't be shared as a file: " + string.Join("; ", problems));
            File.WriteAllText(path, json, new UTF8Encoding(false));
            return path;
        }

        /// <summary>What makes two dashes the same one: everything but where it came from and the id it was saved under.</summary>
        private static string Fingerprint(DashDefinition d)
        {
            var c = d.Clone();
            c.Id = null; c.Source = null; c.FormatVersion = 0;
            return DashTools.Serialize(c);
        }
    }
}
