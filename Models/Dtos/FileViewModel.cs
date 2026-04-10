using System.Text.Json;

namespace WebReader.Models.Dtos;

public class FileViewModel
{
    public required Guid UserId { get; init; }
    public required Guid FileId { get; init; }
    public required Guid BucketId { get; init; }
    public required string BucketName { get; init; }
    public required string FileName { get; init; }
    public required int Page { get; init; }
    public required int? Scale { get; init; }
    public required string Title { get; init; }
    public string? CurrentPartName { get; init; }
    public Guid? NextPartId { get; init; }
    public Guid? PrevPartId { get; init; }
    public JsonDocument Settings { get; set; } = JsonDocument.Parse("{}");
    public required FileType Type { get; set; }
}
