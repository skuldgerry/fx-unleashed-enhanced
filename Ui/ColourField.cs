using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace User.FXProRpmSync
{
    /// <summary>
    /// A colour swatch with a hex box; the swatch opens a palette of ready colours and a picker for any other: a square of
    /// saturation and brightness over a strip of hues (drag in either, the colour follows as it moves).
    /// </summary>
    public class ColourField : StackPanel
    {
        private static readonly string[] Palette =
        {
            "#FFFFFF", "#FF0020", "#FF1E00", "#FF5A00", "#FF7A00", "#FFB000", "#FFD000", "#FFFF00",
            "#B0FF00", "#00FF40", "#00FFA3", "#00F0FF", "#7FDBFF", "#00A0FF", "#0060FF", "#0020FF",
            "#7B2FFF", "#9D4EDD", "#C000FF", "#FF00D0", "#FF2E97", "#FF6090", "#FFE0B0", "#000000",
        };

        private const int PickW = 212, PickH = 120, HueH = 14;

        private readonly Border swatch;
        private readonly TextBox hexBox;
        private readonly Action<string> set;
        private readonly Popup popup;
        // "no colour" (an alert that keeps its lights' own): offered when there's a label for it
        private readonly Action<bool> setNone;
        private bool none;
        private const string NoneText = "keep";

        /// <summary>The palette and picker as they show when the swatch is clicked (for the offline picture of it).</summary>
        internal FrameworkElement Panel(string hex) { FromHex(hex); var c = (FrameworkElement)popup.Child; popup.Child = null; return c; }

        // the picker: hue 0-360, saturation and value 0-1
        private double hue, sat, val;
        private readonly WriteableBitmap square = new WriteableBitmap(PickW, PickH, 96, 96, PixelFormats.Bgr32, null);
        private readonly Ellipse squareMark = new Ellipse { Width = 10, Height = 10, Stroke = Brushes.White, StrokeThickness = 2, IsHitTestVisible = false };
        private readonly Border hueMark = new Border { Width = 4, Height = HueH + 4, BorderBrush = Brushes.White, BorderThickness = new Thickness(1.5), IsHitTestVisible = false };
        private readonly Border preview = new Border { Width = 34, Height = 22, CornerRadius = new CornerRadius(3), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) };

        public ColourField(string hex, Action<string> set) : this(hex, set, null, false, null) { }

        /// <param name="noneLabel">With one: the popup offers "no colour" with this label; `setNone` hears it switched on (true) or off (a colour picked).</param>
        public ColourField(string hex, Action<string> set, string noneLabel, bool isNone, Action<bool> setNone)
        {
            this.set = set;
            this.setNone = setNone;
            Orientation = Orientation.Horizontal;
            swatch = new Border { Width = 26, Height = 22, CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray, Cursor = Cursors.Hand, VerticalAlignment = VerticalAlignment.Center,
                                  ToolTip = "Click: ready colours, or pick any colour" };
            hexBox = new TextBox { Width = 88, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Text = hex };
            Children.Add(swatch);
            Children.Add(hexBox);
            Show(hex);

            popup = new Popup { PlacementTarget = swatch, StaysOpen = false, AllowsTransparency = true };
            var body = new StackPanel { Margin = new Thickness(8) };
            if (noneLabel != null)
            {
                var off = new Border { Padding = new Thickness(8, 5, 8, 6), Margin = new Thickness(2, 0, 2, 8), CornerRadius = new CornerRadius(4), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Cursor = Cursors.Hand,
                                       Background = Theme.B(Color.FromRgb(0x1B, 0x1D, 0x21)),
                                       Child = new TextBlock { Text = "∅  " + noneLabel, Foreground = Theme.Text, FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxWidth = 196 } };
                off.MouseLeftButtonUp += (s, e) => { popup.IsOpen = false; SetNone(true); };
                body.Children.Add(off);
            }
            var grid = new UniformGrid { Columns = 8 };
            foreach (var c in Palette)
            {
                var chip = new Border { Width = 22, Height = 22, Margin = new Thickness(2), CornerRadius = new CornerRadius(3), Background = Theme.B((Color)ColorConverter.ConvertFromString(c)), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Cursor = Cursors.Hand };
                var cc = c;
                chip.MouseLeftButtonUp += (s, e) => { popup.IsOpen = false; hexBox.Text = cc; Commit(); };
                grid.Children.Add(chip);
            }
            body.Children.Add(grid);
            body.Children.Add(new TextBlock { Text = "Any colour", Foreground = Theme.Text3, FontSize = 11, Margin = new Thickness(2, 8, 0, 4) });
            body.Children.Add(Picker());
            popup.Child = new Border { Background = Theme.B(Color.FromRgb(0x25, 0x27, 0x2b)), CornerRadius = new CornerRadius(6), Child = body };
            swatch.MouseLeftButtonUp += (s, e) => { FromHex(hexBox.Tag as string ?? hexBox.Text); popup.IsOpen = true; };
            hexBox.LostFocus += (s, e) => Commit();
            hexBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) Commit(); };
            if (isNone && noneLabel != null) { none = true; ShowNone(); }
        }

        /// <summary>"No colour" on or off; picking any colour switches it off.</summary>
        private void SetNone(bool on)
        {
            if (none == on) return;
            none = on;
            if (on) ShowNone(); else swatch.Child = null;
            setNone?.Invoke(on);
        }

        private void ShowNone()
        {
            swatch.Background = Theme.B(Color.FromRgb(0x1B, 0x1D, 0x21));
            swatch.Child = new TextBlock { Text = "∅", Foreground = Theme.Text2, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 13 };
            hexBox.Text = NoneText;
        }

        /// <summary>The square (saturation across, brightness down) and the hue strip, with the colour as it is now.</summary>
        private FrameworkElement Picker()
        {
            var squareCanvas = new Canvas { Width = PickW, Height = PickH, Cursor = Cursors.Cross, Background = new ImageBrush(square), ClipToBounds = true };
            squareCanvas.Children.Add(squareMark);
            var hueStrip = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0) };
            for (int i = 0; i <= 6; i++) hueStrip.GradientStops.Add(new GradientStop(Hsv(i * 60, 1, 1), i / 6.0));
            var hueCanvas = new Canvas { Width = PickW, Height = HueH, Cursor = Cursors.Hand, Background = hueStrip, Margin = new Thickness(0, 6, 0, 0) };
            hueCanvas.Children.Add(hueMark);

            void InSquare(Point p) { sat = Clamp(p.X / PickW); val = 1 - Clamp(p.Y / PickH); Picked(false); }
            void OnHue(Point p) { hue = Clamp(p.X / PickW) * 360; Picked(true); }
            Drag(squareCanvas, InSquare);
            Drag(hueCanvas, OnHue);

            var row = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
            var hint = new TextBlock { Text = "Drag in the square and the strip", Foreground = Theme.Text3, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            row.Children.Add(preview);
            row.Children.Add(hint);

            var box = new StackPanel();
            box.Children.Add(squareCanvas);
            box.Children.Add(hueCanvas);
            box.Children.Add(row);
            return box;
        }

        /// <summary>Press and drag in `area`: `at` gets each point; the colour is kept when the button is let go.</summary>
        private void Drag(Canvas area, Action<Point> at)
        {
            area.MouseLeftButtonDown += (s, e) => { area.CaptureMouse(); at(e.GetPosition(area)); e.Handled = true; };
            area.MouseMove += (s, e) => { if (area.IsMouseCaptured) at(e.GetPosition(area)); };
            area.MouseLeftButtonUp += (s, e) => { if (!area.IsMouseCaptured) return; area.ReleaseMouseCapture(); Commit(); e.Handled = true; };
        }

        /// <summary>The picker moved: the square again if the hue changed, the marks, and the colour in the hex box (kept on release).</summary>
        private void Picked(bool hueChanged)
        {
            if (hueChanged) PaintSquare();
            PlaceMarks();
            var c = Hsv(hue, sat, val);
            var hex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            hexBox.Text = hex;
            preview.Background = new SolidColorBrush(c);
            swatch.Background = new SolidColorBrush(c);
        }

        /// <summary>The picker set to a colour (when it opens: the field's colour).</summary>
        private void FromHex(string hex)
        {
            Color c;
            try { c = (Color)ColorConverter.ConvertFromString(hex); } catch { c = Colors.White; }
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0, max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
            val = max;
            sat = max <= 0 ? 0 : d / max;
            if (d > 0)
            {
                double h = max == r ? (g - b) / d % 6 : max == g ? (b - r) / d + 2 : (r - g) / d + 4;
                hue = (h * 60 + 360) % 360;
            }
            PaintSquare();
            PlaceMarks();
            preview.Background = new SolidColorBrush(c);
        }

        private void PaintSquare()
        {
            var px = new int[PickW * PickH];
            for (int y = 0; y < PickH; y++)
                for (int x = 0; x < PickW; x++)
                {
                    var c = Hsv(hue, x / (double)(PickW - 1), 1 - y / (double)(PickH - 1));
                    px[y * PickW + x] = (c.R << 16) | (c.G << 8) | c.B;
                }
            square.WritePixels(new Int32Rect(0, 0, PickW, PickH), px, PickW * 4, 0);
        }

        private void PlaceMarks()
        {
            Canvas.SetLeft(squareMark, sat * PickW - 5);
            Canvas.SetTop(squareMark, (1 - val) * PickH - 5);
            Canvas.SetLeft(hueMark, hue / 360 * PickW - 2);
            Canvas.SetTop(hueMark, -2);
        }

        private static double Clamp(double x) => Math.Max(0, Math.Min(1, x));

        private static Color Hsv(double h, double s, double v)
        {
            h = (h % 360 + 360) % 360 / 60;
            int i = (int)h;
            double f = h - i, p = v * (1 - s), q = v * (1 - s * f), t = v * (1 - s * (1 - f));
            double r, g, b;
            switch (i)
            {
                case 0: r = v; g = t; b = p; break;
                case 1: r = q; g = v; b = p; break;
                case 2: r = p; g = v; b = t; break;
                case 3: r = p; g = q; b = v; break;
                case 4: r = t; g = p; b = v; break;
                default: r = v; g = p; b = q; break;
            }
            return Color.FromRgb((byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
        }

        private void Commit()
        {
            var t = hexBox.Text.Trim();
            if (none && t == NoneText) return; // still "no colour"
            if (!t.StartsWith("#")) t = "#" + t;
            if (t.Length != 7 || !int.TryParse(t.Substring(1), System.Globalization.NumberStyles.HexNumber, null, out _)) { if (none) ShowNone(); else hexBox.Text = hexBox.Tag as string ?? "#FFFFFF"; return; }
            t = t.ToUpperInvariant();
            hexBox.Text = t;
            bool wasNone = none;
            SetNone(false);
            if (t == hexBox.Tag as string && !wasNone) return;
            Show(t);
            set(t);
        }

        private void Show(string hex)
        {
            hexBox.Tag = hex;
            try { swatch.Background = Theme.B((Color)ColorConverter.ConvertFromString(hex)); } catch { swatch.Background = Brushes.Black; }
        }
    }
}
