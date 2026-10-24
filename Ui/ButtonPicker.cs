using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace User.FXProRpmSync
{
    /// <summary>
    /// Picks the buttons an alert lights (its # field) on a drawing of the wheel instead of by number: click a button's
    /// light, or press the button on the wheel, to add it or take it away. Only buttons the button map puts a light under
    /// can be picked (UsbSettings.ButtonMap: the wheel's usual map until the user maps their own).
    /// </summary>
    public static class ButtonPicker
    {
        /// <summary>The buttons picked (in the order of their lights), or null if it was cancelled.</summary>
        public static List<int> Pick(Window owner, FXProRpmSyncPlugin plugin, IEnumerable<int> current, string colour, string alertName)
        {
            var s = plugin.Settings.Usb;
            var model = plugin.ActiveModel;
            var map = s.ButtonMap;
            // light -> button, for the clicks on the drawing (one button per light in a map)
            var byLed = map.GroupBy(kv => kv.Value).ToDictionary(g => g.Key, g => g.First().Key);
            var picked = new HashSet<int>((current ?? Enumerable.Empty<int>()).Where(map.ContainsKey));
            var (r, g, b) = LightEngine.Rgb(string.IsNullOrEmpty(colour) ? "#FFFFFF" : colour);
            if (r + g + b < 120) (r, g, b) = ((byte)255, (byte)255, (byte)255); // a dark alert colour still shows what is picked

            var view = new WheelView(model, glow: true) { Width = 420 };
            var status = new TextBlock { Foreground = Theme.Text, FontFamily = Theme.Display, FontSize = 15, Margin = new Thickness(0, 10, 0, 2) };
            var hint = Theme.Note(map.Count == 0
                ? "No light is mapped under any button yet: map them in the Button lights card (bottom of the Lights tab)."
                : "Click a button's light to add it or take it away, or press the button on the wheel. Lit in the alert's colour: picked. Grey: can be picked.",
                new Thickness(0, 0, 0, 10));
            hint.MaxWidth = 420;

            void Paint()
            {
                var frame = new LedColor[model.LedCount];
                foreach (var kv in map)
                    if (kv.Value >= 0 && kv.Value < frame.Length)
                        frame[kv.Value] = picked.Contains(kv.Key) ? new LedColor(r, g, b, 90) : new LedColor(55, 55, 55, 40);
                view.Show(frame);
                status.Text = picked.Count == 0 ? "No button picked" : "Buttons " + string.Join(", ", Ordered());
            }
            IEnumerable<int> Ordered() => picked.OrderBy(n => map[n]);
            void Toggle(int button)
            {
                if (!map.ContainsKey(button)) { status.Text = $"Button {button} has no light under it in the button map"; return; }
                if (!picked.Remove(button)) picked.Add(button);
                Paint();
            }

            view.LedClicked += led => { if (byLed.TryGetValue(led, out var button)) Toggle(button); else status.Text = "That light isn't under a mapped button"; };

            var buttons = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            List<int> result = null;
            Window w = null;
            buttons.Children.Add(Theme.Btn("Done", () => { result = Ordered().ToList(); w.Close(); }, primary: true));
            buttons.Children.Add(Theme.Btn("Clear", () => { picked.Clear(); Paint(); }));
            buttons.Children.Add(Theme.Btn("Cancel", () => w.Close()));

            var body = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
            body.Children.Add(Theme.Eyebrow("Buttons for " + (string.IsNullOrWhiteSpace(alertName) ? "this alert" : alertName)));
            body.Children.Add(hint);
            body.Children.Add(new Border { Background = Theme.B("#07080A"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Child = view });
            body.Children.Add(status);
            body.Children.Add(buttons);

            w = new Window
            {
                Title = "Choose the buttons", Owner = owner, Content = body, SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Background = Theme.Page,
                ShowInTaskbar = false,
            };
            Theme.Apply(w);

            // a press on the wheel picks too, and marks where the button is on the drawing
            Action<int> pressed = button => w.Dispatcher.BeginInvoke(new Action(() => { view.Press(button, s); Toggle(button); }));
            if (plugin.Buttons != null) plugin.Buttons.ButtonDown += pressed;
            w.Closed += (o, e) => { if (plugin.Buttons != null) plugin.Buttons.ButtonDown -= pressed; };
            Paint();
            w.ShowDialog();
            return result;
        }
    }
}
