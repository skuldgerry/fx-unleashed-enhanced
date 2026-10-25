using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace User.FXProRpmSync
{
    /// <summary>
    /// Picks lights of some groups (the buttons' and encoders' for a colour of their own) on a drawing of the wheel: click
    /// a light to add it or take it away, or press a button on the wheel for the light under it (UsbSettings.ButtonMap).
    /// </summary>
    public static class LightPicker
    {
        /// <summary>The lights picked (in order), or null if it was cancelled.</summary>
        public static List<int> Pick(Window owner, FXProRpmSyncPlugin plugin, IEnumerable<LedGroup> groups, IEnumerable<int> current, string colour, string title)
        {
            var s = plugin.Settings.Usb;
            var model = plugin.ActiveModel;
            var allowed = new HashSet<int>(groups.SelectMany(model.Leds));
            var picked = new HashSet<int>((current ?? Enumerable.Empty<int>()).Where(allowed.Contains));
            var (r, g, b) = LightEngine.Rgb(string.IsNullOrEmpty(colour) ? "#FFFFFF" : colour);
            if (r + g + b < 120) (r, g, b) = ((byte)255, (byte)255, (byte)255); // a dark colour still shows what is picked

            var view = new WheelView(model, glow: true) { Width = 420 };
            var status = new TextBlock { Foreground = Theme.Text, FontFamily = Theme.Display, FontSize = 15, Margin = new Thickness(0, 10, 0, 2) };
            var hint = Theme.Note("Click a light to add it or take it away, or press its button on the wheel. Lit in the colour: picked. Grey: can be picked.",
                                  new Thickness(0, 0, 0, 10));
            hint.MaxWidth = 420;

            void Paint()
            {
                var frame = new LedColor[model.LedCount];
                foreach (var led in allowed)
                    if (led >= 0 && led < frame.Length)
                        frame[led] = picked.Contains(led) ? new LedColor(r, g, b, 90) : new LedColor(55, 55, 55, 40);
                view.Show(frame);
                status.Text = picked.Count == 0 ? "No light picked" : picked.Count == 1 ? "1 light picked" : picked.Count + " lights picked";
            }
            void Toggle(int led)
            {
                if (!allowed.Contains(led)) { status.Text = "That light isn't one of the " + string.Join(" or ", groups.Select(x => model.GroupName(x).ToLowerInvariant())); return; }
                if (!picked.Remove(led)) picked.Add(led);
                Paint();
            }

            view.LedClicked += Toggle;

            var buttons = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            List<int> result = null;
            Window w = null;
            buttons.Children.Add(Theme.Btn("Done", () => { result = picked.OrderBy(x => x).ToList(); w.Close(); }, primary: true));
            buttons.Children.Add(Theme.Btn("Clear", () => { picked.Clear(); Paint(); }));
            buttons.Children.Add(Theme.Btn("Cancel", () => w.Close()));

            var body = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
            body.Children.Add(Theme.Eyebrow(title));
            body.Children.Add(hint);
            body.Children.Add(new Border { Background = Theme.B("#07080A"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10), Child = view });
            body.Children.Add(status);
            body.Children.Add(buttons);

            w = new Window
            {
                Title = "Choose the lights", Owner = owner, Content = body, SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Background = Theme.Page,
                ShowInTaskbar = false,
            };
            Theme.Apply(w);

            // a press on the wheel picks the light under that button, and marks where the button is on the drawing
            var map = s.ButtonMap;
            Action<int> pressed = button => w.Dispatcher.BeginInvoke(new Action(() =>
            {
                view.Press(button, s);
                if (map.TryGetValue(button, out var led)) Toggle(led); else status.Text = $"Button {button} has no light under it in the button map";
            }));
            if (plugin.Buttons != null) plugin.Buttons.ButtonDown += pressed;
            w.Closed += (o, e) => { if (plugin.Buttons != null) plugin.Buttons.ButtonDown -= pressed; };
            Paint();
            w.ShowDialog();
            return result;
        }
    }
}
