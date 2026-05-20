namespace FileCleaner.Models;

public class RecommendationProfile
{
    public Dictionary<string, int> DeletedExtensionCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTime LastUpdated { get; set; } = DateTime.Now;
}
