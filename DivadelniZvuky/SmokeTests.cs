using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace DivadelniZvuky;
public partial class MainWindow
{
 internal async Task RunSmokeTest(string output)
 {
  Directory.CreateDirectory(output);
  if (File.Exists(Path.Combine(output, "test-error.txt"))) File.Delete(Path.Combine(output, "test-error.txt"));
  if (File.Exists(Path.Combine(output, "test-result.txt"))) File.Delete(Path.Combine(output, "test-result.txt"));
  void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
  void ValidatePartition()
  {
   double sum = 0;
   foreach (var section in project.Sections)
   {
    Rect r = Bounds(section); Check(r.Left >= -.00001 && r.Top >= -.00001 && r.Right <= 1.00001 && r.Bottom <= 1.00001, "Section outside workspace"); sum += r.Width * r.Height;
    foreach (var other in project.Sections.Where(s => s != section))
    { Rect intersect = Rect.Intersect(r, Bounds(other)); Check(intersect.IsEmpty || intersect.Width * intersect.Height < .000001, "Sections overlap"); }
   }
   Check(Math.Abs(sum - 1) < .00001, "Packed sections do not fill the workspace");
  }
  string wave = Path.Combine(sessionFolder, "test.wav");
  using (var w = new BinaryWriter(File.Create(wave)))
  {
   w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + 882000); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(44100); w.Write(88200); w.Write((short)2); w.Write((short)16); w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(882000); w.Write(new byte[882000]);
  }
  var original = project.Sections[0]; original.Name = "První dějství";
  original.Cues = [new Cue { Text = "Déšť", Source = wave, FadeOut = true, FadeSeconds = 7, Volume = .6 }, new Cue { Text = "Telefon" }, new Cue { Text = "Kroky" }];
  RenderTiles(); UpdateLayout();
  var tileCanvas = (Canvas)sectionViews[original.Id].Scroll.Content;
  var horizontalChildren = tileCanvas.Children.OfType<FrameworkElement>().ToList();
  Check(horizontalChildren.Select(Canvas.GetTop).Distinct().Count() == 1, "Tiles are not in one horizontal row");
  double addSlot = Canvas.GetLeft(horizontalChildren[^1]);
  AddCue(original); UpdateLayout();
  tileCanvas = (Canvas)sectionViews[original.Id].Scroll.Content;
  var nextChildren = tileCanvas.Children.OfType<FrameworkElement>().ToList();
  Check(nextChildren.Count == 5 && Canvas.GetLeft(nextChildren[3]) < Canvas.GetLeft(nextChildren[4]), "Plus did not advance after the inserted tile");
  UndoLayout(); original = project.Sections[0];
  var cue = original.Cues[0]; var cueView = CreateTile(cue, sectionViews[original.Id], 220);
  Check(cueView.ToolTip is ToolTip { Content: TextBlock { Text: "Poznámka…" } }, "Missing default hover note");
  var menu = cueView.ContextMenu!; var fade = menu.Items.OfType<MenuItem>().Single(i => i.Header as string == "Fade");
  Check(fade.Items.Count == 4, "Fade submenu presets missing");
  menu.PlacementTarget = sectionViews[original.Id]; menu.IsOpen = true; await Task.Delay(100); menu.ApplyTemplate(); fade.ApplyTemplate(); fade.IsSubmenuOpen = true; await Task.Delay(100); UpdateLayout();
  Check(((Popup)fade.Template.FindName("PART_Popup", fade)).IsOpen, "Fade hover submenu did not open");
  fade.IsSubmenuOpen = false; menu.IsOpen = false;
  foreach (int seconds in new[] { 3, 5, 7 })
  {
   ((MenuItem)fade.Items[seconds == 3 ? 1 : seconds == 5 ? 2 : 3]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
   Check(cue.FadeOut && cue.FadeSeconds == seconds, "Fade preset selection failed");
   Check(Math.Abs(EffectiveVolume(1, 1, true, 10 - seconds / 2.0, 10, seconds) - .5) < .0001, "Fade midpoint failed");
   Check(EffectiveVolume(1, 1, true, 10, 10, seconds) == 0, "Fade end failed");
  }
  cue.Note = "Spustit po zhasnutí.\nPočkat na pokyn.";
  dirty = false;
  AddSection(original); ValidatePartition(); Check(project.Sections[1].Y == .5 && project.Sections[1].Width == 1, "Two horizontal section halves failed");
  var duplicate = project.Sections[1];
  Check(duplicate.Cues[0].Id != cue.Id && duplicate.Cues[0].Source == wave && duplicate.Cues[0].FadeSeconds == 7 && duplicate.Cues[0].Volume == .6 && duplicate.Cues[0].Note == cue.Note, "Duplicate did not copy settings/audio/notes independently");
  duplicate.Cues[0].Text = "Změněná kopie"; Check(cue.Text == "Déšť", "Duplicate shares cue model");
  AddSection(original); ValidatePartition(); Check(project.Sections[0].Height == .5 && project.Sections[2].X == .5 && project.Sections[2].Height == 1, "Three section layout failed");
  UpdateLayout(); SavePreview(Path.Combine(output, "preview-three.png"));
  RememberLayout(); TransformSection(original, StageHost.ActualWidth * .15, 0, true);
  ValidatePartition(); Check(original.Width > .6 && project.Sections[2].Width < .4, "Neighbour push failed");
  await Task.Delay(150); UpdateLayout(); SavePreview(Path.Combine(output, "preview-pressure.png"));
  SettleSections(); await Task.Delay(420); UndoLayout();
  Check(Math.Abs(project.Sections[0].Width - .5) < .0001, "Undo pushed layout failed");
  original = project.Sections[0]; cue = original.Cues[0];
  BeginSectionResize(original);
  TransformSection(original, 24, 0, true);
  await Task.Delay(240); UpdateLayout();
  Check(cellInteraction is not null && cellInteraction.Membrane.Displacement.Max() > 3 && original.Width == .5, "Local membrane contact missing or changed the entire column");
  Geometry GlobalClip(SectionSurface view)
  {
   var geometry = view.Clip.Clone(); geometry.Transform = new TranslateTransform(Canvas.GetLeft(view), Canvas.GetTop(view)); return geometry;
  }
  var pushingView = sectionViews[original.Id]; var victimView = sectionViews[project.Sections[2].Id];
  Check(Geometry.Combine(GlobalClip(pushingView), GlobalClip(victimView), GeometryCombineMode.Intersect, null).GetArea() < .1, "Contacting cell surfaces overlap");
  SavePreview(Path.Combine(output, "preview-contact.png"));
  int sourceRebounds = pushingView.SettleCount, victimRebounds = victimView.SettleCount;
  FinishSectionResize(); await Task.Delay(900);
  Check(original.Width == .5 && pushingView.SettleCount == sourceRebounds && victimView.SettleCount == victimRebounds, "Released contact changed layout or rebounded without a snap");
  BeginSectionResize(original);
  TransformSection(original, 200, 0, true); ValidatePartition();
  Check(original.Width == 1 && project.Sections[2].Y == .5 && project.Sections[2].Height == .5, "Top quarter did not displace the full-height neighbour into the bottom row");
  Check(pushingView.SettleCount == sourceRebounds && victimView.SettleCount == victimRebounds, "Rebound happened before landing");
  await Task.Delay(145); UpdateLayout();
  Check(pushingView.SettleCount == sourceRebounds && victimView.SettleCount == victimRebounds + 1, "Rebound must apply only to the displaced cell after landing");
  SavePreview(Path.Combine(output, "preview-reflow.png"));
  FinishSectionResize(); await Task.Delay(420); ValidatePartition();
  UndoLayout(); Check(project.Sections[0].Width == .5 && project.Sections[2].Height == 1, "Undo topology change failed");
  var lowerLeft = project.Sections[1];
  BeginSectionResize(lowerLeft); TransformSection(lowerLeft, 200, 0, true); ValidatePartition();
  Check(lowerLeft.Width == 1 && lowerLeft.Y == .5 && project.Sections[2].Y == 0 && project.Sections[2].Height == .5, "Lower quarter mirrored reflow failed");
  FinishSectionResize(); await Task.Delay(400); UndoLayout();
  original = project.Sections[0]; cue = original.Cues[0];
  AddSection(original); ValidatePartition(); Check(project.Sections.All(s => s.Width == .5 && s.Height == .5), "Quarter layout failed");
  UpdateLayout(); SavePreview(Path.Combine(output, "preview-four.png"));
  // Repeat resizing in opposing directions: never overlap or escape the monitor.
  for (int i = 0; i < 30; i++) { TransformSection(project.Sections[i % 4], (i % 2 == 0 ? 1 : -1) * 20, 9, true); ValidatePartition(); }
  ArrangeSections(); RenderTiles(); UpdateLayout();
  SectionMenu(project.Sections[^1]).Items.OfType<MenuItem>().Single(i => i.Header as string == "Odstranit sekci").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
  Check(project.Sections.Count == 3, "Section delete menu failed");
  UndoLayout(); Check(project.Sections.Count == 4 && project.Sections[^1].Cues[0].Source == wave, "Undo section delete failed");
  original = project.Sections[0]; cue = original.Cues[0];
  RememberLayout(); project.Sections = [original]; project.LayoutTree = null;
  original.X = 0; original.Y = 0; original.Width = .4; original.Height = .4; RenderTiles(); UpdateLayout();
  TransformSection(original, 10000, 10000, false);
  Check(Math.Abs(original.X + original.Width - 1) < .00001 && Math.Abs(original.Y + original.Height - 1) < .00001, "Free section movement escaped workspace");
  UndoLayout(); Check(project.Sections.Count == 4, "Undo free section movement failed"); original = project.Sections[0]; cue = original.Cues[0];
  LayoutLockClick(this, new RoutedEventArgs());
  string before = JsonSerializer.Serialize(project.Sections);
  TransformSection(original, 90, 90, true); AddSection(original);
  var surface = sectionViews[original.Id]; surface.BeginDrag(cue); Check(!surface.IsDragging, "Locked tile drag allowed");
  Check(before == JsonSerializer.Serialize(project.Sections), "Lock allowed a layout change");
  LayoutLockClick(this, new RoutedEventArgs());
  surface.BeginDrag(cue); surface.PreviewAt(2); surface.EndDrag(true); Check(original.Cues[2] == cue, "Tile reorder failed");
  UndoLayout(); original = project.Sections[0]; cue = original.Cues[0]; Check(cue.Text == "Déšť", "Undo tile reorder failed");
  DeleteCue(cue); Check(!AllCues().Any(c => c.Id == cue.Id), "Delete tile failed"); UndoLayout();
  original = project.Sections[0]; cue = original.Cues[0]; Check(cue.Source == wave, "Undo delete lost sound");
  // Horizontal overflow and scrollbar orientation.
  for (int i = 0; i < 16; i++) original.Cues.Add(new Cue { Text = "Efekt " + (i + 1) });
  RenderTiles(); UpdateLayout(); surface = sectionViews[original.Id];
  Check(surface.Scroll.VerticalScrollBarVisibility == ScrollBarVisibility.Disabled && surface.Scroll.ScrollableWidth > 0, "Horizontal overflow missing");
  surface.Scroll.ScrollToRightEnd(); UpdateLayout(); Check(surface.Scroll.HorizontalOffset > 0 && surface.Scroll.VerticalOffset == 0, "Horizontal scroll failed");
  void ToggleSizing(ShowSection section)
  {
   var item = SectionMenu(section).Items.OfType<MenuItem>().Single(i => i.Header is string text && text.StartsWith("Autosize / Scroll"));
   item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
   UpdateLayout();
  }
  ToggleSizing(original);
  tileCanvas = (Canvas)surface.Scroll.Content;
  var fitted = tileCanvas.Children.OfType<FrameworkElement>().ToArray();
  Check(original.AutoSizeTiles && project.Sections.Skip(1).All(s => !s.AutoSizeTiles), "Sizing toggle affected other sections");
  Check(surface.Scroll.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled && surface.Scroll.ScrollableWidth == 0 && surface.Scroll.HorizontalOffset == 0, "Autosize still scrolls");
  Check(fitted.Length == original.Cues.Count + 1 && fitted[^1] is Button, "Autosize omitted a tile or the add button");
  Check(fitted.Select(Canvas.GetTop).Distinct().Count() > 1, "Autosize failed to use the available rows");
  Check(fitted.All(tile => tile.Width > 0 && tile.Width == tile.Height &&
   Canvas.GetLeft(tile) + tile.Width <= surface.Scroll.ViewportWidth + .01 &&
   Canvas.GetTop(tile) + tile.Height <= surface.Scroll.ViewportHeight + .01), "Autosize clipped or stretched a tile");
  Check(fitted[0].ToolTip is ToolTip { Content: TextBlock note } && note.Text == cue.Note, "Saved note missing from the hover card");
  ToggleSizing(original);
  Check(!original.AutoSizeTiles && surface.Scroll.ScrollableWidth > 0, "Returning to Scroll did not restore overflow");
  ToggleSizing(original);
  projectPath = Path.Combine(output, "sections.divz"); project.LayoutLocked = true; SyncLayoutLock();
  Check(SaveProject(), "Save sections failed");
  File.Delete(wave); OpenProject(projectPath);
  Check(project.Sections.Count == 4 && project.LayoutLocked && project.Sections[0].Name == "První dějství" && project.Sections[0].AutoSizeTiles && AllCues().First().Note == cue.Note && AllCues().First().FadeSeconds == 7 && File.Exists(AllCues().First().Source), "Sections/settings/audio round-trip failed");
  project.LayoutLocked = false; SyncLayoutLock();
  // Migration of a pre-section project.
  var legacy = new ShowProject { Version = 1, Cues = [new Cue { Text = "Starý projekt", FadeOut = true }] };
  NormalizeSections(legacy); Check(legacy.Sections.Count == 1 && legacy.Sections[0].Cues[0].FadeSeconds == 3, "Legacy migration failed");
  var ready = new TaskCompletionSource(); EventHandler opened = (_, _) => ready.TrySetResult(); player.MediaOpened += opened;
  StartCue(AllCues().First()); Check(await Task.WhenAny(ready.Task, Task.Delay(7000)) == ready.Task, "WAV playback failed"); player.MediaOpened -= opened;
  Check(playing, "Player did not start"); Stop();
  // Fullscreen still uses the physical monitor bounds.
  ToggleFullscreen(); UpdateLayout(); var handle = new WindowInteropHelper(this).Handle; var bounds = FullMonitorBounds(handle); GetWindowRect(handle, out var actual);
  Check(actual.Right == bounds.Right && actual.Bottom == bounds.Bottom, "Fullscreen regressed"); ToggleFullscreen();
  await Task.Delay(400); RenderTiles(); UpdateLayout(); SavePreview(Path.Combine(output, "preview-final.png"));
  dirty = false;
  File.WriteAllText(Path.Combine(output, "test-result.txt"), "PASS: horizontal scroll and inline plus; per-section Autosize with square tiles; hover notes and project persistence; section geometry, reflow, rebound, duplication and undo; fade presets; layout lock; WAV playback and fullscreen. Animation feel requires manual verification.");
 }
}
