namespace WebReader.Models.Dtos;

public class UpdatePageRequest
{
    public required Guid FileId { get; set; }
    public required Guid UserId { get; set; }
    public required int Page { get; set; }
    public required int Scale { get; set; }
}
