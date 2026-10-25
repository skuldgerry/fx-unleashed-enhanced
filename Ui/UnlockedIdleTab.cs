using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace User.FXProRpmSync
{
    /// <summary>
    /// Unlocked mode, between sessions: the screensaver (the logo, the clock, your pictures or any dash; pick the one
    /// shown) and sleep (lights and screen off after a while without a game).
    /// </summary>
    public class UnlockedIdleTab : StackPanel
    {
        private readonly FXProRpmSyncPlugin plugin;
        private UsbSettings S => plugin.Settings.Usb;
        private UsbController Usb => plugin.Usb;

        private readonly WrapPanel gallery;
        private readonly Button addPicture;
        private readonly TextBlock sleepState;
        private readonly Button sleepButton;
        private readonly DispatcherTimer slowTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };

        public UnlockedIdleTab(FXProRpmSyncPlugin plugin)
        {
            this.plugin = plugin;

            // ----- Screensaver -----
            var saver = new StackPanel();
            var head = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var on = Theme.Switch("Show a screensaver", S.ScreenSaver, v => { S.ScreenSaver = v; Changed(); gallery.Opacity = v ? 1 : 0.5; });
            on.Margin = new Thickness(0, 4, 0, 0);
            DockPanel.SetDock(on, Dock.Right);
            head.Children.Add(on);
            var headText = new StackPanel();
            headText.Children.Add(Theme.Eyebrow("Screensaver"));
            headText.Children.Add(Theme.Title("When you're not racing", 20));
            head.Children.Add(headText);
            saver.Children.Add(head);
            saver.Children.Add(Theme.Note("Shown on the wheel's screen between sessions; off gives the screen back to the wheel's own dash. " +
                                          "Click one to make it the default; with \"Take turns\" on, tick the others to show in turn. With picture memory on (Wheel tab), pictures show in full colour " +
                                          "and a GIF plays (as many of its frames as fit in the screen's memory); without it, pictures are simplified to draw in within 15 seconds and a GIF shows its first frame."));
            gallery = new WrapPanel { Opacity = S.ScreenSaver ? 1 : 0.5 };
            saver.Children.Add(gallery);
            var rotate = Theme.SliderField(0, 60, S.SaverSwitchMinutes, 1, v => v < 1 ? "Just the default" : $"Every {v:0} min", v =>
            {
                if ((int)v == S.SaverSwitchMinutes) return;
                S.SaverSwitchMinutes = (int)v;
                Changed();
                BuildGallery();
            }, 260);
            var rotateRow = Theme.Field("Take turns", rotate, 110);
            rotateRow.Margin = new Thickness(0, 6, 0, 10);
            saver.Children.Add(rotateRow);
            var add = new WrapPanel { Margin = new Thickness(0, 4, 0, -8) };
            addPicture = Theme.Btn("Add a picture…", AddPicture, icon: "");
            addPicture.ToolTip = "A picture, or a GIF: an animated one plays when the screen has picture memory";
            add.Children.Add(addPicture);
            var dashBox = new ComboBox { Width = 260, Margin = new Thickness(0, 0, 8, 8), VerticalAlignment = VerticalAlignment.Top };
            bool filling = false;
            // the dashes as they are on disk now (one saved in the designer or installed from the library while SimHub
            // runs shows up without a restart): read again whenever the list opens, and with the Refresh button
            void FillDashes(bool force)
            {
                filling = true;
                dashBox.Items.Clear();
                dashBox.Items.Add(new ComboBoxItem { Content = "Add a dash as a screensaver…", Tag = null, IsEnabled = false });
                foreach (var d in DashCache.All(force)) dashBox.Items.Add(new ComboBoxItem { Content = d.Name, Tag = d.Id });
                dashBox.SelectedIndex = 0;
                filling = false;
            }
            FillDashes(false);
            dashBox.DropDownOpened += (s, e) => FillDashes(true);
            dashBox.SelectionChanged += (s, e) =>
            {
                if (filling || !(dashBox.SelectedItem is ComboBoxItem i) || i.Tag == null) return;
                var id = (string)i.Tag;
                var item = S.Savers.FirstOrDefault(x => x.Kind == SaverKind.Dash && x.DashId == id);
                if (item == null) S.Savers.Add(item = new SaverItem { Id = "dash-" + Guid.NewGuid().ToString("N").Substring(0, 8), Name = DashCache.NameOf(id), Kind = SaverKind.Dash, DashId = id });
                S.SaverId = item.Id;
                dashBox.SelectedIndex = 0;
                Changed();
                BuildGallery();
            };
            add.Children.Add(dashBox);
            var refresh = Theme.Btn("Refresh", () => FillDashes(true), icon: "");
            refresh.ToolTip = "Read the dashes again (one you just saved or installed)";
            refresh.VerticalAlignment = VerticalAlignment.Top;
            add.Children.Add(refresh);
            saver.Children.Add(add);
            var saverCard = Theme.CardBox(saver);
            Children.Add(saverCard);
            var driverCard = Theme.CardBox(DriverEditor());
            Children.Add(driverCard);

            var online = new StackPanel();
            online.Children.Add(Theme.Eyebrow("Screensaver library"));
            online.Children.Add(Theme.Note("Screensavers made by the community, apart from the dashes: install one and pick it above. Made your own (a picture or a dash)? " +
                                           "Press Share… on its tile above to package it for the library."));
            online.Children.Add(new Expander { Header = "Browse the screensaver library", Content = new LibraryPanel(plugin, "saver", BuildGallery), Margin = new Thickness(0, 8, 0, 0), IsExpanded = true });
            var onlineCard = Theme.CardBox(online);
            Children.Add(onlineCard);
            bool hasScreen = plugin.ActiveModel.HasScreen;
            if (!hasScreen) saverCard.Visibility = driverCard.Visibility = onlineCard.Visibility = Visibility.Collapsed; // screensavers need a screen

            // ----- Sleep -----
            var sleep = new StackPanel();
            var sleepHead = new DockPanel { Margin = new Thickness(0, 0, 0, 6) };
            var sleepOn = Theme.Switch("Sleep when idle", S.SleepEnabled, v => { S.SleepEnabled = v; Changed(); });
            sleepOn.Margin = new Thickness(0, 4, 0, 0);
            DockPanel.SetDock(sleepOn, Dock.Right);
            sleepHead.Children.Add(sleepOn);
            var sleepText = new StackPanel();
            sleepText.Children.Add(Theme.Eyebrow("Sleep"));
            sleepText.Children.Add(Theme.Title("Lights out", 20));
            sleepHead.Children.Add(sleepText);
            sleep.Children.Add(sleepHead);
            sleep.Children.Add(Theme.Note("After a while without a game, every light goes dark" + (hasScreen ? " and the screen's backlight turns off" : "") + ". " +
                                          "Starting a game (or the demo, or changing a setting here) wakes the wheel."));
            sleep.Children.Add(Theme.Field("Sleep after", Theme.SliderField(1, 60, S.SleepMinutes, 1, v => $"{v:0} min", v => { S.SleepMinutes = (int)v; Changed(); }), 130));
            var row = new DockPanel();
            sleepButton = Theme.Btn("Sleep now", () => { if (Usb?.Sleeping == true) Usb.Wake(); else Usb?.SleepNow(); Refresh(); }, icon: "");
            DockPanel.SetDock(sleepButton, Dock.Left);
            row.Children.Add(sleepButton);
            sleepState = new TextBlock { Foreground = Theme.Text2, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 8), FontFamily = Theme.Display };
            row.Children.Add(sleepState);
            sleep.Children.Add(row);
            sleep.Children.Add(new Border { Height = 1, Background = Theme.Line, Margin = new Thickness(0, 8, 0, 14) });
            sleep.Children.Add(new WheelButtonBinding(plugin, "sleep", "Sleep / wake"));
            var other = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            other.Children.Add(Theme.Binding("Sleep now", "UsbSleepNow"));
            other.Children.Add(Theme.Binding("Wake", "UsbWake"));
            sleep.Children.Add(new Expander { Header = "A keyboard key or another controller instead (through SimHub)", Content = other, Margin = new Thickness(0, 6, 0, 0) });
            Children.Add(Theme.CardBox(sleep));

            slowTimer.Tick += (s, e) => Refresh();
            Loaded += (s, e) => { slowTimer.Start(); BuildGallery(); Refresh(); };
            Unloaded += (s, e) => slowTimer.Stop();
            BuildGallery();
            Refresh();
        }

        private void Changed()
        {
            Usb?.SettingsChanged();
            plugin.SaveSettings();
        }

        /// <summary>The driver number screensaver's number, name, their colours and look; "Driver number" in the gallery shows it.</summary>
        private FrameworkElement DriverEditor()
        {
            if (S.Driver == null) S.Driver = new DriverCard();
            var d = S.Driver;
            var box = new StackPanel();
            box.Children.Add(Theme.Eyebrow("Driver number"));
            box.Children.Add(Theme.Title("Your number and name", 20));
            box.Children.Add(Theme.Note("A screensaver with your racing number and your name, like the Lovely Dashboard's idle screen: pick \"Driver number\" above " +
                                        "to show it. Switch on the number, the name or both; with neither it shows the FX Unleashed Enhanced logo."));
            // typing waits a moment before the gallery and the wheel draw it again (the wheel's first draw takes a few seconds)
            var settle = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            settle.Tick += (s, e) => { settle.Stop(); Changed(); BuildGallery(); };
            Action later = () => { settle.Stop(); settle.Start(); };
            Action now = () => { settle.Stop(); Changed(); BuildGallery(); };

            var number = new TextBox { Width = 70, MaxLength = 3, Text = d.Number ?? "", VerticalAlignment = VerticalAlignment.Center, IsEnabled = d.ShowNumber };
            number.TextChanged += (s, e) => { d.Number = number.Text; later(); };
            var numberColour = new ColourField(d.NumberColor, hex => { d.NumberColor = hex; later(); }) { Margin = new Thickness(10, 0, 0, 0) };
            var showNumber = Theme.Switch("", d.ShowNumber, v => { d.ShowNumber = v; number.IsEnabled = v; now(); });
            showNumber.Margin = new Thickness(0, 0, 10, 0); showNumber.VerticalAlignment = VerticalAlignment.Center;
            var numberRow = new StackPanel { Orientation = Orientation.Horizontal };
            numberRow.Children.Add(showNumber);
            numberRow.Children.Add(number);
            numberRow.Children.Add(numberColour);
            box.Children.Add(Theme.Field("Number", numberRow, 110));

            var name = new TextBox { Width = 240, MaxLength = 24, Text = d.Name ?? "", VerticalAlignment = VerticalAlignment.Center, IsEnabled = d.ShowName, ToolTip = "Shown in capitals" };
            name.TextChanged += (s, e) => { d.Name = name.Text; later(); };
            var showName = Theme.Switch("", d.ShowName, v => { d.ShowName = v; name.IsEnabled = v; now(); });
            showName.Margin = new Thickness(0, 0, 10, 0); showName.VerticalAlignment = VerticalAlignment.Center;
            var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
            nameRow.Children.Add(showName);
            nameRow.Children.Add(name);
            nameRow.Children.Add(new ColourField(d.NameColor, hex => { d.NameColor = hex; later(); }) { Margin = new Thickness(10, 0, 0, 0) });
            box.Children.Add(Theme.Field("Name", nameRow, 110));

            var look = new ComboBox { Width = 300, ToolTip = "The line goes under the number (under the name without one). The logo shows still." };
            foreach (var (text, value) in new[] { ("Steady", DriverNumberLook.Flat), ("A light running along a line under it", DriverNumberLook.Sweep),
                                                  ("A line under it, breathing", DriverNumberLook.Pulse) })
                look.Items.Add(new ComboBoxItem { Content = text, Tag = value });
            look.SelectedItem = look.Items.Cast<ComboBoxItem>().First(i => (DriverNumberLook)i.Tag == d.Look);
            look.SelectionChanged += (s, e) => { if (look.SelectedItem is ComboBoxItem i) { d.Look = (DriverNumberLook)i.Tag; Changed(); BuildGallery(); } };
            box.Children.Add(Theme.Field("Look", look, 110));
            return box;
        }

        private void Refresh()
        {
            var u = Usb;
            sleepButton.Content = u?.Sleeping == true ? "Wake" : "Sleep now";
            sleepButton.IsEnabled = u?.Active == true || u?.Sleeping == true || (u?.FirmwarePatched ?? S.FirmwareConfirmed);
            if (u?.Sleeping == true) sleepState.Text = "Asleep";
            else if (!S.SleepEnabled) sleepState.Text = "";
            else if (u?.SleepIn is double left) sleepState.Text = $"Sleeping in {(int)(left / 60)}:{(int)(left % 60):00}";
            else sleepState.Text = u?.State == "Active" || u?.State == "Demo" ? "Awake: a game is running" : "";
        }

        private void BuildGallery()
        {
            gallery.Children.Clear();
            foreach (var item in IdleScreens.All(S))
            {
                var img = new Image { Stretch = Stretch.Uniform };
                RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                var frame = new Grid();
                frame.Children.Add(new Border { Height = 124, Background = Brushes.Black, CornerRadius = new CornerRadius(6), Child = img, ClipToBounds = true });
                if (item.Kind == SaverKind.Image || item.Kind == SaverKind.Dash)
                {
                    var captured = item;
                    var remove = new Button
                    {
                        Content = new TextBlock { Text = "", FontFamily = Theme.Icons, FontSize = 10 }, Width = 24, Height = 24, Padding = new Thickness(0),
                        HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 6, 6, 0), ToolTip = "Remove",
                    };
                    remove.Click += (s, e) =>
                    {
                        S.Savers.RemoveAll(x => x.Id == captured.Id);
                        S.SaverRotation.Remove(captured.Id);
                        IdleScreens.Delete(captured);
                        if (S.SaverId == captured.Id) S.SaverId = SaverItem.LogoId;
                        Changed();
                        BuildGallery();
                    };
                    frame.Children.Add(remove);
                    if (!item.IsAnimation) // the library holds pictures and dashes, not animations
                    {
                        var share = new Button
                        {
                            Content = "Share…", Height = 24, Padding = new Thickness(8, 0, 8, 0), FontSize = 11,
                            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(6, 6, 0, 0),
                            ToolTip = "Package this screensaver for the library, to share it with everyone",
                        };
                        share.Click += (s, e) => PackageSaver(captured);
                        frame.Children.Add(share);
                    }
                }
                var id = item.Id;
                bool isDefault = item.Id == IdleScreens.Find(S, S.SaverId).Id;
                var cycle = S.SaverCycle();
                int turn = cycle.IndexOf(id);
                var badges = new WrapPanel { Margin = new Thickness(6), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
                if (isDefault) badges.Children.Add(Badge("DEFAULT", Theme.Red));
                if (turn >= 0 && cycle.Count > 1) badges.Children.Add(Badge("#" + (turn + 1), Theme.B("#3A3F4A")));
                frame.Children.Add(badges);
                var body = new StackPanel();
                body.Children.Add(frame);
                body.Children.Add(new TextBlock { Text = item.Name, FontFamily = Theme.Display, FontSize = 13.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
                body.Children.Add(new TextBlock { Text = KindText(item), Foreground = Theme.Text3, FontSize = 11 });
                if (item.IsAnimation)
                {
                    // the GIF is more than the screen's memory holds (about 200 KB of it for one animation, so the dashes stay
                    // loaded): evenly spaced frames kept, the loop as long as the GIF's
                    if (item.Frames < item.FramesTotal && !item.IsOversized)
                        body.Children.Add(new TextBlock { Text = $"Took {item.Frames} of {item.FramesTotal} frames (your smoothness choice)", Foreground = Theme.Text3, FontSize = 11, TextWrapping = TextWrapping.Wrap });
                    if (item.IsOversized)
                        body.Children.Add(new TextBlock
                        {
                            Text = $"Oversized: took {item.Frames}/{item.FramesTotal} frames", Foreground = Theme.Amber, FontSize = 11, FontWeight = FontWeights.SemiBold,
                            TextWrapping = TextWrapping.Wrap,
                            ToolTip = "The GIF has more than the screen's picture memory holds for one animation (about 200 KB, so your dashes stay loaded). " +
                                      "Evenly spaced frames were kept, so the loop is as long as the GIF's, with fewer steps.",
                        });
                    if (!S.ScreenRamDrive)
                        body.Children.Add(new TextBlock { Text = "Plays with picture memory on (Wheel tab); until then it shows its first frame.", Foreground = Theme.Text3, FontSize = 11, TextWrapping = TextWrapping.Wrap });
                }
                if (S.SaverSwitchMinutes > 0 && !isDefault)
                {
                    bool inTurn = S.SaverRotation.Contains(id);
                    var toggle = Theme.Switch("In rotation", inTurn, on =>
                    {
                        S.SaverRotation.Remove(id);
                        if (on) S.SaverRotation.Add(id);
                        Changed();
                        Dispatcher.BeginInvoke(new Action(BuildGallery));
                    });
                    toggle.Margin = new Thickness(0, 8, 0, 0);
                    toggle.FontSize = 12;
                    body.Children.Add(toggle);
                }
                var tile = Theme.Tile(body, 226, () => { S.SaverId = id; S.SaverRotation.Remove(id); Changed(); BuildGallery(); },
                    "Click to make it the default");
                Theme.Select(tile, isDefault);
                gallery.Children.Add(tile);
                var it = item;
                DashPictures.ShowSaver(img, it, S.ScreenRamDrive);
            }
        }

        private static Border Badge(string text, Brush bg) => new Border
        {
            Background = bg, CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 1, 6, 2), Margin = new Thickness(0, 0, 4, 4),
            Child = new TextBlock { Text = text, FontFamily = Theme.Display, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = Brushes.White },
        };

        private static string KindText(SaverItem item)
        {
            if (item.Blurb != null) return "Built in · " + item.Blurb.ToLowerInvariant();
            if (item.IsAnimation) return item.Frames < item.FramesTotal ? "Animated GIF" : $"Animated GIF · {item.Frames} frames";
            switch (item.Kind)
            {
                case SaverKind.Image: return "Picture";
                default: return "Dash";
            }
        }

        /// <summary>"Share…" on one of your own screensavers: the Package dialog, as a screensaver (the library keeps them apart from dashes).</summary>
        private void PackageSaver(SaverItem item)
        {
            var owner = Window.GetWindow(this);
            var d = IdleScreens.DashFor(item, DashCache.All())?.Clone();
            if (d == null) { MessageBox.Show(owner, "This screensaver can't be packaged.", "Package for the library"); return; }
            if (!string.IsNullOrWhiteSpace(item.Name)) d.Name = item.Name;
            PackageDialog.Show(owner, d, "saver");
        }

        private async void AddPicture()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Pictures and GIFs|*.png;*.jpg;*.jpeg;*.bmp;*.gif", Title = "A picture or an animated GIF for the wheel's screen" };
            if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;
            string path = dlg.FileName;
            try
            {
                Cursor = System.Windows.Input.Cursors.Wait;
                addPicture.IsEnabled = false;
                // an animated GIF gets its own window (how big, how smooth, what that gives); working things out takes a few
                // seconds (every frame is worked over to fit the screen's memory): off the UI thread
                var gif = await System.Threading.Tasks.Task.Run(() => AnimatedPicture.Inspect(path));
                SaverItem item;
                if (gif != null)
                {
                    Cursor = null;
                    item = GifImportDialog.Show(Window.GetWindow(this), path, gif, S.ScreenRamDrive, S.PadLeft, S.PadTop);
                    if (item == null) return; // cancelled
                }
                else item = await System.Threading.Tasks.Task.Run(() => IdleScreens.ImportImage(path));
                S.Savers.Add(item);
                S.SaverId = item.Id;
                Changed();
                BuildGallery();
            }
            catch (Exception ex) { MessageBox.Show("Couldn't use that picture: " + ex.Message, "FX Unleashed"); }
            finally { Cursor = null; addPicture.IsEnabled = true; }
        }
    }
}
