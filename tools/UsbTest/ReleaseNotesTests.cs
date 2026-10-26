using System.Linq;
using User.FXProRpmSync;

/// <summary>A release's notes in the update box (ReleaseNotes.Changes): only its sections of changes, each item whole.</summary>
static class ReleaseNotesTests
{
    static void Check(string name, bool ok, string detail = "") => FeatureTests.Check("release notes: " + name, ok, detail);

    // as release.ps1 wrote them before -ChangesOnly: the disclaimer, an intro, the changes, install, the firmware
    const string Full = @"> FX Unleashed Enhanced (like FX Unleashed) is an independent community project.

## FX Unleashed Enhanced 0.7.62

[FX Unleashed](https://github.com/fxunleashed/fx-unleashed) 0.7.6 by ziadkadry99 with this edition's changes on top.

### Fixed in 0.7.62

- A custom alert's delete button sits on its second row, with its name and condition; on the alert's own row it went past
  the width and dropped under the arrows.
- The own colours' effect box is wide enough.

Everything from 0.7.61 is here. Full list: [CHANGES-ENHANCED.md](https://example.com/CHANGES-ENHANCED.md).

### Install

- Close SimHub, copy the DLL.

### Custom firmware for Unleashed mode

1. [Custom firmware](https://fxunleashed.com/start/)
";

    const string ChangesOnly = @"### New

- **Update notes in colour.** Only what changed.

### Fixed

- One.
- Two.

### Improved

- Three.

Install and the rest: [README](https://example.com).
";

    public static void Run()
    {
        var full = ReleaseNotes.Changes(Full);
        Check("only the sections of changes", full.Count == 1 && full[0].Kind == ReleaseNotes.Kind.Fixed && full[0].Title == "Fixed in 0.7.62",
              string.Join(", ", full.Select(s => s.Title)));
        Check("an item that goes on a second line stays one item", full.Count == 1 && full[0].Items.Count == 2 &&
              full[0].Items[0].EndsWith("went past the width and dropped under the arrows."), full.Count == 1 ? string.Join(" | ", full[0].Items) : "");
        Check("a paragraph after the list isn't an item", full.Count == 1 && full[0].Items.All(i => !i.Contains("Everything from")));

        var only = ReleaseNotes.Changes(ChangesOnly);
        Check("each kind in its order", string.Join(",", only.Select(s => s.Kind)) == "New,Fixed,Changed", string.Join(",", only.Select(s => s.Kind)));
        Check("the footer isn't part of the last section", only.Count == 3 && only[2].Items.Count == 1 && only[2].Items[0] == "Three.");
        Check("notes with no sections of changes", ReleaseNotes.Changes("Some text.\n\n## Install\n\n- copy it").Count == 0 && ReleaseNotes.Changes(null).Count == 0);
    }
}
