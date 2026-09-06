using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Elas3DView
{
    /// <summary>
    /// Schaltfläche mit Farbfeld. Ein Klick öffnet eine Palette; darunter liegen
    /// die zuletzt benutzten Farben und der Weg zu einer völlig freien Farbe.
    /// </summary>
    public class ColorPickerButton : Button
    {
        private readonly Border _chip;
        private readonly TextBlock _caption;
        private Popup _popup;
        private WrapPanel _recentPanel;
        private Color _color = Color.FromRgb(0xB6, 0xBC, 0xC4);

        public event EventHandler ColorChanged;

        /// <summary>Zuletzt gewählte eigene Farben, geteilt über alle Wähler.</summary>
        public static readonly List<Color> Recent = new List<Color>();

        // Marken- und Werkstofffarben. Erste Reihe: die Blüte aus dem Elas-Logo.
        private static readonly string[][] Groups =
        {
            new[] { "#D4514F", "#EE9264", "#D3D356", "#6E9A5A", "#6E90C4", "#C43A63" },
            new[] { "#FFFFFF", "#F2F0EA", "#EAE6DC", "#C8CDD3", "#B6BCC4", "#8A9099" },
            new[] { "#545A62", "#3A3F46", "#232629", "#12161B", "#2E4756", "#0F766E" },
            new[] { "#C0392B", "#E2863A", "#F0A830", "#F2C14E", "#C8D64B", "#7FBF4D" },
            new[] { "#5DB06B", "#3E9A6E", "#14B8A6", "#38BDF8", "#4E93D6", "#2F72B8" },
            new[] { "#3B5BA5", "#818CF8", "#7C5CBF", "#A855C7", "#E0669A", "#F2A2C0" },
            new[] { "#8B5E3C", "#B08050", "#D9B382", "#C9A227", "#B87333", "#6F7B84" }
        };

        public Color SelectedColor
        {
            get { return _color; }
            set
            {
                _color = value;
                _chip.Background = new SolidColorBrush(value);
            }
        }

        public ColorPickerButton(string caption)
        {
            Style = (Style)Application.Current.Resources["Btn"];
            Padding = new Thickness(8, 5, 10, 5);

            StackPanel sp = new StackPanel();
            sp.Orientation = Orientation.Horizontal;

            _chip = new Border();
            _chip.Width = 18;
            _chip.Height = 18;
            _chip.CornerRadius = new CornerRadius(6);
            _chip.BorderThickness = new Thickness(1);
            _chip.BorderBrush = new SolidColorBrush(Color.FromArgb(0x66, 0x80, 0x80, 0x80));
            _chip.Background = new SolidColorBrush(_color);
            _chip.VerticalAlignment = VerticalAlignment.Center;
            sp.Children.Add(_chip);

            _caption = new TextBlock();
            _caption.Text = caption;
            _caption.Margin = new Thickness(8, 0, 6, 0);
            _caption.VerticalAlignment = VerticalAlignment.Center;
            sp.Children.Add(_caption);

            Path arrow = new Path();
            arrow.Data = Geometry.Parse("M 0,0 L 4,4 L 8,0");
            arrow.Stroke = (Brush)Application.Current.Resources["TextMuted"];
            arrow.StrokeThickness = 1.4;
            arrow.VerticalAlignment = VerticalAlignment.Center;
            sp.Children.Add(arrow);

            Content = sp;
            Click += delegate { TogglePopup(); };
        }

        private void TogglePopup()
        {
            if (_popup == null) _popup = BuildPopup();
            RefreshRecent();
            _popup.IsOpen = !_popup.IsOpen;
        }

        private Popup BuildPopup()
        {
            Popup p = new Popup();
            p.PlacementTarget = this;
            p.Placement = PlacementMode.Bottom;
            p.StaysOpen = false;
            p.AllowsTransparency = true;
            p.PopupAnimation = PopupAnimation.Fade;

            Border shell = new Border();
            shell.CornerRadius = new CornerRadius(13);
            shell.Background = (Brush)Application.Current.Resources["Elevated"];
            shell.BorderThickness = new Thickness(1);
            shell.BorderBrush = (Brush)Application.Current.Resources["Gold"];
            shell.Padding = new Thickness(12);
            shell.Margin = new Thickness(0, 5, 8, 8);
            p.Child = shell;

            StackPanel root = new StackPanel();
            root.Width = 232;
            shell.Child = root;

            foreach (string[] row in Groups)
            {
                WrapPanel wp = new WrapPanel();
                wp.Margin = new Thickness(0, 0, 0, 4);
                foreach (string hex in row) wp.Children.Add(Swatch(ParseHex(hex)));
                root.Children.Add(wp);
            }

            root.Children.Add(Divider());

            TextBlock lbl = new TextBlock();
            lbl.Text = "Zuletzt verwendet";
            lbl.FontSize = 11;
            lbl.Margin = new Thickness(2, 2, 0, 5);
            lbl.Foreground = (Brush)Application.Current.Resources["TextMuted"];
            root.Children.Add(lbl);

            _recentPanel = new WrapPanel();
            _recentPanel.Margin = new Thickness(0, 0, 0, 6);
            root.Children.Add(_recentPanel);

            Button custom = new Button();
            custom.Content = "Eigene Farbe …";
            custom.Style = (Style)Application.Current.Resources["Btn"];
            custom.Margin = new Thickness(0, 2, 0, 0);
            custom.HorizontalAlignment = HorizontalAlignment.Stretch;
            custom.Click += delegate { PickCustom(); };
            root.Children.Add(custom);

            return p;
        }

        private static Border Divider()
        {
            Border d = new Border();
            d.Height = 1;
            d.Margin = new Thickness(0, 6, 0, 6);
            d.Background = (Brush)Application.Current.Resources["Border"];
            return d;
        }

        private Border Swatch(Color c)
        {
            Border b = new Border();
            b.Width = 30;
            b.Height = 26;
            b.Margin = new Thickness(0, 0, 4, 4);
            b.CornerRadius = new CornerRadius(7);
            b.Background = new SolidColorBrush(c);
            b.BorderThickness = new Thickness(1);
            b.BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0x88, 0x88, 0x88));
            b.Cursor = System.Windows.Input.Cursors.Hand;
            b.ToolTip = HexOf(c);
            b.MouseLeftButtonUp += delegate
            {
                Apply(c, false);
                _popup.IsOpen = false;
            };
            return b;
        }

        private void RefreshRecent()
        {
            if (_recentPanel == null) return;
            _recentPanel.Children.Clear();
            if (Recent.Count == 0)
            {
                TextBlock t = new TextBlock();
                t.Text = "—";
                t.Foreground = (Brush)Application.Current.Resources["TextMuted"];
                t.Margin = new Thickness(2, 0, 0, 4);
                _recentPanel.Children.Add(t);
                return;
            }
            foreach (Color c in Recent) _recentPanel.Children.Add(Swatch(c));
        }

        private void PickCustom()
        {
            System.Windows.Forms.ColorDialog dlg = new System.Windows.Forms.ColorDialog();
            dlg.FullOpen = true;
            dlg.AnyColor = true;
            dlg.Color = System.Drawing.Color.FromArgb(_color.R, _color.G, _color.B);
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            Apply(Color.FromRgb(dlg.Color.R, dlg.Color.G, dlg.Color.B), true);
            if (_popup != null) _popup.IsOpen = false;
        }

        private void Apply(Color c, bool remember)
        {
            SelectedColor = c;
            if (remember)
            {
                Recent.RemoveAll(delegate(Color x) { return x == c; });
                Recent.Insert(0, c);
                while (Recent.Count > 12) Recent.RemoveAt(Recent.Count - 1);
            }
            if (ColorChanged != null) ColorChanged(this, EventArgs.Empty);
        }

        // -------------------------------------------------------- Hilfsfunktionen

        public static Color ParseHex(string hex)
        {
            try
            {
                if (hex == null) return Colors.Gray;
                hex = hex.TrimStart('#');
                if (hex.Length != 6) return Colors.Gray;
                return Color.FromRgb(
                    byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            }
            catch { return Colors.Gray; }
        }

        public static string HexOf(Color c)
        {
            return string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}", c.R, c.G, c.B);
        }
    }
}
