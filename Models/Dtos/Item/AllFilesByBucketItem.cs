namespace WebReader.Models.Dtos.Item;

public class AllFilesByBucketItem
{
    public required Guid Id { get; init; }
    public required string FileName { get; init; }
    public bool IsReading { get; init; }
    public bool IsParted { get; init; }
    public bool IsDone { get; init; }
    public int TotalCount { get; set; }
}
