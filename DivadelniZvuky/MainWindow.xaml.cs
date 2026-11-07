using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;

namespace DivadelniZvuky;

public sealed class Cue
{
 public string Id { get; set; } = Guid.NewGuid().ToString("N");
 public string Text { get; set; } = "Nový zvuk";
 public string? Note { get; set; }
 public bool Used { get; set; }
 public double Volume { get; set; } = 1;
 public bool FadeOut { get; set; }
 public int FadeSeconds { get; set; } = 3;
 public string? AudioEntry { get; set; }
 [System.Text.Json.Serialization.JsonIgnore] public string? Source { get; set; }
}
public sealed class ShowProject
{
 public int Version { get; set; } = 2;
 public double StageWidth { get; set; } = 1000;
 public double StageHeight { get; set; } = 490;
 public bool StageAutoSize { get; set; } = true;
 public double MasterVolume { get; set; } = 1;
 public bool LayoutLocked { get; set; }
 public List<Cue> Cues { get; set; } = [];
 public List<ShowSection> Sections { get; set; } = [];
 public SectionSplit? LayoutTree { get; set; }
}

public partial class MainWindow : Window
{
 internal bool TestMode;
 readonly MediaPlayer player = new();
 readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(30) };
 readonly string sessionFolder = Path.Combine(Path.GetTempPath(), "DivadelniZvuky", Guid.NewGuid().ToString("N"));
 ShowProject project = new();
 string? projectPath;
 Cue? active;
 bool playing, updatingSeek, dirty, pendingPlay;
 bool syncingVolume;
 bool fullscreen;
 int modeTransitionRevision;
 WindowState previousWindowState;
 WindowStyle previousWindowStyle;
 ResizeMode previousResizeMode;
 Rect previousBounds;
 bool previousTopmost;
 double lastAudibleVolume = 1;
 static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".wav", ".wma", ".aif", ".aiff", ".m4a", ".aac", ".flac" };
 static string AudioExtension(string path)
 {
  string extension = Path.GetExtension(path).ToLowerInvariant();
  if (!AudioExtensions.Contains(extension)) throw new InvalidDataException("Nepodporovaná přípona zvuku: " + extension);
  return extension;
 }

 public MainWindow()
 {
  InitializeComponent();
  SourceInitialized += (_, _) => ApplyDarkTitleBar(this);
  Activated += (_, _) => { if (fullscreen) Topmost = true; };
  Deactivated += (_, _) => { if (fullscreen) Topmost = previousTopmost; };
  Background = MeshBackground(false);
  InitializeSections();
  Directory.CreateDirectory(sessionFolder);
  player.MediaOpened += (_, _) =>
  {
   Seek.Maximum = player.NaturalDuration.HasTimeSpan ? Math.Max(0.01, player.NaturalDuration.TimeSpan.TotalSeconds) : 1;
   DurationText.Text = FormatTime(Seek.Maximum);
   ApplyVolume();
   if (pendingPlay) { pendingPlay = false; player.Play(); SetPlaying(true); }
  };
  player.MediaEnded += (_, _) => { SetPlaying(false); UpdatePosition(); };
  player.MediaFailed += (_, e) => { pendingPlay = false; SetPlaying(false); MessageBox.Show(this, "Zvuk nelze přehrát.\n" + e.ErrorException.Message, "Přehrávání", MessageBoxButton.OK, MessageBoxImage.Error); };
  timer.Tick += (_, _) => { ApplyVolume(); UpdatePosition(); };
  timer.Start();
  Loaded += (_, _) => { FitStage(); RenderTiles(); };
  Closing += (_, e) => { if (!TestMode && !ConfirmDiscard()) e.Cancel = true; };
  Closed += (_, _) => { timer.Stop(); contactTimer.Stop(); player.Close(); try { Directory.Delete(sessionFolder, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } };
  PreviewKeyDown += (_, e) =>
  {
   if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z) { UndoLayout(); e.Handled = true; }
   if (e.Key == Key.F11) { if (!e.IsRepeat) ToggleFullscreen(); e.Handled = true; }
   if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S) { SaveProject(); e.Handled = true; }
   if (e.Key == Key.Escape) { Stop(); e.Handled = true; }
  };
 }

 void MarkDirty() { dirty = true; UpdateTitle(); }
 void UpdateTitle()
 {
  string name = projectPath is null ? "Nové představení" : Path.GetFileNameWithoutExtension(projectPath);
  ProjectLabel.Text = name + (dirty ? " •" : "");
  Title = name + " · TheatreTiles 0.2.3";
 }
 void FullscreenClick(object sender, RoutedEventArgs e) => ToggleFullscreen();
 [DllImport("dwmapi.dll")]
 static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
 [DllImport("dwmapi.dll")]
 static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out int value, int size);
 [DllImport("user32.dll")]
 static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
 [DllImport("user32.dll")]
 [return: MarshalAs(UnmanagedType.Bool)]
 static extern bool RedrawWindow(IntPtr hwnd, IntPtr rect, IntPtr region, uint flags);
 [StructLayout(LayoutKind.Sequential)]
 struct NativeRect { public int Left, Top, Right, Bottom; }
 [StructLayout(LayoutKind.Sequential)]
 struct MonitorInfo { public int Size; public NativeRect Monitor, Work; public uint Flags; }
 [DllImport("user32.dll")]
 static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
 [DllImport("user32.dll", CharSet = CharSet.Auto)]
 [return: MarshalAs(UnmanagedType.Bool)]
 static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
 [DllImport("user32.dll", SetLastError = true)]
 [return: MarshalAs(UnmanagedType.Bool)]
 static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
 [DllImport("user32.dll")]
 [return: MarshalAs(UnmanagedType.Bool)]
 static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);
 static NativeRect FullMonitorBounds(IntPtr hwnd)
 {
  var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
  if (!GetMonitorInfo(MonitorFromWindow(hwnd, 2), ref info)) throw new InvalidOperationException("Nelze zjistit rozměry monitoru.");
  return info.Monitor;
 }
 static void ApplyDarkTitleBar(Window window)
 {
  var handle = new WindowInteropHelper(window).Handle;
  int enabled = 1, black = 0, white = 0x00F4F4F5;
  DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int));
  DwmSetWindowAttribute(handle, 35, ref black, sizeof(int));
  DwmSetWindowAttribute(handle, 36, ref white, sizeof(int));
 }
 void ToggleFullscreen()
 {
  var handle = new WindowInteropHelper(this).Handle;
  NativeRect monitor = FullMonitorBounds(handle);
  int disableTransitions = 1;
  DwmSetWindowAttribute(handle, 3, ref disableTransitions, sizeof(int));
  // Commit frame/style/layout changes together, without painting intermediate sizes.
  SendMessage(handle, 0x000B, IntPtr.Zero, IntPtr.Zero);
  try
  {
  if (!fullscreen)
  {
   previousWindowState = WindowState;
   previousWindowStyle = WindowStyle;
   previousResizeMode = ResizeMode;
   previousTopmost = Topmost;
   previousBounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
   WindowState = WindowState.Normal;
   WindowStyle = WindowStyle.None;
   ResizeMode = ResizeMode.NoResize;
   fullscreen = true;
   Topmost = true;
   // Physical monitor bounds include the taskbar, unlike the maximized work area.
   if (!SetWindowPos(handle, new IntPtr(-1), monitor.Left, monitor.Top, monitor.Right - monitor.Left, monitor.Bottom - monitor.Top, 0x0020))
    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
  }
  else
  {
   fullscreen = false;
   Topmost = previousTopmost;
   WindowState = WindowState.Normal;
   WindowStyle = previousWindowStyle;
   ResizeMode = previousResizeMode;
   Left = previousBounds.Left; Top = previousBounds.Top; Width = previousBounds.Width; Height = previousBounds.Height;
   WindowState = previousWindowState;
   fullscreen = false;
  }
  FullscreenGlyph.Data = Geometry.Parse(fullscreen
   ? "M2,2 L9,9 M3,9 L9,9 9,3 M22,2 L15,9 M15,3 L15,9 21,9 M2,22 L9,15 M3,15 L9,15 9,21 M22,22 L15,15 M15,21 L15,15 21,15"
   : "M8,2 L2,2 2,8 M2,2 L9,9 M16,2 L22,2 22,8 M22,2 L15,9 M2,16 L2,22 8,22 M2,22 L9,15 M22,16 L22,22 16,22 M22,22 L15,15");
  FullscreenButton.ToolTip = fullscreen ? "Zpět do okna (F11)" : "Celá obrazovka (F11)";
  }
  finally
  {
   FitStage(); RenderTiles(); UpdateLayout();
   ApplyDarkTitleBar(this);
   SendMessage(handle, 0x000B, new IntPtr(1), IntPtr.Zero);
   RedrawWindow(handle, IntPtr.Zero, IntPtr.Zero, 0x0581);
   int revision = ++modeTransitionRevision;
   if (Content is UIElement content)
   {
    var animation = new DoubleAnimation(.84, 1, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
    animation.Completed += (_, _) => { if (revision == modeTransitionRevision) { int restoreTransitions = 0; DwmSetWindowAttribute(handle, 3, ref restoreTransitions, sizeof(int)); } };
    content.BeginAnimation(UIElement.OpacityProperty, animation);
   }
   else { int restoreTransitions = 0; DwmSetWindowAttribute(handle, 3, ref restoreTransitions, sizeof(int)); }
  }
 }
 static Brush Color(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;
 static string FormatTime(double seconds) => TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(seconds >= 3600 ? @"h\:mm\:ss" : @"mm\:ss");
 static Brush MeshBackground(bool tile, bool used = false)
 {
  var group = new DrawingGroup();
  var bounds = new RectangleGeometry(new Rect(0, 0, 1, 1));
  void Layer(Brush brush) => group.Children.Add(new GeometryDrawing(brush, null, bounds));
  void Glow(string hex, double x, double y, double rx, double ry)
  {
   var color = (System.Windows.Media.Color)ColorConverter.ConvertFromString(hex);
   System.Windows.Media.Color Alpha(double opacity) => System.Windows.Media.Color.FromArgb((byte)(color.A * opacity), color.R, color.G, color.B);
   Layer(new RadialGradientBrush(new GradientStopCollection { new(color, 0), new(Alpha(.8), .24), new(Alpha(.28), .62), new(Alpha(0), 1) })
   { Center = new Point(x, y), GradientOrigin = new Point(x, y), RadiusX = rx, RadiusY = ry, ColorInterpolationMode = ColorInterpolationMode.ScRgbLinearInterpolation });
  }
  if (!tile)
  {
   Layer(Color("#03050D"));
   Glow("#293C70", .30, .08, .75, .8);
   Glow("#795D94", .92, .15, .83, .72);
   Glow("#235583", .08, .83, .86, .78);
   Glow("#6D7383", .96, .94, .69, .72);
   Glow("#C002050D", .38, .47, .42, .55);
  }
  else if (!used)
  {
   Layer(Color("#0C392F"));
   Glow("#48A55E", .10, .12, .92, .76);
   Glow("#168759", .90, .17, .78, .85);
   Glow("#7AA64D", .24, .86, .78, .67);
   Glow("#13715D", .88, .91, .8, .67);
   Glow("#950C392F", .48, .48, .5, .47);
  }
  else
  {
   Layer(Color("#4B1B30")); Glow("#A3424D", .1, .2, 1, .85); Glow("#B15E69", .8, .86, .9, .9); Glow("#6B254B", .8, .08, .8, .9);
  }
  var brush = new DrawingBrush(group) { Stretch = Stretch.Fill }; brush.Freeze(); return brush;
 }
 internal Border CreateTile(Cue cue, SectionSurface surface, double size)
 {
  var border = new Border
  {
   Width = size, Height = size, CornerRadius = new CornerRadius(surface.Model.AutoSizeTiles ? Math.Min(20, size / 5) : 20),
   Background = MeshBackground(true, cue.Used),
   Margin = new Thickness(0), BorderBrush = active == cue ? Color("#DDE7F2") : Brushes.Transparent,
   BorderThickness = new Thickness(active == cue ? Math.Min(2, size / 10) : 0),
   AllowDrop = true, Cursor = Cursors.Hand, ClipToBounds = true
  };
  var note = new TextBlock
  {
   Text = CueNoteText(cue), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Left,
   MaxWidth = 400, FontFamily = FontFamily, FontSize = 16, Foreground = Color("#F4F4F5")
  };
  var noteTip = new ToolTip
  {
   Content = note, Style = (Style)FindResource("CueNoteToolTip"),
   PlacementTarget = border, Placement = PlacementMode.Custom,
   CustomPopupPlacementCallback = static (size, _, _) =>
   [
    new(new Point(14, -size.Height - 10), PopupPrimaryAxis.Horizontal),
    new(new Point(-size.Width - 14, -size.Height - 10), PopupPrimaryAxis.Horizontal),
    new(new Point(14, 18), PopupPrimaryAxis.Horizontal),
    new(new Point(-size.Width - 14, 18), PopupPrimaryAxis.Horizontal)
   ]
  };
  border.ToolTip = noteTip;
  border.ToolTipOpening += (_, _) =>
   noteTip.PlacementRectangle = new Rect(Mouse.GetPosition(border), new Size(0, 0));
  border.MouseMove += (_, e) =>
  {
   if (noteTip.IsOpen)
    noteTip.PlacementRectangle = new Rect(e.GetPosition(border), new Size(0, 0));
  };
  ToolTipService.SetInitialShowDelay(border, 300);
  ToolTipService.SetShowDuration(border, int.MaxValue);
  var grid = new Grid { Margin = new Thickness(surface.Model.AutoSizeTiles ? Math.Min(18, size / 10) : 18) }; border.Child = grid;
  var label = new TextBlock
  {
   Text = cue.Text, FontSize = Math.Clamp(size / 8.15, surface.Model.AutoSizeTiles ? 1 : 14, 27), FontWeight = FontWeights.SemiBold, Foreground = Brushes.White,
   TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
   HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Center, MaxHeight = 145,
   IsHitTestVisible = false
  };
  grid.Children.Add(label);
  var grip = MakeGrip(Brushes.White);
  grip.HorizontalAlignment = HorizontalAlignment.Center; grip.VerticalAlignment = VerticalAlignment.Top;
  double gripScale = Math.Min(1, size / 70);
  grip.LayoutTransform = new ScaleTransform(gripScale, gripScale);
  grip.Cursor = Cursors.SizeAll; grip.Visibility = Visibility.Hidden;
  Point origin = default;
  grip.MouseLeftButtonDown += (_, e) => { origin = e.GetPosition(grip); e.Handled = true; };
  grip.MouseMove += (_, e) =>
  {
   if (!project.LayoutLocked && e.LeftButton == MouseButtonState.Pressed && (e.GetPosition(grip) - origin).Length > 5)
   {
    surface.BeginDrag(cue);
    try { DragDrop.DoDragDrop(grip, new DataObject("TheatreCue", cue.Id), DragDropEffects.Move); }
    finally { surface.EndDrag(false); }
   }
  };
  grid.Children.Add(grip);
  border.MouseEnter += (_, _) => grip.Visibility = project.LayoutLocked ? Visibility.Hidden : Visibility.Visible;
  border.MouseLeave += (_, _) => grip.Visibility = Visibility.Hidden;
  border.MouseLeftButtonDown += (_, e) => { e.Handled = true; if (!surface.IsDragging) StartCue(cue); };
  border.ContextMenu = CreateCueMenu(cue, label, note);
  return border;
 }
 static string CueNoteText(Cue cue) => string.IsNullOrWhiteSpace(cue.Note) ? "Poznámka…" : cue.Note;
 ContextMenu CreateCueMenu(Cue cue, TextBlock label, TextBlock note)
 {
  var menu = new ContextMenu { MinWidth = 280 };
  var upload = new MenuItem { Header = "Nahrát / změnit zvuk", Icon = new TextBlock { Text = "↑" } };
  upload.Click += (_, _) => Import(cue); menu.Items.Add(upload);
  var edit = new MenuItem { Header = "Upravit popisek", Icon = new TextBlock { Text = "✎" } };
  edit.Click += (_, _) => EditCueText(cue, label, note: note); menu.Items.Add(edit);
  var reset = new MenuItem { Header = "Označit jako nepřehraný", Icon = new TextBlock { Text = "↻" } };
  reset.Click += (_, _) => { cue.Used = false; MarkDirty(); RenderTiles(); }; menu.Items.Add(reset);
  menu.Items.Add(new Separator());
  var fade = new MenuItem { Header = "Fade" };
  foreach (int seconds in new[] { 0, 3, 5, 7 })
  {
   var preset = new MenuItem { Header = seconds == 0 ? "Vypnuto" : $"{seconds} s", IsCheckable = true, IsChecked = seconds == 0 ? !cue.FadeOut : cue.FadeOut && cue.FadeSeconds == seconds };
   preset.Click += (_, _) =>
   {
    cue.FadeOut = seconds > 0; if (seconds > 0) cue.FadeSeconds = seconds;
    foreach (MenuItem item in fade.Items) item.IsChecked = item == preset;
    MarkDirty(); ApplyVolume();
   };
   fade.Items.Add(preset);
  }
  menu.Items.Add(fade);
  menu.Items.Add(new Separator());
  var panel = new StackPanel { Width = 222, Margin = new Thickness(0, 6, 0, 6) };
  var caption = new TextBlock { Text = $"Hlasitost zvuku · {cue.Volume:P0}", Foreground = Brushes.White, FontSize = 16, Margin = new Thickness(0, 0, 0, 12) };
  var slider = new Slider { Minimum = 0, Maximum = 1, Value = cue.Volume, SmallChange = .01, LargeChange = .1, ToolTip = "Základní hlasitost tohoto zvuku" };
  panel.Children.Add(caption); panel.Children.Add(slider);
  slider.ValueChanged += (_, _) => { cue.Volume = slider.Value; caption.Text = $"Hlasitost zvuku · {cue.Volume:P0}"; MarkDirty(); ApplyVolume(); };
  menu.Items.Add(new MenuItem { Header = panel, StaysOpenOnClick = true, Focusable = false });
  menu.Items.Add(new Separator());
  var remove = new MenuItem { Header = "Odstranit dlaždici", IsEnabled = !project.LayoutLocked };
  remove.Click += (_, _) => DeleteCue(cue); menu.Items.Add(remove);
  return menu;
 }
 void EditCueText(Cue cue, TextBlock label, string title = "Upravit popisek", TextBlock? note = null)
 {
  var dialog = new Window { Owner = this, Title = title, Width = 420, Height = note is null ? 270 : 410, FontFamily = FontFamily, WindowStartupLocation = WindowStartupLocation.CenterOwner, ResizeMode = ResizeMode.NoResize, Background = Color("#1A1B29"), Foreground = Brushes.White };
  dialog.SourceInitialized += (_, _) => ApplyDarkTitleBar(dialog);
  var panel = new DockPanel { Margin = new Thickness(22) };
  var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
  var cancel = new Button { Content = "Zrušit", IsCancel = true, Margin = new Thickness(0, 0, 10, 0) };
  var save = new Button { Content = "Uložit", IsDefault = false }; save.Click += (_, _) => dialog.DialogResult = true;
  buttons.Children.Add(cancel); buttons.Children.Add(save); DockPanel.SetDock(buttons, Dock.Bottom); panel.Children.Add(buttons);
  TextBox Editor(string text, double fontSize) => new()
  {
   Text = text, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, FontSize = fontSize,
   Foreground = Brushes.White, Background = Color("#242636"), BorderBrush = Color("#454759"),
   Padding = new Thickness(12), VerticalScrollBarVisibility = ScrollBarVisibility.Auto
  };
  var editor = Editor(cue.Text, note is null ? 24 : 18);
  TextBox? noteEditor = null;
  if (note is null) panel.Children.Add(editor);
  else
  {
   void AddCaption(string text, Thickness margin)
   {
    var caption = new TextBlock { Text = text, FontSize = 15, Margin = margin };
    DockPanel.SetDock(caption, Dock.Top);
    panel.Children.Add(caption);
   }
   AddCaption("Název dlaždice", new Thickness(0, 0, 0, 6));
   editor.Height = 62;
   DockPanel.SetDock(editor, Dock.Top);
   panel.Children.Add(editor);
   AddCaption("Poznámka", new Thickness(0, 14, 0, 6));
   noteEditor = Editor(cue.Note ?? "", 16);
   panel.Children.Add(noteEditor);
  }
  dialog.Content = panel;
  dialog.Loaded += (_, _) => { editor.Focus(); editor.SelectAll(); };
  if (dialog.ShowDialog() != true) return;
  cue.Text = editor.Text;
  label.Text = cue.Text;
  if (note is not null && noteEditor is not null)
  {
   cue.Note = string.IsNullOrWhiteSpace(noteEditor.Text) ? null : noteEditor.Text;
   note.Text = CueNoteText(cue);
  }
  if (active == cue) NowPlaying.Text = cue.Text;
  MarkDirty();
 }
 internal static double EffectiveVolume(double master, double track, bool fade, double position, double duration, int fadeSeconds = 3)
 {
  double envelope = fade && duration > 0 ? Math.Clamp((duration - position) / Math.Min(fadeSeconds, duration), 0, 1) : 1;
  return Math.Clamp(master, 0, 1) * Math.Clamp(track, 0, 1) * envelope;
 }
 void ApplyVolume()
 {
  double duration = player.NaturalDuration.HasTimeSpan ? player.NaturalDuration.TimeSpan.TotalSeconds : 0;
  player.Volume = EffectiveVolume(project.MasterVolume, active?.Volume ?? 1, active?.FadeOut ?? false, player.Position.TotalSeconds, duration, active?.FadeSeconds ?? 3);
 }
 void MasterVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
 {
  if (MasterPercent is null || syncingVolume) return;
  project.MasterVolume = e.NewValue;
  if (e.NewValue > 0) lastAudibleVolume = e.NewValue;
  UpdateMasterLabel(); MarkDirty(); ApplyVolume();
 }
 void UpdateMasterLabel()
 {
  bool muted = project.MasterVolume == 0;
  MasterPercent.Text = $"{project.MasterVolume * 100:0} %";
  VolumeWaves.Visibility = muted ? Visibility.Collapsed : Visibility.Visible;
  VolumeCross.Visibility = muted ? Visibility.Visible : Visibility.Collapsed;
  MasterMute.ToolTip = muted ? "Obnovit hlasitost" : "Ztlumit zvuk";
 }
 void SyncMasterVolume() { syncingVolume = true; MasterVolume.Value = project.MasterVolume; syncingVolume = false; UpdateMasterLabel(); ApplyVolume(); }
 void MuteClick(object sender, RoutedEventArgs e) => MasterVolume.Value = project.MasterVolume > 0 ? 0 : lastAudibleVolume;

 void Import(Cue cue)
 {
  var dialog = new OpenFileDialog { Filter = "Zvukové soubory|*.mp3;*.wav;*.wma;*.aif;*.aiff;*.m4a;*.aac;*.flac|MP3|*.mp3|WAV|*.wav|WMA|*.wma|AIFF|*.aif;*.aiff|M4A / AAC|*.m4a;*.aac|FLAC|*.flac", Title = "Vyberte zvuk" };
  if (dialog.ShowDialog(this) != true) return;
  try
  {
   string dest = Path.Combine(sessionFolder, Guid.NewGuid().ToString("N") + AudioExtension(dialog.FileName));
   File.Copy(dialog.FileName, dest);
   if (active == cue) ClearPlayer();
   cue.Source = dest;
   if (cue.Text == "Nový zvuk") cue.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
   MarkDirty(); RenderTiles();
  }
  catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException) { Error(e); }
 }
 void StartCue(Cue cue)
 {
  if (cue.Source is null) { Import(cue); return; }
  player.Close(); pendingPlay = true; active = cue; cue.Used = true;
  ApplyVolume();
  Transport.Visibility = Visibility.Visible; NowPlaying.Text = cue.Text;
  updatingSeek = true; Seek.Value = 0; updatingSeek = false;
  SetPlaying(false); player.Open(new Uri(cue.Source)); MarkDirty(); RenderTiles();
 }
 void SetPlaying(bool value) { playing = value; PlayButton.Content = value ? "Ⅱ" : "▶"; }
 void UpdatePosition()
 {
  if (active is null || Seek.IsMouseCaptureWithin) return;
  updatingSeek = true; Seek.Value = Math.Min(Seek.Maximum, player.Position.TotalSeconds); updatingSeek = false;
  PositionText.Text = FormatTime(player.Position.TotalSeconds);
 }
 void SeekChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
 {
  if (updatingSeek || active is null) return;
  player.Position = TimeSpan.FromSeconds(e.NewValue); PositionText.Text = FormatTime(e.NewValue); ApplyVolume();
 }
 void Skip(double seconds) { if (active is not null) { player.Position = TimeSpan.FromSeconds(Math.Clamp(player.Position.TotalSeconds + seconds, 0, Seek.Maximum)); UpdatePosition(); } }
 void BackClick(object sender, RoutedEventArgs e) => Skip(-5);
 void ForwardClick(object sender, RoutedEventArgs e) => Skip(5);
 void PlayClick(object sender, RoutedEventArgs e)
 {
  if (active is null) return;
  if (playing) { player.Pause(); SetPlaying(false); }
  else { if (player.Position.TotalSeconds >= Seek.Maximum - .1) player.Position = TimeSpan.Zero; player.Play(); SetPlaying(true); }
 }
 void Stop() { pendingPlay = false; player.Stop(); SetPlaying(false); UpdatePosition(); }
 void StopClick(object sender, RoutedEventArgs e) => Stop();
 void ClearPlayer() { pendingPlay = false; player.Close(); active = null; SetPlaying(false); Transport.Visibility = Visibility.Collapsed; }
 void FitStage() => LayoutSections();
 void HostSizeChanged(object sender, SizeChangedEventArgs e) => LayoutSections();
 void SaveClick(object sender, RoutedEventArgs e) => SaveProject();
 bool SaveProject()
 {
  string? destination = projectPath;
  if (destination is null)
  {
   var dialog = new SaveFileDialog { Filter = "Divadelní projekt (*.divz)|*.divz", DefaultExt = ".divz", FileName = "Představení.divz" };
   if (dialog.ShowDialog(this) != true) return false;
   destination = dialog.FileName;
  }
  string temp = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
  try
  {
   using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create))
   {
    foreach (var cue in AllCues())
    {
     cue.AudioEntry = cue.Source is null ? null : "audio/" + cue.Id + AudioExtension(cue.Source);
     if (cue.Source is not null) zip.CreateEntryFromFile(cue.Source, cue.AudioEntry!, CompressionLevel.NoCompression);
    }
    using var writer = new StreamWriter(zip.CreateEntry("project.json").Open());
    writer.Write(JsonSerializer.Serialize(project, new JsonSerializerOptions { WriteIndented = true }));
   }
   File.Move(temp, destination, true); projectPath = destination; dirty = false; UpdateTitle(); return true;
  }
  catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException) { Error(e); return false; }
  finally { if (File.Exists(temp)) try { File.Delete(temp); } catch (IOException) { } }
 }
 void OpenClick(object sender, RoutedEventArgs e)
 {
  var dialog = new OpenFileDialog { Filter = "Divadelní projekt (*.divz)|*.divz" };
  if (dialog.ShowDialog(this) == true) OpenProject(dialog.FileName);
 }
 public void OpenProject(string path)
 {
  if (!ConfirmDiscard()) return;
  try
  {
   using var zip = ZipFile.OpenRead(path);
   var manifest = zip.GetEntry("project.json") ?? throw new InvalidDataException("V souboru chybí projekt.");
   using var reader = new StreamReader(manifest.Open());
   var loaded = JsonSerializer.Deserialize<ShowProject>(reader.ReadToEnd()) ?? throw new InvalidDataException("Projekt je prázdný.");
   NormalizeSections(loaded);
   if (!double.IsFinite(loaded.MasterVolume)) throw new InvalidDataException("Neplatná hlavní hlasitost.");
   loaded.MasterVolume = Math.Clamp(loaded.MasterVolume, 0, 1);
   var ids = new HashSet<string>();
   foreach (var cue in loaded.Sections.SelectMany(s => s.Cues))
   {
    if (cue is null || !Guid.TryParseExact(cue.Id, "N", out _) || !ids.Add(cue.Id)) throw new InvalidDataException("Neplatné dlaždice.");
    cue.Text ??= "";
    if (!double.IsFinite(cue.Volume)) throw new InvalidDataException("Neplatná hlasitost zvuku.");
    cue.Volume = Math.Clamp(cue.Volume, 0, 1);
    if (cue.FadeSeconds is not (3 or 5 or 7)) cue.FadeSeconds = 3;
    if (cue.AudioEntry is null) continue;
    var entry = zip.GetEntry(cue.AudioEntry) ?? throw new InvalidDataException("Chybí zvuk: " + cue.Text);
    if (entry.Length > 1024L * 1024 * 1024) throw new InvalidDataException("Zvuk je příliš velký.");
    string extracted = Path.Combine(sessionFolder, Guid.NewGuid().ToString("N") + AudioExtension(cue.AudioEntry));
    entry.ExtractToFile(extracted); cue.Source = extracted;
   }
   CancelSectionResize(); ClearPlayer(); undoHistory.Clear(); project = loaded; SyncLayoutLock();
   projectPath = path; dirty = false; SyncMasterVolume(); FitStage(); RenderTiles(); UpdateTitle();
  }
  catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or ArgumentException) { Error(e); }
 }
 bool ConfirmDiscard()
 {
  if (!dirty) return true;
  var result = MessageBox.Show(this, "Uložit změny v projektu?", "Neuložené změny", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
  return result == MessageBoxResult.No || (result == MessageBoxResult.Yes && SaveProject());
 }
 void NewClick(object sender, RoutedEventArgs e)
 {
  if (!ConfirmDiscard()) return;
  CancelSectionResize(); ClearPlayer(); undoHistory.Clear(); project = new(); project.Sections.Add(new ShowSection()); SyncLayoutLock(); projectPath = null; dirty = false; SyncMasterVolume(); FitStage(); RenderTiles(); UpdateTitle();
 }
 void Error(Exception e) => MessageBox.Show(this, e.Message, "Operaci se nepodařilo dokončit", MessageBoxButton.OK, MessageBoxImage.Error);

 void SavePreview(string path)
 {
  var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
  bitmap.Render(this);
  var png = new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
  using var stream = File.Create(path); png.Save(stream);
 }
}
