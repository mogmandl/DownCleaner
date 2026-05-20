namespace FileCleaner.Models;

public class ChartItem
{
    public string Label { get; set; } = "";
    public double Value { get; set; }
    public double Percent { get; set; }
    public string Display { get; set; } = "";
    public System.Windows.Media.Brush BarBrush { get; set; } = System.Windows.Media.Brushes.Gray;
}
