using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace ExtrabbitCode.Inventor.ModernUi;

/// <summary>
/// A themed modal color picker built on <see cref="ModernWindow"/>: a saturation/value field, a hue
/// bar, an optional alpha bar, hex + RGB input, preset swatches and an old/new preview. Returns the
/// chosen color, or null when cancelled.
/// <para>
/// Like <see cref="ModernMessageBox"/> and <see cref="ModernToast"/> it stays conflict-free across
/// library versions: no custom controls and no dependency-property registration — the picker
/// surfaces are plain framework shapes driven by mouse events, themed via the window's own
/// <c>Brush.*</c> resources.
/// </para>
/// </summary>
public static class ModernColorPicker
{
    /// <summary>Shows a modal themed color picker and returns the chosen color, or null on cancel.</summary>
    /// <param name="owner">Owner window (centers on it and inherits its palette); may be null.</param>
    /// <param name="theme">Light or Dark.</param>
    /// <param name="initial">The color the picker starts on (also shown as the restorable "old" half
    /// of the preview). Its alpha is ignored unless <paramref name="showAlpha"/> is true.</param>
    /// <param name="title">Title-bar caption.</param>
    /// <param name="showAlpha">True to add an opacity bar and 8-digit hex (<c>#AARRGGBB</c>).</param>
    /// <param name="presets">Optional preset swatches replacing the built-in row; an empty list hides
    /// the row.</param>
    /// <param name="palette">Optional color override; defaults to the owner's current palette.</param>
    /// <param name="font">Optional font (e.g. <c>FontOptions.FromInventor(...)</c>).</param>
    public static Color? Show(
        Window? owner,
        Theme theme,
        Color initial,
        string title = "Pick a color",
        bool showAlpha = false,
        IReadOnlyList<Color>? presets = null,
        ThemePalette? palette = null,
        FontOptions? font = null)
    {
        ThemePalette? effective = palette ?? ThemePalette.TryInheritFrom(owner, theme);

        ModernWindow window = new(theme, effective, font)
        {
            Title = title,
            Owner = owner,
            WindowStartupLocation = owner is null
                ? WindowStartupLocation.CenterScreen
                : WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
        };

        if (!showAlpha)
        {
            initial = Color.FromRgb(initial.R, initial.G, initial.B);
        }

        Session session = new(initial, showAlpha, presets ?? DefaultPresets);

        bool accepted = false;

        Grid root = new() { Margin = new Thickness(24, 20, 24, 18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        FrameworkElement picker = session.Build();
        Grid.SetRow(picker, 0);
        root.Children.Add(picker);

        StackPanel buttonBar = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 18, 0, 0),
        };
        Grid.SetRow(buttonBar, 1);
        root.Children.Add(buttonBar);

        Button ok = new() { Content = "OK", MinWidth = 92, IsDefault = true };
        ok.SetResourceReference(FrameworkElement.StyleProperty, "AccentButton");
        ok.Click += (_, _) =>
        {
            accepted = true;
            window.Close();
        };
        buttonBar.Children.Add(ok);

        Button cancel = new() { Content = "Cancel", MinWidth = 92, Margin = new Thickness(8, 0, 0, 0), IsCancel = true };
        buttonBar.Children.Add(cancel);

        window.Content = root;

        // Size the window explicitly from a measure pass — SizeToContent is unreliable with
        // WindowChrome (same approach as ModernMessageBox).
        const double captionHeight = 34;
        const double borders = 2;
        root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Size desired = root.DesiredSize;
        window.Width = desired.Width + borders;
        window.Height = desired.Height + captionHeight + borders;

        window.ShowDialog();
        return accepted ? session.Current : null;
    }

    /// <summary>The built-in preset swatches (Inventor blue first), used when none are passed.</summary>
    private static readonly Color[] DefaultPresets =
    [
        Color.FromRgb(0x06, 0x96, 0xD7), // Inventor blue
        Color.FromRgb(0x1F, 0xA8, 0xA8), // teal
        Color.FromRgb(0x3F, 0xB9, 0x50), // green
        Color.FromRgb(0xD2, 0x99, 0x22), // amber
        Color.FromRgb(0xF2, 0x8C, 0x28), // orange
        Color.FromRgb(0xEC, 0x4A, 0x41), // red
        Color.FromRgb(0xE5, 0x48, 0x8D), // pink
        Color.FromRgb(0x89, 0x57, 0xE5), // purple
        Color.FromRgb(0xFF, 0xFF, 0xFF), // white
        Color.FromRgb(0x1E, 0x1E, 0x1E), // near-black
    ];

    // ============================================================================================
    // Everything below is the picker's state + UI. A plain private class (no WPF base type): the
    // many mouse/text handlers share the HSV(A) state through it.
    // ============================================================================================

    private sealed class Session
    {
        // Fixed surface sizes; the field width is derived so the pickers line up with the input row.
        private const double FieldHeight = 180;
        private const double BarWidth = 16;
        private const double Gap = 12;
        private const double ThumbSize = 14;
        private const double Radius = 4;

        private readonly bool _alpha;
        private readonly Color _initial;
        private readonly IReadOnlyList<Color> _presets;
        private readonly double _fieldWidth;
        private readonly double _rowWidth;

        // Canonical state. Hue is kept separately so it survives round-trips through gray/black
        // (where RGB→HSV loses it).
        private double _h; // 0..360
        private double _s; // 0..1
        private double _v; // 0..1
        private byte _a;

        private bool _syncing;

        // Mutable pieces updated on every change.
        private readonly SolidColorBrush _hueBase = new();
        private readonly SolidColorBrush _preview = new();
        private readonly GradientStop _alphaFrom = new();
        private readonly GradientStop _alphaTo = new();
        private Grid _fieldThumb = null!;
        private Grid _hueThumb = null!;
        private Grid _alphaThumb = null!;
        private TextBox _hexBox = null!;
        private TextBox _rBox = null!;
        private TextBox _gBox = null!;
        private TextBox _bBox = null!;
        private TextBox? _aBox;

        public Session(Color initial, bool alpha, IReadOnlyList<Color> presets)
        {
            _alpha = alpha;
            _initial = initial;
            _presets = presets;

            // Input row: preview 56 + hex 104 + three (or four) 46-wide byte boxes, 8px gaps.
            _rowWidth = 56 + 8 + 104 + (8 + 46) * (alpha ? 4 : 3);
            _fieldWidth = _rowWidth - (BarWidth + Gap) * (alpha ? 2 : 1);

            (_h, _s, _v) = RgbToHsv(initial);
            _a = alpha ? initial.A : (byte)0xFF;
        }

        /// <summary>The currently picked color.</summary>
        public Color Current { get; private set; }

        public FrameworkElement Build()
        {
            StackPanel root = new();

            // --- Row 0: saturation/value field + hue bar (+ alpha bar) -------------------------
            StackPanel pickers = new() { Orientation = Orientation.Horizontal };
            pickers.Children.Add(BuildField());
            pickers.Children.Add(BuildHueBar());
            if (_alpha)
            {
                pickers.Children.Add(BuildAlphaBar());
            }
            root.Children.Add(pickers);

            // --- Row 1: preview + hex + RGB(A) boxes --------------------------------------------
            root.Children.Add(BuildInputRow());

            // --- Row 2: preset swatches -----------------------------------------------------------
            if (_presets.Count > 0)
            {
                root.Children.Add(BuildPresets());
            }

            Sync(null);
            return root;
        }

        // --- surfaces ---------------------------------------------------------------------------

        private FrameworkElement BuildField()
        {
            _hueBase.Color = HsvToRgb(_h, 1, 1, 0xFF);

            Grid field = new() { Width = _fieldWidth, Height = FieldHeight };

            field.Children.Add(new Rectangle { RadiusX = Radius, RadiusY = Radius, Fill = _hueBase });
            field.Children.Add(new Rectangle
            {
                RadiusX = Radius,
                RadiusY = Radius,
                Fill = Frozen(new LinearGradientBrush(Colors.White, Color.FromArgb(0, 255, 255, 255), 0)),
            });
            field.Children.Add(new Rectangle
            {
                RadiusX = Radius,
                RadiusY = Radius,
                Fill = Frozen(new LinearGradientBrush(Color.FromArgb(0, 0, 0, 0), Colors.Black, 90)),
            });
            field.Children.Add(BorderOverlay());

            _fieldThumb = BuildThumb();
            field.Children.Add(ThumbLayer(_fieldThumb));

            AttachDrag(field, p =>
            {
                _s = Clamp01(p.X / _fieldWidth);
                _v = 1 - Clamp01(p.Y / FieldHeight);
                Sync(null);
            });
            return field;
        }

        private FrameworkElement BuildHueBar()
        {
            LinearGradientBrush rainbow = new()
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops =
                {
                    new GradientStop(Color.FromRgb(255, 0, 0), 0),
                    new GradientStop(Color.FromRgb(255, 255, 0), 1 / 6d),
                    new GradientStop(Color.FromRgb(0, 255, 0), 2 / 6d),
                    new GradientStop(Color.FromRgb(0, 255, 255), 3 / 6d),
                    new GradientStop(Color.FromRgb(0, 0, 255), 4 / 6d),
                    new GradientStop(Color.FromRgb(255, 0, 255), 5 / 6d),
                    new GradientStop(Color.FromRgb(255, 0, 0), 1),
                },
            };

            Grid bar = new() { Width = BarWidth, Height = FieldHeight, Margin = new Thickness(Gap, 0, 0, 0) };
            bar.Children.Add(new Rectangle { RadiusX = Radius, RadiusY = Radius, Fill = Frozen(rainbow) });
            bar.Children.Add(BorderOverlay());

            _hueThumb = BuildThumb();
            bar.Children.Add(ThumbLayer(_hueThumb));

            AttachDrag(bar, p =>
            {
                _h = Clamp01(p.Y / FieldHeight) * 360;
                if (_h >= 360)
                {
                    _h = 0;
                }
                Sync(null);
            });
            return bar;
        }

        private FrameworkElement BuildAlphaBar()
        {
            _alphaFrom.Offset = 0;
            _alphaTo.Offset = 1;

            LinearGradientBrush fade = new()
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1),
                GradientStops = { _alphaFrom, _alphaTo },
            };

            Grid bar = new() { Width = BarWidth, Height = FieldHeight, Margin = new Thickness(Gap, 0, 0, 0) };
            bar.Children.Add(new Rectangle { RadiusX = Radius, RadiusY = Radius, Fill = Checkerboard() });
            bar.Children.Add(new Rectangle { RadiusX = Radius, RadiusY = Radius, Fill = fade });
            bar.Children.Add(BorderOverlay());

            _alphaThumb = BuildThumb();
            bar.Children.Add(ThumbLayer(_alphaThumb));

            AttachDrag(bar, p =>
            {
                _a = (byte)Math.Round((1 - Clamp01(p.Y / FieldHeight)) * 255);
                Sync(null);
            });
            return bar;
        }

        // --- inputs -----------------------------------------------------------------------------

        private FrameworkElement BuildInputRow()
        {
            StackPanel row = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };

            // Old/new preview: the left (initial) half restores the initial color on click.
            Grid halves = new();
            halves.ColumnDefinitions.Add(new ColumnDefinition());
            halves.ColumnDefinitions.Add(new ColumnDefinition());

            Rectangle old = new()
            {
                Fill = Frozen(new SolidColorBrush(_initial)),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Initial color — click to restore",
            };
            old.MouseLeftButtonDown += (_, _) => SetColor(_initial);
            halves.Children.Add(old);

            Rectangle current = new() { Fill = _preview, IsHitTestVisible = false };
            Grid.SetColumn(current, 1);
            halves.Children.Add(current);

            Grid layered = new();
            if (_alpha)
            {
                layered.Children.Add(new Rectangle { Fill = Checkerboard() });
            }
            layered.Children.Add(halves);

            Border preview = new()
            {
                Width = 56,
                Height = 30,
                CornerRadius = new CornerRadius(Radius),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Bottom,
                SnapsToDevicePixels = true,
                Child = layered,
            };
            preview.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");

            // Border does not clip children to its corner radius; a matching clip keeps the color
            // halves inside the rounded outline.
            preview.Clip = new RectangleGeometry(new Rect(0, 0, 56, 30), Radius, Radius);
            row.Children.Add(preview);

            _hexBox = new TextBox { Width = 104, MaxLength = 9, CharacterCasing = CharacterCasing.Upper };
            _hexBox.TextChanged += (_, _) =>
            {
                if (!_syncing && TryParseHex(_hexBox.Text, _alpha, out Color c))
                {
                    SetColor(c, except: _hexBox);
                }
            };
            _hexBox.LostFocus += (_, _) => Sync(null);
            row.Children.Add(Labeled("Hex", _hexBox));

            _rBox = ByteBox(() => Current.R, value => Color.FromArgb(Current.A, value, Current.G, Current.B));
            _gBox = ByteBox(() => Current.G, value => Color.FromArgb(Current.A, Current.R, value, Current.B));
            _bBox = ByteBox(() => Current.B, value => Color.FromArgb(Current.A, Current.R, Current.G, value));
            row.Children.Add(Labeled("R", _rBox));
            row.Children.Add(Labeled("G", _gBox));
            row.Children.Add(Labeled("B", _bBox));

            if (_alpha)
            {
                _aBox = ByteBox(() => Current.A, value => Color.FromArgb(value, Current.R, Current.G, Current.B));
                row.Children.Add(Labeled("A", _aBox));
            }

            return row;
        }

        private TextBox ByteBox(Func<byte> get, Func<byte, Color> apply)
        {
            // Slim side padding: a centered 3-digit value plus WPF's caret reservation must fit the
            // 46px box — the theme's default padding clips a wide value like "150".
            TextBox box = new()
            {
                Width = 46,
                MaxLength = 3,
                TextAlignment = TextAlignment.Center,
                Padding = new Thickness(3, 4, 3, 4),
            };
            box.TextChanged += (_, _) =>
            {
                if (_syncing || !int.TryParse(box.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int value))
                {
                    return;
                }

                byte b = (byte)Math.Clamp(value, 0, 255);
                if (b != get())
                {
                    SetColor(apply(b), except: box);
                }
            };
            box.LostFocus += (_, _) => Sync(null);
            return box;
        }

        private static StackPanel Labeled(string caption, TextBox box)
        {
            TextBlock label = new() { Text = caption, Margin = new Thickness(1, 0, 0, 2) };
            label.SetResourceReference(FrameworkElement.StyleProperty, "CaptionTextStyle");

            StackPanel stack = new() { Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Bottom };
            stack.Children.Add(label);
            stack.Children.Add(box);
            return stack;
        }

        private FrameworkElement BuildPresets()
        {
            WrapPanel wrap = new() { Margin = new Thickness(0, 14, 0, 0), Width = _rowWidth, HorizontalAlignment = HorizontalAlignment.Left };
            foreach (Color preset in _presets)
            {
                Color color = _alpha ? preset : Color.FromRgb(preset.R, preset.G, preset.B);

                Border swatch = new()
                {
                    Width = 20,
                    Height = 20,
                    CornerRadius = new CornerRadius(Radius),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 6, 0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Background = Frozen(new SolidColorBrush(color)),
                    ToolTip = FormatHex(color, _alpha),
                    SnapsToDevicePixels = true,
                };
                swatch.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");
                swatch.MouseEnter += (_, _) => swatch.SetResourceReference(Border.BorderBrushProperty, "Brush.Accent");
                swatch.MouseLeave += (_, _) => swatch.SetResourceReference(Border.BorderBrushProperty, "Brush.Border");
                swatch.MouseLeftButtonDown += (_, _) => SetColor(color);
                wrap.Children.Add(swatch);
            }
            return wrap;
        }

        // --- shared visuals ----------------------------------------------------------------------

        /// <summary>A 1px themed outline over a picker surface (hit-test transparent).</summary>
        private static Rectangle BorderOverlay()
        {
            Rectangle outline = new()
            {
                RadiusX = Radius,
                RadiusY = Radius,
                StrokeThickness = 1,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true,
            };
            outline.SetResourceReference(Shape.StrokeProperty, "Brush.Border");
            return outline;
        }

        /// <summary>The ring thumb shared by all three surfaces; positioned via Canvas.Left/Top.</summary>
        private static Grid BuildThumb()
        {
            Grid thumb = new() { Width = ThumbSize, Height = ThumbSize };
            thumb.Children.Add(new Ellipse
            {
                Stroke = Brushes.White,
                StrokeThickness = 2,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 4, ShadowDepth = 0, Opacity = 0.6 },
            });
            return thumb;
        }

        /// <summary>Hosts a thumb on a Canvas: unlike a Grid cell it never layout-clips the ring when
        /// it hangs over the surface's edge.</summary>
        private static Canvas ThumbLayer(Grid thumb)
        {
            Canvas layer = new() { IsHitTestVisible = false };
            layer.Children.Add(thumb);
            return layer;
        }

        private static void AttachDrag(Grid surface, Action<Point> update)
        {
            bool dragging = false;
            surface.Background = Brushes.Transparent; // Grid needs a background to be hit-testable
            surface.MouseLeftButtonDown += (_, e) =>
            {
                dragging = true;
                surface.CaptureMouse();
                update(e.GetPosition(surface));
            };
            surface.MouseMove += (_, e) =>
            {
                if (dragging)
                {
                    update(e.GetPosition(surface));
                }
            };
            surface.MouseLeftButtonUp += (_, _) =>
            {
                dragging = false;
                surface.ReleaseMouseCapture();
            };
            surface.LostMouseCapture += (_, _) => dragging = false;
        }

        // --- state ------------------------------------------------------------------------------

        /// <summary>Adopts a full color, keeping the current hue (and saturation) when the new color
        /// is gray/black and RGB→HSV can no longer express them.</summary>
        private void SetColor(Color color, FrameworkElement? except = null)
        {
            (double h, double s, double v) = RgbToHsv(color);
            if (s > 0)
            {
                _h = h;
            }
            if (v > 0)
            {
                _s = s;
            }
            _v = v;
            _a = _alpha ? color.A : (byte)0xFF;
            Sync(except);
        }

        /// <summary>Recomputes the color from HSV(A) and pushes it into every visual and input box —
        /// except the box currently being typed in, so the caret is not disturbed mid-edit.</summary>
        private void Sync(FrameworkElement? except)
        {
            Current = HsvToRgb(_h, _s, _v, _a);

            _hueBase.Color = HsvToRgb(_h, 1, 1, 0xFF);
            _preview.Color = Current;
            _alphaFrom.Color = Color.FromRgb(Current.R, Current.G, Current.B);
            _alphaTo.Color = Color.FromArgb(0, Current.R, Current.G, Current.B);

            Place(_fieldThumb, _s * _fieldWidth, (1 - _v) * FieldHeight);
            Place(_hueThumb, BarWidth / 2, _h / 360 * FieldHeight);
            if (_alpha)
            {
                Place(_alphaThumb, BarWidth / 2, (1 - _a / 255d) * FieldHeight);
            }

            _syncing = true;
            try
            {
                Set(_hexBox, FormatHex(Current, _alpha));
                Set(_rBox, Current.R.ToString(CultureInfo.InvariantCulture));
                Set(_gBox, Current.G.ToString(CultureInfo.InvariantCulture));
                Set(_bBox, Current.B.ToString(CultureInfo.InvariantCulture));
                if (_aBox is not null)
                {
                    Set(_aBox, Current.A.ToString(CultureInfo.InvariantCulture));
                }
            }
            finally
            {
                _syncing = false;
            }

            void Set(TextBox box, string text)
            {
                if (!ReferenceEquals(box, except) && box.Text != text)
                {
                    box.Text = text;
                }
            }
        }

        private static void Place(Grid thumb, double x, double y)
        {
            Canvas.SetLeft(thumb, x - ThumbSize / 2);
            Canvas.SetTop(thumb, y - ThumbSize / 2);
        }

        // --- helpers ------------------------------------------------------------------------------

        private static double Clamp01(double value) => Math.Clamp(value, 0, 1);

        private static string FormatHex(Color c, bool alpha) => alpha
            ? $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}"
            : $"#{c.R:X2}{c.G:X2}{c.B:X2}";

        private static bool TryParseHex(string text, bool allowAlpha, out Color color)
        {
            color = default;
            string t = text.Trim().TrimStart('#');
            if (t.Length is not (6 or 8) ||
                !uint.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v))
            {
                return false;
            }

            byte a = allowAlpha && t.Length == 8 ? (byte)(v >> 24) : (byte)0xFF;
            color = Color.FromArgb(a, (byte)(v >> 16), (byte)(v >> 8), (byte)v);
            return true;
        }

        private static (double H, double S, double V) RgbToHsv(Color c)
        {
            double r = c.R / 255d, g = c.G / 255d, b = c.B / 255d;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            double h = 0;
            if (delta > 0)
            {
                h = max == r ? 60 * (((g - b) / delta + 6) % 6)
                  : max == g ? 60 * ((b - r) / delta + 2)
                  : 60 * ((r - g) / delta + 4);
            }

            return (h, max <= 0 ? 0 : delta / max, max);
        }

        private static Color HsvToRgb(double h, double s, double v, byte a)
        {
            h = ((h % 360) + 360) % 360;
            double c = v * s;
            double x = c * (1 - Math.Abs(h / 60 % 2 - 1));
            double m = v - c;

            (double r, double g, double b) = h switch
            {
                < 60 => (c, x, 0d),
                < 120 => (x, c, 0d),
                < 180 => (0d, c, x),
                < 240 => (0d, x, c),
                < 300 => (x, 0d, c),
                _ => (c, 0d, x),
            };

            return Color.FromArgb(a,
                (byte)Math.Round((r + m) * 255),
                (byte)Math.Round((g + m) * 255),
                (byte)Math.Round((b + m) * 255));
        }

        /// <summary>An 8px checkerboard tile (shows through transparent colors).</summary>
        private static Brush Checkerboard()
        {
            GeometryGroup dark = new();
            dark.Children.Add(new RectangleGeometry(new Rect(0, 0, 4, 4)));
            dark.Children.Add(new RectangleGeometry(new Rect(4, 4, 4, 4)));

            DrawingGroup tile = new();
            tile.Children.Add(new GeometryDrawing(Brushes.White, null, new RectangleGeometry(new Rect(0, 0, 8, 8))));
            tile.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)), null, dark));

            DrawingBrush brush = new(tile)
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 8, 8),
                ViewportUnits = BrushMappingMode.Absolute,
            };
            brush.Freeze();
            return brush;
        }

        private static TBrush Frozen<TBrush>(TBrush brush) where TBrush : Brush
        {
            brush.Freeze();
            return brush;
        }
    }
}
