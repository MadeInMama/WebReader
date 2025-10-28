namespace WebReader.Models.Dtos;

public class FileViewModel
{
    public required Guid UserId { get; init; }
    public Guid? FileId { get; init; }
    public required string Url { get; init; }
    public required int Page { get; init; }
    public required string Title { get; init; }
    public int? SendUpdateInSeconds { get; init; }
}