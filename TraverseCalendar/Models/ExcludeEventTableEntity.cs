using Azure;
using Azure.Data.Tables;

namespace TraverseCalendar.Models;

public class ExcludeEventTableEntity : ITableEntity
{
    public string Subject { get; set; } = string.Empty;

    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}