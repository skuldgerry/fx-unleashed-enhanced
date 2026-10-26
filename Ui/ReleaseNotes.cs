using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace User.FXProRpmSync
{
    /// <summary>
    /// A release's notes in the update box, made easy to read at a glance: only its sections of changes (a heading with
    /// new / added, fixed, changed / improved in it), each in its own colour, a bullet per change, bold and links as such.
    /// Notes without such sections show as they are.
    /// </summary>
    public static class ReleaseNotes
    {
        public enum Kind { New, Fixed, Changed }

        public sealed class Section
        {
            public Kind Kind;
            public string Title;
            public List<string> Items = new List<string>();
        }

        private static readonly (Kind Kind, Regex Words)[] Kinds =
        {
            (Kind.Fixed, new Regex(@"\b(fix|fixed|fixes|bug ?fixes)\b", RegexOptions.IgnoreCase)),
            (Kind.New, new Regex(@"\b(new|added|adds|features?)\b", RegexOptions.IgnoreCase)),
            (Kind.Changed, new Regex(@"\b(changed|changes|improved|improvements?)\b", RegexOptions.IgnoreCase)),
        };

        /// <summary>The sections of changes in a release's notes (markdown), in their order; empty when it has none.</summary>
        public static List<Section> Changes(string notes)
        {
            var list = new List<Section>();
            Section current = null;
            bool blank = false;
            foreach (var raw in (notes ?? "").Replace("\r", "").Split('\n'))
            {
                var line = raw.TrimEnd();
                if (line.Trim().Length == 0) { blank = true; continue; }
                var heading = Regex.Match(line, @"^\s*#{2,4}\s+(.*)$");
                if (heading.Success)
                {
                    var title = heading.Groups[1].Value.Trim();
                    var kind = Kinds.FirstOrDefault(k => k.Words.IsMatch(title));
                    current = kind.Words == null ? null : new Section { Kind = kind.Kind, Title = title };
                    if (current != null) list.Add(current);
                }
                else if (current != null)
                {
                    var item = Regex.Match(line, @"^\s*[-*]\s+(.*)$");
                    if (item.Success && !raw.StartsWith("    ")) current.Items.Add(item.Groups[1].Value.Trim());
                    else if (current.Items.Count == 0) current.Items.Add(line.Trim());
                    else if (blank) current = null; // a paragraph after the list isn't part of it
                    else current.Items[current.Items.Count - 1] += " " + line.Trim(); // the item goes on
                }
                blank = false;
            }
            list.RemoveAll(s => s.Items.Count == 0);
            return list;
        }

        public static SolidColorBrush ColourOf(Kind k) => (SolidColorBrush)(k == Kind.New ? Theme.Green : k == Kind.Fixed ? Theme.Amber : Theme.Blue);

        /// <summary>The notes as the update box shows them: the changes in colour, or (none found) the text as it is.</summary>
        public static FrameworkElement Render(string notes)
        {
            var sections = Changes(notes);
            if (sections.Count == 0)
                return new TextBox { Text = notes ?? "", IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 240, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, FontSize = 12 };
            var box = new StackPanel();
            foreach (var s in sections)
            {
                var colour = ColourOf(s.Kind);
                var c = colour.Color;
                var part = new StackPanel();
                part.Children.Add(new Border
                {
                    Background = colour, CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 2, 8, 3), Margin = new Thickness(0, 0, 0, 8), HorizontalAlignment = HorizontalAlignment.Left,
                    Child = new TextBlock { Text = s.Title.ToUpperInvariant(), Foreground = Brushes.Black, FontWeight = FontWeights.Bold, FontSize = 11.5 },
                });
                foreach (var item in s.Items)
                {
                    var row = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };
                    var dot = new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), Background = colour, Margin = new Thickness(1, 6, 9, 0), VerticalAlignment = VerticalAlignment.Top };
                    DockPanel.SetDock(dot, Dock.Left);
                    row.Children.Add(dot);
                    var text = new TextBlock { Foreground = Theme.Text, FontSize = 12.5, TextWrapping = TextWrapping.Wrap };
                    AddInlines(text.Inlines, item, colour);
                    row.Children.Add(text);
                    part.Children.Add(row);
                }
                // each kind of change in a band of its colour, so they are found at a glance
                box.Children.Add(new Border
                {
                    Background = Theme.B(Color.FromArgb(0x1C, c.R, c.G, c.B)), BorderBrush = colour, BorderThickness = new Thickness(3, 0, 0, 0),
                    CornerRadius = new CornerRadius(4), Padding = new Thickness(12, 10, 12, 6), Margin = new Thickness(0, box.Children.Count == 0 ? 0 : 8, 0, 0), Child = part,
                });
            }
            return new ScrollViewer { Content = box, MaxHeight = 320, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        /// <summary>A line of markdown as text: **bold**, `code` and [links](url) as such, the rest as it is.</summary>
        private static void AddInlines(InlineCollection into, string s, Brush linkColour)
        {
            int at = 0;
            foreach (Match m in Regex.Matches(s, @"\*\*(.+?)\*\*|`([^`]+)`|\[([^\]]+)\]\((https?://[^)\s]+)\)"))
            {
                if (m.Index > at) into.Add(new Run(s.Substring(at, m.Index - at)));
                if (m.Groups[1].Success) into.Add(new Bold(new Run(m.Groups[1].Value)));
                else if (m.Groups[2].Success) into.Add(new Run(m.Groups[2].Value) { FontFamily = new FontFamily("Consolas") });
                else
                {
                    var url = m.Groups[4].Value;
                    var link = new Hyperlink(new Run(m.Groups[3].Value)) { Foreground = linkColour, Cursor = System.Windows.Input.Cursors.Hand };
                    link.Click += (o, e) => { try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception) { } };
                    into.Add(link);
                }
                at = m.Index + m.Length;
            }
            if (at < s.Length) into.Add(new Run(s.Substring(at)));
        }
    }
}
