namespace WebReader.Models.Dtos;

public class UploadFileRequest
{
    public required Guid BucketId { get; set; }
    public string? CustomName { get; set; }
    public IFormFile? File { get; set; }
}
