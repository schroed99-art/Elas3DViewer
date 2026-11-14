using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace Elas3DView
{
    /// <summary>
    /// Farben und Steuerelement-Stile nach der Handschrift von elas3dstudio.de,
    /// wie sie ElasPrintFlow seit dem 12.08.2026 trägt: warmes Dunkelbraun statt
    /// kühlem Blau, Gold als Akzent.
    ///
    /// Drei Regeln aus dem Design-Handoff, die hier bewusst eingehalten werden:
    ///   * keine Schatten und kein Glas — Erhebung entsteht durch Kante plus Flächenwechsel
    ///   * Schaltflächen sind NIE gefüllt; primär und sekundär trennen nur
    ///     goldene Kante und Schriftgewicht
    ///   * Radien 8–12 px, Pillen 999 px
    ///
    /// Manrope und Cormorant Garamond sind seit dem 14.11.2026 eingebettet, aber
    /// nur in den Schnitten des Kopfbands (BrandFonts.cs). Für die übrige
    /// Oberfläche stehen weiter Segoe UI und — für Zahlen — Constantia, das als
    /// einzige mitgelieferte Serifenschrift Mediävalziffern mitbringt.
    /// </summary>
    public static class Theme
    {
        public static bool IsDark = true;
        public static event EventHandler Changed;

        public const string UiFont = "Segoe UI Variable Text, Segoe UI";
        public const string NumFont = "Constantia, Cambria, Georgia";

        private class Palette
        {
            public Color Bg, Bar, Panel, Elevated, Preview;
            public Color Border, Divider, Input;
            public Color Gold, GoldSoft, GoldText;
            public Color Text, TextDim, TextMuted, TextWeak;
            public Color Positive, Negative, Neutral;
        }

        // Werte wörtlich aus elasprintflow-design.md
        private static readonly Palette Dark = new Palette
        {
            Bg = H("17141C"),
            Bar = H("15121A"),
            Panel = H("1C1822"),
            Elevated = H("1A1622"),
            Preview = H("201B27"),
            Border = H("2A2434"),
            Divider = H("241F2C"),
            Input = H("2E2838"),
            Gold = H("D39A39"),
            GoldSoft = H("E7C9A0"),
            GoldText = H("F0D6A6"),
            Text = H("FBF4EE"),
            TextDim = H("F2E9E3"),
            TextMuted = H("8D8494"),
            TextWeak = H("6C6474"),
            Positive = H("7FA87F"),
            Negative = H("C97A6D"),
            Neutral = H("6F9EAB")
        };

        // Helle Entsprechung in derselben warmen Familie
        private static readonly Palette Light = new Palette
        {
            Bg = H("F4EFE8"),
            Bar = H("EFE8DF"),
            Panel = H("FCF9F5"),
            Elevated = H("FFFDFA"),
            Preview = H("EFE8DE"),
            Border = H("DFD4C4"),
            Divider = H("E8DFD3"),
            Input = H("FFFFFF"),
            Gold = H("A9761E"),
            GoldSoft = H("8A5F14"),
            GoldText = H("6E4B0F"),
            Text = H("241F2C"),
            TextDim = H("3A3340"),
            TextMuted = H("6C6474"),
            TextWeak = H("8D8494"),
            Positive = H("4F7A50"),
            Negative = H("A44A3C"),
            Neutral = H("4A7683")
        };

        private static Color H(string hex)
        {
            return Color.FromRgb(
                Convert.ToByte(hex.Substring(0, 2), 16),
                Convert.ToByte(hex.Substring(2, 2), 16),
                Convert.ToByte(hex.Substring(4, 2), 16));
        }

        private static Palette Current { get { return IsDark ? Dark : Light; } }

        public static Color GoldColor { get { return Current.Gold; } }
        public static Color NegativeColor { get { return Current.Negative; } }

        /// <summary>
        /// Vorschaufläche mit feiner Schraffur — im Handoff die Fläche, auf der
        /// Modellbilder liegen.
        /// </summary>
        /// <param name="scale">
        /// Die Kachel ist in festen Einheiten angegeben. Beim Rendern in ein großes
        /// Bild muss sie mitwachsen, sonst erscheint die Schraffur dort viel feiner
        /// als auf dem Bildschirm. Für die Ansicht ist der Wert 1.
        /// </param>
        public static Brush PreviewBrush(double scale)
        {
            if (scale < 0.05) scale = 1.0;
            Palette p = Current;
            DrawingGroup dg = new DrawingGroup();

            GeometryDrawing bg = new GeometryDrawing(
                new SolidColorBrush(p.Preview),
                null,
                new RectangleGeometry(new Rect(0, 0, 10, 10)));
            dg.Children.Add(bg);

            Pen pen = new Pen(new SolidColorBrush(Mix(p.Preview, p.Border, 0.75)), 1.0);
            GeometryGroup lines = new GeometryGroup();
            lines.Children.Add(new LineGeometry(new Point(-2, 12), new Point(12, -2)));
            lines.Children.Add(new LineGeometry(new Point(3, 12), new Point(12, 3)));
            lines.Children.Add(new LineGeometry(new Point(-2, 7), new Point(7, -2)));
            dg.Children.Add(new GeometryDrawing(null, pen, lines));

            double tile = 10.0 * scale;
            DrawingBrush b = new DrawingBrush(dg);
            b.TileMode = TileMode.Tile;
            b.Viewport = new Rect(0, 0, tile, tile);
            b.ViewportUnits = BrushMappingMode.Absolute;
            b.Viewbox = new Rect(0, 0, 10, 10);
            b.ViewboxUnits = BrushMappingMode.Absolute;
            b.Freeze();
            return b;
        }

        private static Color Mix(Color a, Color b, double t)
        {
            return Color.FromRgb(
                (byte)(a.R + (b.R - a.R) * t),
                (byte)(a.G + (b.G - a.G) * t),
                (byte)(a.B + (b.B - a.B) * t));
        }

        // ---------------------------------------------------------- Anwenden

        public static void Install()
        {
            ResourceDictionary rd = (ResourceDictionary)XamlReader.Parse(Xaml);
            Application.Current.Resources.MergedDictionaries.Add(rd);
            PushColors();
        }

        public static void Apply(bool dark)
        {
            IsDark = dark;
            PushColors();
            if (Changed != null) Changed(null, EventArgs.Empty);
        }

        private static void PushColors()
        {
            Palette p = Current;
            Set("Bg", p.Bg);
            Set("Bar", p.Bar);
            Set("Panel", p.Panel);
            Set("Elevated", p.Elevated);
            Set("Preview", p.Preview);
            Set("Border", p.Border);
            Set("Divider", p.Divider);
            Set("Input", p.Input);
            Set("Gold", p.Gold);
            Set("GoldSoft", p.GoldSoft);
            Set("GoldText", p.GoldText);
            Set("Text", p.Text);
            Set("TextDim", p.TextDim);
            Set("TextMuted", p.TextMuted);
            Set("TextWeak", p.TextWeak);
            Set("Positive", p.Positive);
            Set("Negative", p.Negative);
            Set("Neutral", p.Neutral);
            Application.Current.Resources["ScrimBrush"] =
                Frozen(Color.FromArgb(0xAA, 0x0C, 0x0A, 0x10));
        }

        private static void Set(string key, Color c)
        {
            Application.Current.Resources[key] = Frozen(c);
        }

        private static SolidColorBrush Frozen(Color c)
        {
            SolidColorBrush b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        // ------------------------------------------- Dunkle Fensterleiste (Win11)

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public static void ApplyTitleBar(Window w)
        {
            try
            {
                System.Windows.Interop.WindowInteropHelper h = new System.Windows.Interop.WindowInteropHelper(w);
                if (h.Handle == IntPtr.Zero) return;
                int on = IsDark ? 1 : 0;
                if (DwmSetWindowAttribute(h.Handle, 20, ref on, sizeof(int)) != 0)
                    DwmSetWindowAttribute(h.Handle, 19, ref on, sizeof(int));
            }
            catch { }
        }

        // ---------------------------------------------------------- Stile

        private const string Xaml = @"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>

  <!-- ==================== Schaltflächen ====================
       Nie gefüllt. Sekundär: ruhige Kante. Primär: goldene Kante,
       goldene Schrift, halbfett. Warnend: Kante und Schrift in Rot. -->
  <Style x:Key='Btn' TargetType='Button'>
    <Setter Property='Foreground' Value='{DynamicResource TextDim}'/>
    <Setter Property='BorderBrush' Value='{DynamicResource Border}'/>
    <Setter Property='FontFamily' Value='Segoe UI Variable Text, Segoe UI'/>
    <Setter Property='FontSize' Value='12.5'/>
    <Setter Property='Padding' Value='13,6,13,6'/>
    <Setter Property='Margin' Value='0,0,6,0'/>
    <Setter Property='SnapsToDevicePixels' Value='True'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='b' CornerRadius='9' Background='Transparent'
                  BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1'
                  Padding='{TemplateBinding Padding}'>
            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='b' Property='Background' Value='{DynamicResource Input}'/>
              <Setter TargetName='b' Property='BorderBrush' Value='{DynamicResource Gold}'/>
            </Trigger>
            <Trigger Property='IsPressed' Value='True'>
              <Setter TargetName='b' Property='Background' Value='{DynamicResource Divider}'/>
            </Trigger>
            <Trigger Property='IsEnabled' Value='False'>
              <Setter Property='Opacity' Value='0.38'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='BtnPrimary' TargetType='Button' BasedOn='{StaticResource Btn}'>
    <Setter Property='Foreground' Value='{DynamicResource GoldText}'/>
    <Setter Property='BorderBrush' Value='{DynamicResource Gold}'/>
    <Setter Property='FontWeight' Value='SemiBold'/>
  </Style>

  <Style x:Key='BtnDanger' TargetType='Button' BasedOn='{StaticResource Btn}'>
    <Setter Property='Foreground' Value='{DynamicResource Negative}'/>
  </Style>

  <!-- Pille im Kopfband, nach ElasPrintFlow (Titelleiste.tsx: 44 px hoch, 13 px
       Schrift, Kante #2A2430 auf #1B1721). Feste Farben statt Palette: Das Band
       bleibt dunkel, auch im hellen Design. -->
  <Style x:Key='BtnBand' TargetType='Button'>
    <Setter Property='Foreground' Value='#CFC2BC'/>
    <Setter Property='FontFamily' Value='Segoe UI Variable Text, Segoe UI'/>
    <Setter Property='FontSize' Value='13'/>
    <Setter Property='Height' Value='44'/>
    <Setter Property='Padding' Value='18,0,18,0'/>
    <Setter Property='SnapsToDevicePixels' Value='True'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Button'>
          <Border x:Name='b' CornerRadius='22' Background='#1B1721'
                  BorderBrush='#2A2430' BorderThickness='1'
                  Padding='{TemplateBinding Padding}'>
            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='b' Property='BorderBrush' Value='#8CD39A39'/>
              <Setter Property='Foreground' Value='#F0D6A6'/>
            </Trigger>
            <Trigger Property='IsPressed' Value='True'>
              <Setter TargetName='b' Property='Background' Value='#241F2C'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='BtnChip' TargetType='Button' BasedOn='{StaticResource Btn}'>
    <Setter Property='Padding' Value='11,4,11,4'/>
    <Setter Property='Margin' Value='0,0,4,0'/>
    <Setter Property='FontSize' Value='12'/>
    <Setter Property='Foreground' Value='{DynamicResource TextMuted}'/>
  </Style>

  <!-- ==================== Umschalter ==================== -->
  <Style x:Key='Chk' TargetType='CheckBox'>
    <Setter Property='Foreground' Value='{DynamicResource TextMuted}'/>
    <Setter Property='FontFamily' Value='Segoe UI Variable Text, Segoe UI'/>
    <Setter Property='FontSize' Value='12.5'/>
    <Setter Property='VerticalAlignment' Value='Center'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='CheckBox'>
          <StackPanel Orientation='Horizontal' Background='Transparent'>
            <Border x:Name='box' Width='16' Height='16' CornerRadius='5'
                    Background='Transparent' BorderThickness='1'
                    BorderBrush='{DynamicResource Border}' VerticalAlignment='Center'>
              <Path x:Name='tick' Data='M 2,6 L 5,9 L 11,2' StrokeThickness='1.8'
                    Stroke='{DynamicResource Gold}' Visibility='Collapsed'
                    StrokeEndLineCap='Round' StrokeStartLineCap='Round'/>
            </Border>
            <ContentPresenter Margin='7,0,0,0' VerticalAlignment='Center'/>
          </StackPanel>
          <ControlTemplate.Triggers>
            <Trigger Property='IsChecked' Value='True'>
              <Setter TargetName='box' Property='BorderBrush' Value='{DynamicResource Gold}'/>
              <Setter TargetName='tick' Property='Visibility' Value='Visible'/>
              <Setter Property='Foreground' Value='{DynamicResource GoldText}'/>
            </Trigger>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='box' Property='BorderBrush' Value='{DynamicResource Gold}'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- ==================== Textfeld ==================== -->
  <Style x:Key='Txt' TargetType='TextBox'>
    <Setter Property='Foreground' Value='{DynamicResource Text}'/>
    <Setter Property='Background' Value='{DynamicResource Input}'/>
    <Setter Property='CaretBrush' Value='{DynamicResource Gold}'/>
    <Setter Property='SelectionBrush' Value='{DynamicResource Gold}'/>
    <Setter Property='FontFamily' Value='Segoe UI Variable Text, Segoe UI'/>
    <Setter Property='FontSize' Value='14'/>
    <Setter Property='Padding' Value='9,7,9,7'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='TextBox'>
          <Border x:Name='b' CornerRadius='9' Background='{TemplateBinding Background}'
                  BorderBrush='{DynamicResource Border}' BorderThickness='1'>
            <ScrollViewer x:Name='PART_ContentHost' Margin='{TemplateBinding Padding}'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsFocused' Value='True'>
              <Setter TargetName='b' Property='BorderBrush' Value='{DynamicResource Gold}'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- Dasselbe Feld für Passwörter; PasswordBox erbt nicht von TextBox. -->
  <Style x:Key='Pwd' TargetType='PasswordBox'>
    <Setter Property='Foreground' Value='{DynamicResource Text}'/>
    <Setter Property='Background' Value='{DynamicResource Input}'/>
    <Setter Property='CaretBrush' Value='{DynamicResource Gold}'/>
    <Setter Property='SelectionBrush' Value='{DynamicResource Gold}'/>
    <Setter Property='FontFamily' Value='Segoe UI Variable Text, Segoe UI'/>
    <Setter Property='FontSize' Value='14'/>
    <Setter Property='Padding' Value='9,7,9,7'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='PasswordBox'>
          <Border x:Name='b' CornerRadius='9' Background='{TemplateBinding Background}'
                  BorderBrush='{DynamicResource Border}' BorderThickness='1'>
            <ScrollViewer x:Name='PART_ContentHost' Margin='{TemplateBinding Padding}'/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsFocused' Value='True'>
              <Setter TargetName='b' Property='BorderBrush' Value='{DynamicResource Gold}'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- ==================== Schieberegler ==================== -->
  <Style x:Key='Sld' TargetType='Slider'>
    <Setter Property='VerticalAlignment' Value='Center'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Slider'>
          <Grid Height='22' Background='Transparent'>
            <Border Height='3' CornerRadius='999' VerticalAlignment='Center'
                    Background='{DynamicResource Border}'/>
            <Track x:Name='PART_Track'>
              <Track.DecreaseRepeatButton>
                <RepeatButton Command='Slider.DecreaseLarge' Focusable='False'>
                  <RepeatButton.Template>
                    <ControlTemplate TargetType='RepeatButton'>
                      <Border Height='3' CornerRadius='999' VerticalAlignment='Center'
                              Background='{DynamicResource Gold}'/>
                    </ControlTemplate>
                  </RepeatButton.Template>
                </RepeatButton>
              </Track.DecreaseRepeatButton>
              <Track.Thumb>
                <Thumb Width='13' Height='13'>
                  <Thumb.Template>
                    <ControlTemplate TargetType='Thumb'>
                      <Border CornerRadius='999' Background='{DynamicResource Gold}'/>
                    </ControlTemplate>
                  </Thumb.Template>
                </Thumb>
              </Track.Thumb>
              <Track.IncreaseRepeatButton>
                <RepeatButton Command='Slider.IncreaseLarge' Focusable='False'>
                  <RepeatButton.Template>
                    <ControlTemplate TargetType='RepeatButton'>
                      <Border Background='Transparent'/>
                    </ControlTemplate>
                  </RepeatButton.Template>
                </RepeatButton>
              </Track.IncreaseRepeatButton>
            </Track>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- ==================== Bildlaufleiste ==================== -->
  <Style x:Key='SbThumb' TargetType='Thumb'>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Thumb'>
          <Border CornerRadius='999' Background='{DynamicResource Border}' Margin='3,0,3,0'/>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
  <Style TargetType='ScrollBar'>
    <Setter Property='Width' Value='11'/>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ScrollBar'>
          <Grid Background='Transparent'>
            <Track x:Name='PART_Track' IsDirectionReversed='True'>
              <Track.DecreaseRepeatButton>
                <RepeatButton Command='ScrollBar.PageUpCommand' Opacity='0' Focusable='False'/>
              </Track.DecreaseRepeatButton>
              <Track.Thumb><Thumb Style='{StaticResource SbThumb}'/></Track.Thumb>
              <Track.IncreaseRepeatButton>
                <RepeatButton Command='ScrollBar.PageDownCommand' Opacity='0' Focusable='False'/>
              </Track.IncreaseRepeatButton>
            </Track>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- ==================== Liste ====================
       Aktiver Eintrag mit 2 px Goldkante links statt Kapsel — wie die
       Seitenleiste in ElasPrintFlow. -->
  <Style x:Key='List' TargetType='ListBox'>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='BorderThickness' Value='0'/>
    <Setter Property='Foreground' Value='{DynamicResource TextDim}'/>
    <Setter Property='FontFamily' Value='Segoe UI Variable Text, Segoe UI'/>
    <Setter Property='FontSize' Value='12.5'/>
    <Setter Property='ScrollViewer.HorizontalScrollBarVisibility' Value='Disabled'/>
    <Setter Property='HorizontalContentAlignment' Value='Stretch'/>
  </Style>

  <Style x:Key='ListItem' TargetType='ListBoxItem'>
    <Setter Property='Foreground' Value='{DynamicResource TextDim}'/>
    <Setter Property='Padding' Value='10,7,8,7'/>
    <Setter Property='Margin' Value='0,0,0,1'/>
    <Setter Property='HorizontalContentAlignment' Value='Stretch'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ListBoxItem'>
          <Border x:Name='b' CornerRadius='0,8,8,0' Background='Transparent'
                  BorderThickness='2,0,0,0' BorderBrush='Transparent'
                  Padding='{TemplateBinding Padding}' SnapsToDevicePixels='True'>
            <ContentPresenter/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='b' Property='Background' Value='{DynamicResource Divider}'/>
            </Trigger>
            <Trigger Property='IsSelected' Value='True'>
              <Setter TargetName='b' Property='Background' Value='{DynamicResource Elevated}'/>
              <Setter TargetName='b' Property='BorderBrush' Value='{DynamicResource Gold}'/>
              <Setter Property='Foreground' Value='{DynamicResource GoldText}'/>
              <Setter Property='FontWeight' Value='SemiBold'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- ==================== Auswahlfeld ==================== -->
  <Style x:Key='Combo' TargetType='ComboBox'>
    <Setter Property='Foreground' Value='{DynamicResource TextDim}'/>
    <Setter Property='FontFamily' Value='Segoe UI Variable Text, Segoe UI'/>
    <Setter Property='FontSize' Value='12.5'/>
    <Setter Property='Height' Value='30'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ComboBox'>
          <Grid>
            <ToggleButton x:Name='tb' Focusable='False' ClickMode='Press'
                          IsChecked='{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}'>
              <ToggleButton.Template>
                <ControlTemplate TargetType='ToggleButton'>
                  <Border x:Name='bd' CornerRadius='9' Background='Transparent'
                          BorderBrush='{DynamicResource Border}' BorderThickness='1'>
                    <Path HorizontalAlignment='Right' VerticalAlignment='Center'
                          Margin='0,0,10,0' Data='M 0,0 L 4,4 L 8,0'
                          Stroke='{DynamicResource TextMuted}' StrokeThickness='1.4'/>
                  </Border>
                  <ControlTemplate.Triggers>
                    <Trigger Property='IsMouseOver' Value='True'>
                      <Setter TargetName='bd' Property='BorderBrush' Value='{DynamicResource Gold}'/>
                    </Trigger>
                  </ControlTemplate.Triggers>
                </ControlTemplate>
              </ToggleButton.Template>
            </ToggleButton>
            <ContentPresenter Content='{TemplateBinding SelectionBoxItem}' IsHitTestVisible='False'
                              Margin='11,0,26,0' VerticalAlignment='Center'/>
            <Popup IsOpen='{TemplateBinding IsDropDownOpen}' Placement='Bottom' AllowsTransparency='True'
                   Focusable='False' PopupAnimation='Fade'>
              <Border Background='{DynamicResource Elevated}' BorderBrush='{DynamicResource Border}'
                      BorderThickness='1' CornerRadius='10' Margin='0,4,0,0' Padding='4'
                      MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}'>
                <ScrollViewer MaxHeight='320'>
                  <StackPanel IsItemsHost='True'/>
                </ScrollViewer>
              </Border>
            </Popup>
          </Grid>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style x:Key='ComboItem' TargetType='ComboBoxItem'>
    <Setter Property='Foreground' Value='{DynamicResource TextDim}'/>
    <Setter Property='Padding' Value='9,6,9,6'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ComboBoxItem'>
          <Border x:Name='b' CornerRadius='7' Background='Transparent' Padding='{TemplateBinding Padding}'>
            <ContentPresenter/>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='b' Property='Background' Value='{DynamicResource Divider}'/>
            </Trigger>
            <Trigger Property='IsSelected' Value='True'>
              <Setter Property='Foreground' Value='{DynamicResource GoldText}'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- ==================== Kontextmenü ==================== -->
  <Style TargetType='ContextMenu'>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='BorderThickness' Value='0'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ContextMenu'>
          <Border Background='{DynamicResource Elevated}' BorderBrush='{DynamicResource Border}'
                  BorderThickness='1' CornerRadius='11' Padding='5'>
            <StackPanel IsItemsHost='True'/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='MenuItem'>
    <Setter Property='Foreground' Value='{DynamicResource TextDim}'/>
    <Setter Property='FontFamily' Value='Segoe UI Variable Text, Segoe UI'/>
    <Setter Property='FontSize' Value='12.5'/>
    <Setter Property='Padding' Value='12,7,16,7'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='MenuItem'>
          <Border x:Name='b' CornerRadius='7' Background='Transparent' Padding='{TemplateBinding Padding}'>
            <Grid>
              <Grid.ColumnDefinitions>
                <ColumnDefinition Width='*'/>
                <ColumnDefinition Width='Auto'/>
              </Grid.ColumnDefinitions>
              <ContentPresenter Grid.Column='0' ContentSource='Header' VerticalAlignment='Center'/>
              <TextBlock Grid.Column='1' Text='{TemplateBinding InputGestureText}' Margin='26,0,0,0'
                         Foreground='{DynamicResource TextWeak}' VerticalAlignment='Center'/>
            </Grid>
          </Border>
          <ControlTemplate.Triggers>
            <Trigger Property='IsHighlighted' Value='True'>
              <Setter TargetName='b' Property='Background' Value='{DynamicResource Divider}'/>
              <Setter Property='Foreground' Value='{DynamicResource GoldText}'/>
            </Trigger>
            <Trigger Property='IsEnabled' Value='False'>
              <Setter Property='Opacity' Value='0.38'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <Style TargetType='Separator'>
    <Setter Property='Height' Value='1'/>
    <Setter Property='Margin' Value='8,5,8,5'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='Separator'>
          <Border Height='1' Background='{DynamicResource Divider}'/>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- ==================== Kurzhinweis ==================== -->
  <Style TargetType='ToolTip'>
    <Setter Property='Foreground' Value='{DynamicResource TextDim}'/>
    <Setter Property='FontFamily' Value='Segoe UI Variable Text, Segoe UI'/>
    <Setter Property='FontSize' Value='12'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='ToolTip'>
          <Border Background='{DynamicResource Elevated}' BorderBrush='{DynamicResource Border}'
                  BorderThickness='1' CornerRadius='7' Padding='9,5,9,5'>
            <ContentPresenter/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>

  <!-- ==================== Trennbalken ==================== -->
  <Style TargetType='GridSplitter'>
    <Setter Property='Background' Value='Transparent'/>
    <Setter Property='Template'>
      <Setter.Value>
        <ControlTemplate TargetType='GridSplitter'>
          <Border Background='Transparent'>
            <Border Width='1' HorizontalAlignment='Center' Background='{DynamicResource Divider}'/>
          </Border>
        </ControlTemplate>
      </Setter.Value>
    </Setter>
  </Style>
</ResourceDictionary>";
    }
}
