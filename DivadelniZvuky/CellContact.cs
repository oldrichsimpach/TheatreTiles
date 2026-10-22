using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace DivadelniZvuky;

// One shared, damped spring membrane drives BOTH contacting walls.
internal sealed class ContactMembrane
{
 internal const int Count = 41;
 internal readonly double[] Displacement = new double[Count];
 readonly double[] velocity = new double[Count];
 internal double Force, Centre = .68;
 internal double Energy => Displacement.Max(Math.Abs) + velocity.Max(Math.Abs) * .03;
 internal void Step(double dt)
 {
  dt = Math.Clamp(dt, 0, .04);
  int steps = Math.Max(1, (int)Math.Ceiling(dt / .005)); double h = dt / steps;
  for (int step = 0; step < steps; step++)
  {
   var previous = Displacement.ToArray();
   for (int i = 1; i < Count - 1; i++)
   {
    double t = i / (double)(Count - 1);
    double target = Force * Math.Exp(-Math.Pow((t - Centre) / .28, 2)) * Math.Pow(Math.Sin(Math.PI * t), 2);
    double acceleration = 240 * (target - previous[i]) - 25 * velocity[i] + 150 * (previous[i - 1] - 2 * previous[i] + previous[i + 1]);
    velocity[i] += acceleration * h;
    Displacement[i] = Math.Max(0, previous[i] + velocity[i] * h);
   }
  }
 }
}

public sealed partial class SectionSurface
{
 internal int SettleCount;
 Geometry? contactClip;
 internal void ClearContact() { contactClip = null; UpdateWall(); }
 internal void ShowContact(Rect baseRect, bool vertical, bool source, bool towardPositive, double start, double end, double[] displacements)
 {
  double max = displacements.Max();
  double extension = source ? max : 0;
  Rect frame = baseRect;
  if (vertical) { if (source && !towardPositive) frame.X -= extension; frame.Width += extension; }
  else { if (source && !towardPositive) frame.Y -= extension; frame.Height += extension; }
  BeginAnimation(WidthProperty, null); BeginAnimation(HeightProperty, null); BeginAnimation(Canvas.LeftProperty, null); BeginAnimation(Canvas.TopProperty, null);
  Width = frame.Width; Height = frame.Height; Canvas.SetLeft(this, frame.X); Canvas.SetTop(this, frame.Y);
  var body = new RectangleGeometry(new Rect(baseRect.X - frame.X, baseRect.Y - frame.Y, baseRect.Width, baseRect.Height), 20, 20);
  bool positiveEdge = source ? towardPositive : !towardPositive;
  double baseline = vertical ? (positiveEdge ? baseRect.Right : baseRect.Left) : (positiveEdge ? baseRect.Bottom : baseRect.Top);
  double sign = towardPositive ? 1 : -1;
  var points = new Point[displacements.Length];
  for (int i = 0; i < points.Length; i++)
  {
   double across = start + (end - start) * i / (points.Length - 1);
   points[i] = vertical ? new Point(baseline + sign * displacements[i] - frame.X, across - frame.Y) : new Point(across - frame.X, baseline + sign * displacements[i] - frame.Y);
  }
  var bulge = new StreamGeometry();
  using (var path = bulge.Open())
  {
   path.BeginFigure(points[0], true, true);
   // Catmull-Rom interpolation gives a continuous tangent through contact nodes.
   for (int i = 0; i < points.Length - 1; i++)
   {
    Point p0 = points[Math.Max(0, i - 1)], p1 = points[i], p2 = points[i + 1], p3 = points[Math.Min(points.Length - 1, i + 2)];
    path.BezierTo(p1 + (p2 - p0) / 6, p2 - (p3 - p1) / 6, p2, true, false);
   }
  }
  contactClip = new CombinedGeometry(source ? GeometryCombineMode.Union : GeometryCombineMode.Exclude, body, bulge);
  Clip = contactClip; wall.Data = contactClip;
 }
}

public partial class MainWindow
{
 sealed class CellInteraction
 {
  internal required ShowSection Source;
  internal required SectionSplit Fork;
  internal required SectionSplit Nested;
  internal required Rect Region;
  internal required List<ShowSection> Neighbours;
  internal bool NestedFirst, ActiveFirst, Positive;
  internal bool Rotate = true;
  internal double Force, Threshold;
  internal readonly ContactMembrane Membrane = new();
 }
 readonly DispatcherTimer contactTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
 CellInteraction? cellInteraction;
 string? resizeSectionId;
 bool contactCommitted, contactReleased;
 int contactRevision;
 int resizeUndoCount;
 internal void BeginSectionResize(ShowSection section)
 {
  if (project.LayoutLocked) return;
  contactRevision++;
  ResetCellContact(); RememberLayout(); resizeUndoCount = undoHistory.Count;
  resizeSectionId = section.Id; contactCommitted = false;
 }
 CellInteraction? FindCellInteraction(SectionSplit fork, Rect region, ShowSection source, double dx, double dy, int cornerX, int cornerY)
 {
  if (fork.First is null || fork.Second is null) return null;
  double split = fork.Vertical ? region.X + region.Width * fork.Ratio : region.Y + region.Height * fork.Ratio;
  Rect first = fork.Vertical ? new Rect(region.X, region.Y, split - region.X, region.Height) : new Rect(region.X, region.Y, region.Width, split - region.Y);
  Rect second = fork.Vertical ? new Rect(split, region.Y, region.Right - split, region.Height) : new Rect(region.X, split, region.Width, region.Bottom - split);
  bool nestedFirst = Contains(fork.First, source.Id);
  var nested = nestedFirst ? fork.First : fork.Second;
  var opposite = nestedFirst ? fork.Second : fork.First;
  double force = fork.Vertical ? dx : dy;
  int corner = fork.Vertical ? cornerX : cornerY;
  bool simpleContact = nested.SectionId == source.Id;
  bool quarterContact = nested.First is not null && nested.Second is not null && nested.Vertical != fork.Vertical &&
      (nested.First.SectionId == source.Id || nested.Second.SectionId == source.Id);
  if ((simpleContact || quarterContact) && corner == (nestedFirst ? 1 : -1) && force * (nestedFirst ? 1 : -1) > 0)
  {
   double neighbourSize = fork.Vertical ? (nestedFirst ? second.Width : first.Width) * StageHost.ActualWidth : (nestedFirst ? second.Height : first.Height) * StageHost.ActualHeight;
   return new CellInteraction
   {
    Source = source, Fork = fork, Nested = nested, Region = region, NestedFirst = nestedFirst,
    ActiveFirst = nested.First?.SectionId == source.Id, Positive = nestedFirst, Rotate = !simpleContact,
    Neighbours = project.Sections.Where(s => Contains(opposite, s.Id) && (fork.Vertical ? Math.Min(s.Y + s.Height, source.Y + source.Height) - Math.Max(s.Y, source.Y) : Math.Min(s.X + s.Width, source.X + source.Width) - Math.Max(s.X, source.X)) > .001).ToList(),
    Threshold = Math.Clamp(neighbourSize * .2, 65, 150),
   };
  }
  return FindCellInteraction(nested, nestedFirst ? first : second, source, dx, dy, cornerX, cornerY);
 }
 bool TryCellContact(ShowSection section, double dx, double dy, int cornerX, int cornerY)
 {
  if (resizeSectionId != section.Id || project.LayoutTree is null) return false;
  if (contactCommitted) return true;
  cellInteraction ??= FindCellInteraction(project.LayoutTree, new Rect(0, 0, 1, 1), section, dx, dy, cornerX, cornerY);
  var contact = cellInteraction; if (contact is null) return false;
  double delta = (contact.Fork.Vertical ? dx : dy) * (contact.Positive ? 1 : -1);
  contact.Force = Math.Max(0, contact.Force + delta);
  contact.Membrane.Centre = contact.Fork.Vertical ? (cornerY > 0 ? .68 : .32) : (cornerX > 0 ? .68 : .32);
  contact.Membrane.Force = Math.Min(contact.Force, contact.Threshold);
  contactReleased = false;
  contactTimer.Tick -= ContactTick; contactTimer.Tick += ContactTick; contactTimer.Start();
  if (contact.Force >= contact.Threshold) CommitCellReflow(contact);
  return true;
 }
 void ContactTick(object? sender, EventArgs e)
 {
  var contact = cellInteraction; if (contact is null) { contactTimer.Stop(); return; }
  contact.Membrane.Step(.016);
  RenderContact(contact);
  if (contactReleased && contact.Membrane.Energy < .15) ResetCellContact();
 }
 void RenderContact(CellInteraction contact)
 {
  double width = StageHost.ActualWidth, height = StageHost.ActualHeight;
  bool vertical = contact.Fork.Vertical;
  Rect source = PixelBounds(contact.Source);
  double start = vertical ? source.Top + 20 : source.Left + 20;
  double end = vertical ? source.Bottom - 20 : source.Right - 20;
  if (end <= start) return;
  if (sectionViews.TryGetValue(contact.Source.Id, out var activeView)) activeView.ShowContact(source, vertical, true, contact.Positive, start, end, contact.Membrane.Displacement);
  foreach (var neighbour in contact.Neighbours)
   if (sectionViews.TryGetValue(neighbour.Id, out var view)) view.ShowContact(PixelBounds(neighbour), vertical, false, contact.Positive, start, end, contact.Membrane.Displacement);
 }
 Rect PixelBounds(ShowSection section) => new(section.X * StageHost.ActualWidth, section.Y * StageHost.ActualHeight, Math.Max(1, section.Width * StageHost.ActualWidth - (section.Width < 1 ? 6 : 0)), Math.Max(1, section.Height * StageHost.ActualHeight - (section.Height < 1 ? 6 : 0)));
 void ResetCellContact()
 {
  contactTimer.Stop(); cellInteraction = null; contactReleased = false;
  foreach (var view in sectionViews.Values) view.ClearContact();
  LayoutSections();
 }
 void CommitCellReflow(CellInteraction contact)
 {
  var before = project.Sections.ToDictionary(s => s.Id, Bounds);
  if (!contact.Rotate)
  {
   double length = contact.Fork.Vertical ? contact.Region.Width * StageHost.ActualWidth : contact.Region.Height * StageHost.ActualHeight;
   contact.Fork.Ratio = Math.Clamp(contact.Fork.Ratio + (contact.Positive ? 1 : -1) * contact.Force / Math.Max(1, length), .18, .82);
  }
  else
  {
  // Rotate the split tree: the pushing quarter becomes a full-width/height half.
  var activeLeaf = contact.ActiveFirst ? contact.Nested.First! : contact.Nested.Second!;
  var remaining = contact.ActiveFirst ? contact.Nested.Second! : contact.Nested.First!;
  var opposite = contact.NestedFirst ? contact.Fork.Second! : contact.Fork.First!;
  var remainder = new SectionSplit { Vertical = contact.Fork.Vertical, Ratio = contact.Fork.Ratio, First = contact.NestedFirst ? remaining : opposite, Second = contact.NestedFirst ? opposite : remaining };
  contact.Fork.Vertical = contact.Nested.Vertical; contact.Fork.Ratio = contact.Nested.Ratio;
  contact.Fork.First = contact.ActiveFirst ? activeLeaf : remainder;
  contact.Fork.Second = contact.ActiveFirst ? remainder : activeLeaf;
  }
  ResetCellContact();
  ApplyTree(project.LayoutTree!, new Rect(0, 0, 1, 1));
  var final = project.Sections.ToDictionary(s => s.Id, Bounds);
  // First compress the displaced neighbours, then grow into the cleared space.
  foreach (var s in project.Sections)
  {
   Rect r = final[s.Id];
   if (s == contact.Source) r = before[s.Id];
   s.X = r.X; s.Y = r.Y; s.Width = r.Width; s.Height = r.Height;
  }
  LayoutSections(true);
  foreach (var s in project.Sections) { Rect r = final[s.Id]; s.X = r.X; s.Y = r.Y; s.Width = r.Width; s.Height = r.Height; }
  contactCommitted = true; MarkDirty();
  var delay = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(125) };
  int revision = contactRevision;
  delay.Tick += (_, _) =>
  {
   delay.Stop(); if (revision != contactRevision) return; LayoutSections(true);
   foreach (var neighbour in contact.Neighbours)
    if (sectionViews.TryGetValue(neighbour.Id, out var view)) { view.SettleCount++; view.Settle(); }
  }; delay.Start();
 }
 internal void FinishSectionResize()
 {
  resizeSectionId = null;
  if (cellInteraction is not null)
  {
   contactReleased = true; cellInteraction.Membrane.Force = 0;
   if (!contactCommitted && undoHistory.Count == resizeUndoCount) undoHistory.Pop();
  }
  // Rebound is scheduled by CommitCellReflow after the victim has landed.
 }
 void CancelSectionResize()
 {
  contactRevision++; resizeSectionId = null; contactCommitted = false; ResetCellContact();
 }
}
