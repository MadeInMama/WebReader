namespace WebReader.Models.Dtos;

public class UpdateReadingRequest
{
    public required Guid FileId { get; set; }
    public required int Page { get; set; }
    public required int Scale { get; set; }
    public required bool IsLastPage { get; set; }
}
