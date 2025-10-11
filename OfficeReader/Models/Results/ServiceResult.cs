namespace OfficeReader.Models.Results;

public class ServiceResult<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Error { get; set; }
    public List<string> Warnings { get; set; } = [];
}