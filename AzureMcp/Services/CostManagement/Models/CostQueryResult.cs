namespace AzureMcp.Services.CostManagement.Models;

public class CostQueryResult
{
    public string TimeFrame { get; set; } = string.Empty;
    public decimal TotalCost { get; set; }
    public string Currency { get; set; } = string.Empty;
    public List<CostByDate> CostsByDate { get; set; } = [];
    public List<CostByService> CostsByService { get; set; } = [];
    public List<CostByResourceGroup> CostsByResourceGroup { get; set; } = [];
}

public class CostByDate
{
    public DateTime Date { get; set; }
    public decimal Cost { get; set; }
}

public class CostByService
{
    public string ServiceName { get; set; } = string.Empty;
    public decimal Cost { get; set; }
    public string Category { get; set; } = string.Empty;
}

public class CostByResourceGroup
{
    public string ResourceGroupName { get; set; } = string.Empty;
    public decimal Cost { get; set; }
}
