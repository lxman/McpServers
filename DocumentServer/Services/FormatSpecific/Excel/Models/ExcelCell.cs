namespace DocumentServer.Services.FormatSpecific.Excel.Models;

public class ExcelCell
{
    public string Address { get; set; } = "";
    public int Row { get; set; }
    public int Column { get; set; }
    public object? Value { get; set; }
    public string? Formula { get; set; }
    public string DataType { get; set; } = "";
    public string Style { get; set; } = "";
}
