namespace AzureServer.Services.CostManagement.Models;

public class BudgetDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string TimeGrain { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal CurrentSpend { get; set; }
    public decimal ForecastedSpend { get; set; }
    public string Currency { get; set; } = string.Empty;
    public List<BudgetNotification> Notifications { get; set; } = [];
}

public class BudgetNotification
{
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public decimal Threshold { get; set; }
    public string ThresholdType { get; set; } = string.Empty;
}
