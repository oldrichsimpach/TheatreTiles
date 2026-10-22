using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace DivadelniZvuky;

public sealed class ShowSection
{
 public string Id { get; set; } = Guid.NewGuid().ToString("N");
 public string Name { get; set; } = "Sekce";
 public double X { get; set; }
 public double Y { get; set; }
 public double Width { get; set; } = 1;
 public double Height { get; set; } = 1;
 public List<Cue> Cues { get; set; } = [];
}

public sealed partial class SectionSurface : Border
{
 readonly MainWindow owner;
 public ShowSection Model { get; }
 internal readonly ScrollViewer Scroll;
 internal void Settle()
 {
  var wave = new DoubleAnimationUsingKeyFrames(); wave.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero))); wave.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(130)), new CubicEase { EasingMode = EasingMode.EaseOut })); wave.KeyFrames.Add(new LinearDoubleKeyFrame(.23, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220)))); wave.KeyFrames.Add(new EasingDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(360)), new CubicEase { EasingMode = EasingMode.EaseOut })); BeginAnimation(PressureProperty, wave);
  var transform = new ScaleTransform(1, 1); RenderTransformOrigin = new Point(.5, .5); RenderTransform = transform;
  var frames = new DoubleAnimationUsingKeyFrames();
  frames.KeyFrames.Add(new EasingDoubleKeyFrame(.965, KeyTime.FromTimeSpan(TimeSpan.Zero)));
  frames.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(135)), new CubicEase { EasingMode = EasingMode.EaseOut }));
  frames.KeyFrames.Add(new EasingDoubleKeyFrame(.989, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220))));
  frames.KeyFrames.Add(new EasingDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(340)), new CubicEase { EasingMode = EasingMode.EaseOut }));
  transform.BeginAnimation(ScaleTransform.ScaleXProperty, frames); transform.BeginAnimation(ScaleTransform.ScaleYProperty, frames);
 }
 readonly Canvas tiles = new();
 readonly Canvas overlay = new() { IsHitTestVisible = false, ClipToBounds = true };
 readonly Border ghost = new() { BorderBrush = Brushes.White, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(20), Visibility = Visibility.Collapsed };
 readonly TextBlock name;
 readonly System.Windows.Shapes.Path wall = new() { Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(48, 255, 255, 255)), StrokeThickness = 1, IsHitTestVisible = false };
 public static readonly DependencyProperty PressureProperty = DependencyProperty.Register(nameof(Pressure), typeof(double), typeof(SectionSurface), new PropertyMetadata(0.0, (d, _) => ((SectionSurface)d).UpdateWall()));
 public double Pressure { get => (double)GetValue(PressureProperty); set => SetValue(PressureProperty, value); }
 internal void Press() => BeginAnimation(PressureProperty, new DoubleAnimation(Pressure, 1, TimeSpan.FromMilliseconds(100)));
 void UpdateWall()
 {
  if (contactClip is not null) return;
  double w = ActualWidth, h = ActualHeight; if (w < 2 || h < 2) return;
  double r = Math.Min(20, Math.Min(w, h) / 4), d = Math.Max(0, Pressure) * Math.Min(12, Math.Min(w, h) * .06);
  var shape = new StreamGeometry();
  using (var c = shape.Open())
  {
   c.BeginFigure(new Point(r, 0), true, true);
   c.BezierTo(new Point(w * .35, d), new Point(w * .65, d), new Point(w - r, 0), true, false); c.QuadraticBezierTo(new Point(w, 0), new Point(w, r), true, false);
   c.BezierTo(new Point(w - d, h * .35), new Point(w - d, h * .65), new Point(w, h - r), true, false); c.QuadraticBezierTo(new Point(w, h), new Point(w - r, h), true, false);
   c.BezierTo(new Point(w * .65, h - d), new Point(w * .35, h - d), new Point(r, h), true, false); c.QuadraticBezierTo(new Point(0, h), new Point(0, h - r), true, false);
   c.BezierTo(new Point(d, h * .65), new Point(d, h * .35), new Point(0, r), true, false); c.QuadraticBezierTo(new Point(0, 0), new Point(r, 0), true, false);
  }
  Clip = shape; wall.Data = shape;
 }
 readonly List<Rect> slots = [];
 readonly Dictionary<string, Border> views = [];
 Cue? dragging;
 int target;
 double tileSize = 220;
 bool rebuilding;
 public bool IsDragging => dragging is not null;
 public SectionSurface(MainWindow owner, ShowSection model)
 {
  this.owner = owner; Model = model;
  CornerRadius = new CornerRadius(20); BorderThickness = new Thickness(0);
  Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(10, 255, 255, 255));
  var grid = new Grid(); Child = grid;
  grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) }); grid.RowDefinitions.Add(new RowDefinition());
  name = new TextBlock { Text = model.Name, Foreground = Brushes.Gray, Opacity = .3, FontSize = 17, Margin = new Thickness(16, 8, 50, 0), TextTrimming = TextTrimming.CharacterEllipsis, IsHitTestVisible = false };
  grid.Children.Add(name);
  var mover = new Thumb { Width = 52, Height = 28, HorizontalAlignment = HorizontalAlignment.Center, Cursor = Cursors.SizeAll, ToolTip = "Přesunout sekci" };
  var moverTemplate = new FrameworkElementFactory(typeof(Canvas)); moverTemplate.SetValue(FrameworkElement.WidthProperty, 24.0); moverTemplate.SetValue(FrameworkElement.HeightProperty, 14.0); moverTemplate.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center); moverTemplate.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center); moverTemplate.SetValue(UIElement.OpacityProperty, .3); moverTemplate.SetValue(Panel.BackgroundProperty, Brushes.Transparent);
  for (int row = 0; row < 2; row++) for (int col = 0; col < 3; col++) { var dot = new FrameworkElementFactory(typeof(Ellipse)); dot.SetValue(FrameworkElement.WidthProperty, 4.0); dot.SetValue(FrameworkElement.HeightProperty, 4.0); dot.SetValue(Shape.FillProperty, Brushes.Gray); dot.SetValue(Canvas.LeftProperty, col * 9.0); dot.SetValue(Canvas.TopProperty, row * 9.0); moverTemplate.AppendChild(dot); }
  mover.Template = new ControlTemplate(typeof(Thumb)) { VisualTree = moverTemplate }; grid.Children.Add(mover);
  Point movePointer = default;
  mover.DragStarted += (_, _) => { movePointer = owner.WorkspacePointer(); if (!owner.IsLayoutLocked) owner.RememberLayout(); };
  mover.DragDelta += (_, _) => { Point current = owner.WorkspacePointer(); Vector delta = current - movePointer; movePointer = current; owner.TransformSection(model, delta.X, delta.Y, false); };
  Scroll = new ScrollViewer { Content = tiles, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(14, 8, 14, 12) };
  Grid.SetRow(Scroll, 1); grid.Children.Add(Scroll);
  Scroll.PreviewMouseWheel += (_, e) => { Scroll.ScrollToHorizontalOffset(Scroll.HorizontalOffset - e.Delta); e.Handled = true; };
  overlay.Children.Add(ghost); Grid.SetRowSpan(overlay, 2); grid.Children.Add(overlay);
  Grid.SetRowSpan(wall, 2); grid.Children.Add(wall);
  foreach (var corner in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
  {
   var thumb = new Thumb { Width = 18, Height = 18, HorizontalAlignment = corner.Item1 < 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right, VerticalAlignment = corner.Item2 < 0 ? VerticalAlignment.Top : VerticalAlignment.Bottom, Cursor = corner.Item1 == corner.Item2 ? Cursors.SizeNWSE : Cursors.SizeNESW };
   var marker = new FrameworkElementFactory(typeof(TextBlock)); marker.SetValue(TextBlock.TextProperty, corner == (-1, -1) ? "◤" : corner == (1, -1) ? "◥" : corner == (-1, 1) ? "◣" : "◢"); marker.SetValue(TextBlock.ForegroundProperty, Brushes.Gray); marker.SetValue(UIElement.OpacityProperty, .5);
   thumb.Template = new ControlTemplate(typeof(Thumb)) { VisualTree = marker };
   Grid.SetRowSpan(thumb, 2); grid.Children.Add(thumb);
   Point resizePointer = default;
   thumb.DragStarted += (_, _) => { resizePointer = owner.WorkspacePointer(); if (!owner.IsLayoutLocked) owner.BeginSectionResize(model); };
   thumb.DragDelta += (_, _) => { Point current = owner.WorkspacePointer(); Vector delta = current - resizePointer; resizePointer = current; owner.TransformSection(model, delta.X, delta.Y, true, corner.Item1, corner.Item2); };
   thumb.DragCompleted += (_, _) => owner.SettleSections();
  }
  ContextMenuOpening += (_, e) => { if (e.OriginalSource is FrameworkElement element && element != this && FindTileMenu(element)) return; ContextMenu = owner.SectionMenu(model); };
  ContextMenu = owner.SectionMenu(model);
  AllowDrop = true; DragOver += OnDragOver;
  Drop += (_, e) => { if (dragging is null || e.Data.GetData("TheatreCue") as string != dragging.Id) return; EndDrag(true); e.Handled = true; };
  DragLeave += (_, e) => { if (!new Rect(RenderSize).Contains(e.GetPosition(this))) ghost.Visibility = Visibility.Collapsed; };
  SizeChanged += (_, _) => { UpdateWall(); if (contactClip is null) Rebuild(); };
 }
 static bool FindTileMenu(FrameworkElement element)
 {
  for (DependencyObject? current = element; current is not null; current = VisualTreeHelper.GetParent(current))
   if (current is Border b && b.Tag is Cue) return true;
  return false;
 }
 public void Rebuild()
 {
  if (rebuilding || ActualHeight < 40 || ActualWidth < 1) return;
  rebuilding = true;
  try
  {
   name.Text = Model.Name;
   double usableHeight = Math.Max(80, ActualHeight - 76);
   tileSize = Math.Min(220, usableHeight);
   const int rows = 1;
   var order = Preview();
   int cols = order.Count + 1;
   double totalWidth = cols * (tileSize + 14) - 14;
   double viewport = Math.Max(0, ActualWidth - 30);
   double left = Math.Max(0, (viewport - totalWidth) / 2);
   double top = Math.Max(0, (usableHeight - tileSize) / 2);
   var previous = views.ToDictionary(p => p.Key, p => p.Value.TranslatePoint(new Point(), tiles));
   views.Clear(); slots.Clear(); tiles.Children.Clear();
   tiles.Width = Math.Max(viewport, left + totalWidth); tiles.Height = usableHeight;
   // Fill rows left-to-right; extra columns extend the section horizontally.
   for (int i = 0; i < order.Count; i++)
   {
    int col = i % cols, row = i / cols;
    double x = left + col * (tileSize + 14), y = top + row * (tileSize + 14);
    slots.Add(new Rect(x, y, tileSize, tileSize));
    var cue = order[i];
    Border view = cue == dragging ? new Border { Width = tileSize, Height = tileSize, BorderBrush = Brushes.White, BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(20), Child = new TextBlock { Text = "Sem přesunout", Foreground = Brushes.White, Opacity = .6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } } : owner.CreateTile(cue, this, tileSize);
    view.Tag = cue; views[cue.Id] = view; Canvas.SetLeft(view, x); Canvas.SetTop(view, y); tiles.Children.Add(view);
    if (dragging is not null && cue != dragging && previous.TryGetValue(cue.Id, out var old))
    {
     var transform = new TranslateTransform(); view.RenderTransform = transform;
     var time = TimeSpan.FromMilliseconds(150);
     var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
     transform.BeginAnimation(TranslateTransform.XProperty, new System.Windows.Media.Animation.DoubleAnimation(old.X - x, 0, time) { EasingFunction = ease });
     transform.BeginAnimation(TranslateTransform.YProperty, new System.Windows.Media.Animation.DoubleAnimation(old.Y - y, 0, time) { EasingFunction = ease });
    }
   }
   var plus = new System.Windows.Shapes.Path { Data = Geometry.Parse("M42,2 L58,2 58,42 98,42 98,58 58,58 58,98 42,98 42,58 2,58 2,42 42,42 Z"), Stretch = Stretch.Uniform, Width = tileSize * .84, Height = tileSize * .84, Fill = new LinearGradientBrush(System.Windows.Media.Color.FromRgb(36, 38, 46), System.Windows.Media.Color.FromRgb(93, 95, 106), 45) };
   var add = new Button { Width = tileSize, Height = tileSize, Content = plus, Padding = new Thickness(0), BorderThickness = new Thickness(0), Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(24, 26, 37)), IsEnabled = !owner.IsLayoutLocked, ToolTip = "Přidat dlaždici" };
   add.Click += (_, _) => owner.AddCue(Model); Canvas.SetLeft(add, order.Count == 0 ? Math.Max(0, (viewport - tileSize) / 2) : left + order.Count * (tileSize + 14)); Canvas.SetTop(add, top + Math.Max(0, Math.Min(rows, order.Count) - 1) * (tileSize + 14)); tiles.Children.Add(add);
  }
  finally { rebuilding = false; }
 }
 List<Cue> Preview()
 {
  var list = Model.Cues.ToList(); if (dragging is not null) { list.Remove(dragging); list.Insert(Math.Clamp(target, 0, list.Count), dragging); } return list;
 }
 public void BeginDrag(Cue cue) { if (owner.IsLayoutLocked || dragging is not null) return; dragging = cue; target = Model.Cues.IndexOf(cue); Rebuild(); }
 internal void PreviewAt(int index) { if (dragging is null || owner.IsLayoutLocked) return; target = Math.Clamp(index, 0, Model.Cues.Count - 1); Rebuild(); }
 void OnDragOver(object sender, DragEventArgs e)
 {
  if (owner.IsLayoutLocked || dragging is null || e.Data.GetData("TheatreCue") as string != dragging.Id) { e.Effects = DragDropEffects.None; return; }
  e.Effects = DragDropEffects.Move; e.Handled = true;
  Point p = e.GetPosition(this); ghost.Width = tileSize; ghost.Height = tileSize; ghost.Visibility = Visibility.Visible; Canvas.SetLeft(ghost, p.X - tileSize / 2); Canvas.SetTop(ghost, p.Y - 20);
  Point sp = e.GetPosition(Scroll); if (sp.X < 35) Scroll.ScrollToHorizontalOffset(Scroll.HorizontalOffset - 12); else if (sp.X > Scroll.ActualWidth - 35) Scroll.ScrollToHorizontalOffset(Scroll.HorizontalOffset + 12);
  Point q = e.GetPosition(tiles); double best = double.MaxValue; int next = target;
  for (int i = 0; i < slots.Count; i++) { Rect r = slots[i]; double dx = Math.Max(r.Left - q.X, Math.Max(0, q.X - r.Right)), dy = Math.Max(r.Top - q.Y, Math.Max(0, q.Y - r.Bottom)); double d = dx * dx + dy * dy; if (d < best) { best = d; next = i; } }
  if (target != next) PreviewAt(next);
 }
 public void EndDrag(bool commit)
 {
  if (dragging is null) return;
  if (commit && !owner.IsLayoutLocked && Model.Cues.IndexOf(dragging) != target) { owner.RememberLayout(); Model.Cues.Remove(dragging); Model.Cues.Insert(target, dragging); owner.Changed(); }
  dragging = null; ghost.Visibility = Visibility.Collapsed; Rebuild();
 }
}

public partial class MainWindow
{
 readonly Dictionary<string, SectionSurface> sectionViews = [];
 readonly Stack<string> undoHistory = new();
 internal bool IsLayoutLocked => project.LayoutLocked;
 internal Point WorkspacePointer() => Mouse.GetPosition(SectionCanvas);
 internal void Changed() => MarkDirty();
 internal void RememberLayout()
 {
  CancelSectionResize();
  // Sources live in the session folder and remain available for undo.
  var sources = AllCues().ToDictionary(c => c.Id, c => c.Source);
  undoHistory.Push(JsonSerializer.Serialize(new LayoutSnapshot(project.Sections, sources, project.LayoutTree)));
 }
 sealed record LayoutSnapshot(List<ShowSection> Sections, Dictionary<string, string?> Sources, SectionSplit? Tree);
 IEnumerable<Cue> AllCues() => project.Sections.SelectMany(s => s.Cues);
 void UndoLayout()
 {
  if (project.LayoutLocked || undoHistory.Count == 0) return;
  CancelSectionResize();
  var state = JsonSerializer.Deserialize<LayoutSnapshot>(undoHistory.Pop())!;
  var current = AllCues().ToDictionary(c => c.Id);
  foreach (var cue in state.Sections.SelectMany(s => s.Cues))
  {
   cue.Source = state.Sources.GetValueOrDefault(cue.Id);
   // Undo layout does not rewind playback markers or track settings.
   if (current.TryGetValue(cue.Id, out var existing)) { cue.Text = existing.Text; cue.Source = existing.Source; cue.AudioEntry = existing.AudioEntry; cue.Used = existing.Used; cue.Volume = existing.Volume; cue.FadeOut = existing.FadeOut; cue.FadeSeconds = existing.FadeSeconds; }
  }
  project.Sections = state.Sections;
  project.LayoutTree = state.Tree;
  if (active is not null)
  {
   var restored = AllCues().FirstOrDefault(c => c.Id == active.Id);
   if (restored is null) ClearPlayer(); else active = restored;
  }
  MarkDirty(); RenderTiles();
 }
 void InitializeSections()
 {
  project.Sections.Add(new ShowSection());
  var menu = new ContextMenu();
  var add = new MenuItem { Header = "Přidat sekci" };
  add.Click += (_, _) => AddSection(null); menu.Items.Add(add);
  menu.Opened += (_, _) => add.IsEnabled = !project.LayoutLocked;
  SectionCanvas.ContextMenu = menu;
  ContextMenu = menu;
 }
 static void NormalizeSections(ShowProject model)
 {
  if (model.Version is not (1 or 2)) throw new InvalidDataException("Nepodporovaná verze projektu.");
  if (model.Version == 1)
  {
   if (model.Cues is null) throw new InvalidDataException("Neplatné dlaždice.");
   model.Sections = [new ShowSection { Cues = model.Cues }];
   if (!model.StageAutoSize) { model.Sections[0].Width = .8; model.Sections[0].Height = .8; model.Sections[0].X = .1; model.Sections[0].Y = .1; }
  }
  if (model.Sections is null || model.Sections.Count > 64) throw new InvalidDataException("Neplatné sekce.");
  var ids = new HashSet<string>();
  foreach (var section in model.Sections)
  {
   if (section is null || !Guid.TryParseExact(section.Id, "N", out _) || !ids.Add(section.Id) || section.Cues is null || !double.IsFinite(section.X) || !double.IsFinite(section.Y) || !double.IsFinite(section.Width) || !double.IsFinite(section.Height)) throw new InvalidDataException("Neplatná sekce.");
   section.Name ??= "Sekce";
   section.Width = Math.Clamp(section.Width, .05, 1); section.Height = Math.Clamp(section.Height, .05, 1);
   section.X = Math.Clamp(section.X, 0, 1 - section.Width); section.Y = Math.Clamp(section.Y, 0, 1 - section.Height);
  }
  if (model.Sections.Sum(s => s.Cues.Count) > 2000) throw new InvalidDataException("Příliš mnoho dlaždic.");
  model.Version = 2; model.Cues = [];
 }
 internal static FrameworkElement MakeGrip(Brush color)
 {
  var dots = new Canvas { Width = 24, Height = 14, Background = Brushes.Transparent, Opacity = .3 };
  for (int row = 0; row < 2; row++) for (int col = 0; col < 3; col++)
  {
   var dot = new Ellipse { Width = 4, Height = 4, Fill = color };
   Canvas.SetLeft(dot, col * 9); Canvas.SetTop(dot, row * 9); dots.Children.Add(dot);
  }
  return dots;
 }
 void RenderTiles()
 {
  if (SectionCanvas is null) return;
  var live = project.Sections.Select(s => s.Id).ToHashSet();
  foreach (var id in sectionViews.Keys.Where(id => !live.Contains(id) || sectionViews[id].Model != project.Sections.First(s => s.Id == id)).ToList())
  { SectionCanvas.Children.Remove(sectionViews[id]); sectionViews.Remove(id); }
  foreach (var section in project.Sections)
  {
   if (!sectionViews.TryGetValue(section.Id, out var surface)) { surface = new SectionSurface(this, section); sectionViews[section.Id] = surface; SectionCanvas.Children.Add(surface); }
  }
  LayoutSections();
  foreach (var surface in sectionViews.Values) surface.Rebuild();
 }
 void LayoutSections(bool animate = false)
 {
  if (SectionCanvas is null || StageHost.ActualWidth <= 0) return;
  foreach (var section in project.Sections)
  {
   if (!sectionViews.TryGetValue(section.Id, out var view)) continue;
   void Set(DependencyProperty property, double value)
   {
    double previous = (double)view.GetValue(property); view.BeginAnimation(property, null); view.SetValue(property, value);
    if (animate && double.IsFinite(previous)) view.BeginAnimation(property, new DoubleAnimation(previous, value, TimeSpan.FromMilliseconds(110)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
   }
   Set(Canvas.LeftProperty, section.X * StageHost.ActualWidth); Set(Canvas.TopProperty, section.Y * StageHost.ActualHeight);
   Set(FrameworkElement.WidthProperty, Math.Max(1, section.Width * StageHost.ActualWidth - (section.Width < 1 ? 6 : 0)));
   Set(FrameworkElement.HeightProperty, Math.Max(1, section.Height * StageHost.ActualHeight - (section.Height < 1 ? 6 : 0)));
  }
 }
 static Rect Bounds(ShowSection section) => new(section.X, section.Y, section.Width, section.Height);
 internal void TransformSection(ShowSection section, double dx, double dy, bool resize, int cornerX = 1, int cornerY = 1)
 {
  if (project.LayoutLocked || StageHost.ActualWidth < 1 || StageHost.ActualHeight < 1) return;
  double x = section.X, y = section.Y, w = section.Width, h = section.Height;
  if (resize && TryCellContact(section, dx, dy, cornerX, cornerY)) return;
  dx /= StageHost.ActualWidth; dy /= StageHost.ActualHeight;
  if (resize && project.LayoutTree is not null && project.Sections.Count > 1)
  {
   PushBoundaries(project.LayoutTree, new Rect(0, 0, 1, 1), section, dx, dy, cornerX, cornerY);
   ApplyTree(project.LayoutTree, new Rect(0, 0, 1, 1)); LayoutSections(true); MarkDirty(); return;
  }
  if (!resize) { x = Math.Clamp(x + dx, 0, 1 - w); y = Math.Clamp(y + dy, 0, 1 - h); }
  else
  {
   double minW = Math.Min(180 / StageHost.ActualWidth, w), minH = Math.Min(130 / StageHost.ActualHeight, h);
   if (cornerX > 0) w = Math.Clamp(w + dx, minW, 1 - x);
   else { double next = Math.Clamp(x + dx, 0, x + w - minW); w += x - next; x = next; }
   if (cornerY > 0) h = Math.Clamp(h + dy, minH, 1 - y);
   else { double next = Math.Clamp(y + dy, 0, y + h - minH); h += y - next; y = next; }
  }
  var candidate = new Rect(x + .001, y + .001, Math.Max(.001, w - .002), Math.Max(.001, h - .002));
  if (project.Sections.Any(other => other != section && candidate.IntersectsWith(Bounds(other)))) return;
  section.X = x; section.Y = y; section.Width = w; section.Height = h; LayoutSections(); MarkDirty();
  project.LayoutTree = null;
 }
 internal void AddCue(ShowSection section)
 { if (project.LayoutLocked) return; RememberLayout(); section.Cues.Add(new Cue()); MarkDirty(); RenderTiles(); }
 void DeleteCue(Cue cue)
 {
  if (project.LayoutLocked) return;
  RememberLayout(); if (active == cue) ClearPlayer();
  foreach (var section in project.Sections) section.Cues.Remove(cue);
  MarkDirty(); RenderTiles();
 }
 internal void AddSection(ShowSection? source)
 {
  if (project.LayoutLocked) return;
  RememberLayout();
  var added = new ShowSection { Name = source is null ? $"Sekce {project.Sections.Count + 1}" : source.Name + " – kopie", Width = source?.Width ?? .45, Height = source?.Height ?? .45 };
  if (source is not null) added.Cues = source.Cues.Select(c => new Cue { Text = c.Text, Used = c.Used, Volume = c.Volume, FadeOut = c.FadeOut, FadeSeconds = c.FadeSeconds, Source = c.Source }).ToList();
  bool fits = false;
  foreach (double y in new[] { 0.0 }.Concat(project.Sections.Select(s => s.Y + s.Height)).Distinct())
   foreach (double x in new[] { 0.0 }.Concat(project.Sections.Select(s => s.X + s.Width)).Distinct())
   {
    var rect = new Rect(x, y, added.Width, added.Height);
    if (!fits && rect.Right <= 1.000001 && rect.Bottom <= 1.000001 && !project.Sections.Any(s => rect.IntersectsWith(new Rect(s.X + .001, s.Y + .001, s.Width - .002, s.Height - .002)))) { added.X = x; added.Y = y; fits = true; }
   }
  project.Sections.Add(added); if (!fits) ArrangeSections(); else project.LayoutTree = null;
  MarkDirty(); RenderTiles();
 }
 void ArrangeSections()
 {
  int n = project.Sections.Count;
  for (int i = 0; i < n; i++)
  {
   Rect r;
   if (n == 1) r = new Rect(0, 0, 1, 1);
   else if (n == 2) r = new Rect(0, i * .5, 1, .5);
   else if (n == 3) r = i == 2 ? new Rect(.5, 0, .5, 1) : new Rect(0, i * .5, .5, .5);
   else { int cols = (int)Math.Ceiling(Math.Sqrt(n)), rows = (int)Math.Ceiling(n / (double)cols), row = i / cols, rowCount = Math.Min(cols, n - row * cols); r = new Rect((i % cols) / (double)rowCount, row / (double)rows, 1.0 / rowCount, 1.0 / rows); }
   var s = project.Sections[i]; s.X = r.X; s.Y = r.Y; s.Width = r.Width; s.Height = r.Height;
  }
  project.LayoutTree = BuildTree(project.Sections, new Rect(0, 0, 1, 1));
 }
 internal void SettleSections() { FinishSectionResize(); }
 static SectionSplit? BuildTree(List<ShowSection> sections, Rect area)
 {
  if (sections.Count == 0) return null;
  if (sections.Count == 1) return new SectionSplit { SectionId = sections[0].Id };
  foreach (bool vertical in new[] { true, false })
   foreach (double split in sections.Select(s => vertical ? s.X + s.Width : s.Y + s.Height).Distinct())
   {
    double start = vertical ? area.X : area.Y, length = vertical ? area.Width : area.Height;
    if (split <= start + .001 || split >= start + length - .001) continue;
    var first = sections.Where(s => (vertical ? s.X + s.Width : s.Y + s.Height) <= split + .001).ToList();
    var second = sections.Where(s => (vertical ? s.X : s.Y) >= split - .001).ToList();
    if (first.Count == 0 || second.Count == 0 || first.Count + second.Count != sections.Count) continue;
    Rect a = vertical ? new Rect(area.X, area.Y, split - area.X, area.Height) : new Rect(area.X, area.Y, area.Width, split - area.Y);
    Rect b = vertical ? new Rect(split, area.Y, area.Right - split, area.Height) : new Rect(area.X, split, area.Width, area.Bottom - split);
    return new SectionSplit { Vertical = vertical, Ratio = (split - start) / length, First = BuildTree(first, a), Second = BuildTree(second, b) };
   }
  return null;
 }
 void ApplyTree(SectionSplit tree, Rect area)
 {
  if (tree.SectionId is not null) { var section = project.Sections.FirstOrDefault(s => s.Id == tree.SectionId); if (section is not null) { section.X = area.X; section.Y = area.Y; section.Width = area.Width; section.Height = area.Height; } return; }
  if (tree.First is null || tree.Second is null) return;
  Rect a = tree.Vertical ? new Rect(area.X, area.Y, area.Width * tree.Ratio, area.Height) : new Rect(area.X, area.Y, area.Width, area.Height * tree.Ratio);
  Rect b = tree.Vertical ? new Rect(a.Right, area.Y, area.Width - a.Width, area.Height) : new Rect(area.X, a.Bottom, area.Width, area.Height - a.Height);
  ApplyTree(tree.First, a); ApplyTree(tree.Second, b);
 }
 static bool Contains(SectionSplit? tree, string id) => tree is not null && (tree.SectionId == id || Contains(tree.First, id) || Contains(tree.Second, id));
 void PushBoundaries(SectionSplit tree, Rect area, ShowSection section, double dx, double dy, int cornerX, int cornerY)
 {
  if (tree.First is null || tree.Second is null) return;
  double edge = tree.Vertical ? area.X + area.Width * tree.Ratio : area.Y + area.Height * tree.Ratio;
  double requested = tree.Vertical ? (cornerX > 0 ? section.X + section.Width : section.X) : (cornerY > 0 ? section.Y + section.Height : section.Y);
  if (Math.Abs(requested - edge) < .002)
  {
   double delta = tree.Vertical ? dx / area.Width : dy / area.Height;
   double min = Math.Min(.38, tree.Vertical ? 170 / Math.Max(1, StageHost.ActualWidth * area.Width) : 125 / Math.Max(1, StageHost.ActualHeight * area.Height));
   tree.Ratio = Math.Clamp(tree.Ratio + delta, min, 1 - min);
  }
  Rect a = tree.Vertical ? new Rect(area.X, area.Y, area.Width * tree.Ratio, area.Height) : new Rect(area.X, area.Y, area.Width, area.Height * tree.Ratio);
  Rect b = tree.Vertical ? new Rect(a.Right, area.Y, area.Width - a.Width, area.Height) : new Rect(area.X, a.Bottom, area.Width, area.Height - a.Height);
  if (Contains(tree.First, section.Id)) PushBoundaries(tree.First, a, section, dx, dy, cornerX, cornerY);
  else PushBoundaries(tree.Second, b, section, dx, dy, cornerX, cornerY);
 }
 internal ContextMenu SectionMenu(ShowSection section)
 {
  var menu = new ContextMenu();
  var rename = new MenuItem { Header = "Přejmenovat sekci" };
  rename.Click += (_, _) =>
  {
   var label = new TextBlock(); var temporary = new Cue { Text = section.Name };
   string before = section.Name; EditCueText(temporary, label, "Přejmenovat sekci");
   if (temporary.Text != before) { RememberLayout(); section.Name = temporary.Text; MarkDirty(); RenderTiles(); }
  };
  menu.Items.Add(rename);
  var duplicate = new MenuItem { Header = "Duplikovat sekci", IsEnabled = !project.LayoutLocked };
  duplicate.Click += (_, _) => AddSection(section); menu.Items.Add(duplicate);
  var remove = new MenuItem { Header = "Odstranit sekci", IsEnabled = !project.LayoutLocked };
  remove.Click += (_, _) => { if (project.LayoutLocked) return; RememberLayout(); if (active is not null && section.Cues.Contains(active)) ClearPlayer(); project.Sections.Remove(section); ArrangeSections(); MarkDirty(); RenderTiles(); };
  menu.Items.Add(remove); return menu;
 }
 void LayoutLockClick(object sender, RoutedEventArgs e)
 {
  foreach (var view in sectionViews.Values) view.EndDrag(false);
  project.LayoutLocked = !project.LayoutLocked; SyncLayoutLock(); MarkDirty(); RenderTiles();
 }
 void SyncLayoutLock()
 {
  if (LayoutLockGlyph is null) return;
  LayoutLockGlyph.Data = Geometry.Parse(project.LayoutLocked ? "M7,11 L7,7 A5,5 0 0 1 17,7 L17,11 M5,11 L19,11 19,22 5,22 Z M12,15 L12,18" : "M7,11 L7,7 A5,5 0 0 1 17,7 M5,11 L19,11 19,22 5,22 Z M12,15 L12,18");
  LayoutLockButton.ToolTip = project.LayoutLocked ? "Odemknout rozložení" : "Uzamknout rozložení";
  LayoutLockButton.Background = Color(project.LayoutLocked ? "#55503A" : "#20242F");
 }
}
