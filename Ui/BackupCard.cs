using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace User.FXProRpmSync
{
    /// <summary>
    /// About tab, "Backup": the settings as one file to keep or to take to another PC, and back in (SettingsTransfer).
    /// An import is only checked and kept here; it is applied when SimHub starts next, so the card also shows the one
    /// that is waiting (with a way out of it) and what the last start did with one.
    /// </summary>
    public class BackupCard : StackPanel
    {
        private const string Title = "FX Unleashed";
        private readonly FXProRpmSyncPlugin plugin;
        private readonly TextBlock waiting, done;
        private readonly Button restart, cancel;
        private bool withFiles = true;
        private static string SimHubFolder => DashLibrary.SimHubFolder;

        public BackupCard(FXProRpmSyncPlugin plugin)
        {
            this.plugin = plugin;
            Children.Add(Theme.Eyebrow("Backup"));
            Children.Add(Theme.Note("Your lights and alerts, the dashes of each car, car tuning, buttons and options in one file: a copy to keep, or to " +
                                    "take to another PC. What belongs to this PC and this wheel (the firmware and screen state, the presets captured " +
                                    "from SimPro, where updates come from) is never in the file, and an import never changes it."));
            Children.Add(Theme.Switch("Include my dash and screensaver files (a .zip)", withFiles, v => withFiles = v,
                                      "Without them the file only names the dashes: on another PC a car whose dash isn't there shows the default one."));
            var buttons = new WrapPanel();
            buttons.Children.Add(Theme.Btn("Export settings…", Export));
            buttons.Children.Add(Theme.Btn("Import settings…", Import));
            Children.Add(buttons);

            waiting = Theme.Note("", new Thickness(0, 6, 0, 8));
            waiting.Foreground = Theme.Green;
            Children.Add(waiting);
            var pendingButtons = new WrapPanel();
            restart = Theme.Btn("Restart SimHub", () => plugin.Updates?.RestartSimHub(), primary: true);
            cancel = Theme.Btn("Cancel the import", CancelImport);
            pendingButtons.Children.Add(restart);
            pendingButtons.Children.Add(cancel);
            Children.Add(pendingButtons);
            done = Theme.Note("", new Thickness(0, 6, 0, 0));
            Children.Add(done);
            Refresh();
        }

        private void Refresh()
        {
            var p = SettingsTransfer.Pending(SimHubFolder);
            waiting.Text = p == null ? "" : "An import is waiting" + (string.IsNullOrEmpty(p.From) ? "" : " (" + p.From + ")") +
                                            ": it is applied when SimHub starts next. Until then nothing has changed.";
            waiting.Visibility = restart.Visibility = cancel.Visibility = p != null ? Visibility.Visible : Visibility.Collapsed;
            done.Text = plugin.ImportedAtStart ?? "";
            done.Visibility = done.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Export()
        {
            var owner = Window.GetWindow(this);
            try
            {
                if (plugin.Settings == null) throw new Exception("the plugin didn't start normally, so there are no settings to export");
                string ext = withFiles ? SettingsTransfer.ZipExtension : SettingsTransfer.JsonExtension;
                var dlg = new SaveFileDialog
                {
                    Title = "Export the FX Unleashed settings",
                    FileName = "fx-unleashed-settings-" + DateTime.Now.ToString("yyyy-MM-dd") + ext,
                    Filter = withFiles ? "FX Unleashed settings and dashes (*.fxsettings.zip)|*.fxsettings.zip" : "FX Unleashed settings (*.fxsettings.json)|*.fxsettings.json",
                    AddExtension = false,
                    OverwritePrompt = true,
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                };
                if (dlg.ShowDialog(owner) != true) return;
                var path = dlg.FileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? dlg.FileName : dlg.FileName + ext;
                JObject export;
                lock (plugin.SyncRoot) export = SettingsTransfer.Export(plugin.Settings, DateTime.UtcNow);
                int files = SettingsTransfer.ExportTo(path, export, SimHubFolder, withFiles);
                MessageBox.Show(owner, "Saved as\n" + path + "\n\n" +
                                       (withFiles ? $"{files} dash and screensaver file{(files == 1 ? "" : "s")} inside. Dashes made by other people stay theirs: pass the file on only if they allow it."
                                                  : "Only the settings: the dash files themselves are not in it."),
                                Title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show(owner, "Not exported: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
        }

        private void Import()
        {
            var owner = Window.GetWindow(this);
            var dlg = new OpenFileDialog
            {
                Title = "Import FX Unleashed settings",
                Filter = "FX Unleashed settings (*.fxsettings.json;*.fxsettings.zip)|*.fxsettings.json;*.fxsettings.zip|All files|*.*",
                CheckFileExists = true,
            };
            if (dlg.ShowDialog(owner) != true) return;
            try
            {
                var p = SettingsTransfer.Read(dlg.FileName);
                string name = Path.GetFileName(dlg.FileName);
                if (p.Files.Count > 0 && plugin.Settings != null && !TermsGate.Accepted(owner, plugin,
                        "Import " + name + "?\n\nIts dashes come from a file, not from the library. The plugin checks their format, their size and that any script in them " +
                        "passes the library's safety check, but not who made them or whether they allow them to be shared.", "Import settings"))
                    return;

                var lines = new List<string>
                {
                    "Replace the settings here with the ones in " + name + "?", "",
                    "Saved" + (p.Plugin != null ? " by v" + p.Plugin : "") + (p.ExportedUtc > DateTime.MinValue ? " on " + p.ExportedUtc.ToLocalTime().ToString("g") : "") + ". In it:",
                    $"  {Count(p.Count("Usb.UserLights"), "light set")} of your own, {Count(p.Count("Usb.CarDashes"), "car")} with dashes, {Count(p.Count("Overrides"), "car")} tuned",
                };
                if (p.Files.Count > 0) lines.Add($"  {Count(p.Files.Count, "dash and screensaver file")}");
                if (p.Refused.Count > 0)
                {
                    lines.Add("");
                    lines.Add("Left out:");
                    lines.AddRange(p.Refused.Take(8).Select(r => "  " + r));
                    if (p.Refused.Count > 8) lines.Add($"  and {p.Refused.Count - 8} more");
                }
                lines.Add("");
                lines.Add("The settings you have now are copied aside first, and this PC's own (firmware and screen state, SimPro's presets, where updates come from) stay as they are. " +
                          "SimHub restarts to load them.");
                if (MessageBox.Show(owner, string.Join("\n", lines), Title, MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel) != MessageBoxResult.OK) return;

                bool replace = false;
                var conflicts = SettingsTransfer.Conflicts(p, SimHubFolder);
                if (conflicts.Count > 0)
                {
                    var ask = MessageBox.Show(owner, $"{Count(conflicts.Count, "file")} here {(conflicts.Count == 1 ? "has" : "have")} the same name as one in the import, but {(conflicts.Count == 1 ? "is" : "are")} different:\n\n" +
                                                     string.Join("\n", conflicts.Take(10).Select(c => "  " + c)) + (conflicts.Count > 10 ? $"\n  and {conflicts.Count - 10} more" : "") +
                                                     "\n\nYes: use the import's (yours are copied to the plugin's backups folder).\nNo: keep yours.",
                                              Title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.No);
                    if (ask == MessageBoxResult.Cancel) return;
                    replace = ask == MessageBoxResult.Yes;
                }

                SettingsTransfer.Stage(p, SimHubFolder, replace, name, DateTime.UtcNow);
                Refresh();
                if (plugin.Updates != null && MessageBox.Show(owner, "Ready. The import is applied when SimHub starts next.\n\nRestart SimHub now?", Title,
                                                              MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    plugin.Updates.RestartSimHub();
            }
            catch (Exception ex) { MessageBox.Show(owner, "Not imported: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
            Refresh();
        }

        private void CancelImport()
        {
            try { SettingsTransfer.CancelPending(SimHubFolder); }
            catch (Exception ex) { MessageBox.Show(Window.GetWindow(this), "Couldn't cancel the import: " + ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Warning); }
            Refresh();
        }

        private static string Count(int n, string what) => n + " " + what + (n == 1 ? "" : "s");
    }
}
