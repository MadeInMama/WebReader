namespace WebReader.Models.Dtos;

public class FileViewModel
{
    public required Guid UserId { get; init; }
    public required Guid FileId { get; init; }
    public required Guid BucketId { get; init; }
    public required string FileName { get; init; }
    public required int Page { get; init; }
    public required int Scale { get; init; }
    public required string Title { get; init; }
}
