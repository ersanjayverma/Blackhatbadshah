namespace backend.Data.Entities;

public class UsageTracking
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    public int AnalysisCount { get; set; }
    public int ProModelCount { get; set; }
    public int OpenModelCount { get; set; }
    public int PurchasedProCredits { get; set; }
    public int PurchasedOpenCredits { get; set; }
    public DateTime LastAnalysisAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
