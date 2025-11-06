namespace AwsServer.Core.Models.Controllers.Models;

public class QueryStatistics
{
    public double RecordsMatched { get; set; }
    public double RecordsScanned { get; set; }
    public double BytesScanned { get; set; }
}