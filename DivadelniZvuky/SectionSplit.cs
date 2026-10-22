namespace DivadelniZvuky;
public sealed class SectionSplit
{
 public string? SectionId { get; set; }
 public bool Vertical { get; set; }
 public double Ratio { get; set; } = .5;
 public SectionSplit? First { get; set; }
 public SectionSplit? Second { get; set; }
}
