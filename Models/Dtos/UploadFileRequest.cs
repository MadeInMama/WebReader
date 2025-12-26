using Microsoft.AspNetCore.Mvc.Rendering;

namespace WebReader.Models.Dtos;

public class UploadFileRequest
{
    public required Guid BucketId { get; set; }
    public string? CustomName { get; set; }
    public IFormFile? File { get; set; }
    public Guid? AsPartOfId { get; set; }
    public Guid? AsParentOfId { get; set; }
    public IEnumerable<SelectListItem> Parts { get; set; } = [];
}
