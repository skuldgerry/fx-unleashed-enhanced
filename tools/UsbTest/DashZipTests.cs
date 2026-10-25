using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using User.FXProRpmSync;

/// <summary>A zip of dashes (DashShare.Unzip): what is taken from it, under which names, and what is refused.</summary>
static class DashZipTests
{
    static void Check(string name, bool ok, string detail = "") => FeatureTests.Check("dash zip: " + name, ok, detail);

    static void Add(ZipArchive z, string name, byte[] data)
    {
        using (var s = z.CreateEntry(name).Open()) s.Write(data, 0, data.Length);
    }

    public static void Run(string dir)
    {
        string root = Path.Combine(dir, "dash-zip-test"), into = Path.Combine(root, "unpacked");
        if (Directory.Exists(root)) Directory.Delete(root, true);
        Directory.CreateDirectory(root);
        var dash = Encoding.UTF8.GetBytes(DashTools.Serialize(BundledDashes.All().First()));

        string zip = Path.Combine(root, "pack.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            Add(z, "all-dashes/one.fxdash.json", dash);
            Add(z, "more/one.fxdash.json", dash);                                  // the same name in another folder
            Add(z, "../outside.json", Encoding.UTF8.GetBytes("{}"));               // a path that leaves the folder
            Add(z, "readme.txt", Encoding.UTF8.GetBytes("not a dash"));
            Add(z, "big.json", new byte[LibraryClient.MaxDashBytes + 10]);
        }
        var refused = new List<string>();
        var files = DashShare.Unzip(zip, into, refused);
        var names = files.Select(Path.GetFileName).ToList();
        Check("takes the json files, each under its own name", names.SequenceEqual(new[] { "one.fxdash.json", "one.fxdash-2.json", "outside.json" }), string.Join(", ", names));
        Check("everything lands in its folder", files.All(f => Path.GetDirectoryName(f) == into) && !File.Exists(Path.Combine(root, "outside.json")));
        Check("a dash from it reads like any dash file", DashShare.Read(files[0]).Elements.Count > 0);
        Check("too big is refused, with the size", refused.Count == 1 && refused[0].Contains("big.json") && refused[0].Contains("too big"), string.Join(" | ", refused));

        string bad = Path.Combine(root, "bad.zip");
        File.WriteAllText(bad, "not a zip at all");
        refused.Clear();
        Check("a broken zip is refused, nothing taken", DashShare.Unzip(bad, Path.Combine(root, "bad"), refused).Count == 0 && refused.Count == 1 && refused[0].Contains("can't be read"), string.Join(" | ", refused));

        string empty = Path.Combine(root, "empty.zip");
        using (var z = ZipFile.Open(empty, ZipArchiveMode.Create)) Add(z, "notes.txt", Encoding.UTF8.GetBytes("x"));
        refused.Clear();
        Check("a zip with no dash in it says so", DashShare.Unzip(empty, Path.Combine(root, "empty"), refused).Count == 0 && refused.Count == 1 && refused[0].Contains("no dash files"), string.Join(" | ", refused));
    }
}
