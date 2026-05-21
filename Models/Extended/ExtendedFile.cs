namespace WebReader.Models.Extended;

public class ExtendedFile
{
    public Guid Id { get; set; }
    public DateTimeOffset UpdatedDate { get; set; }
    public string Name { get; set; }
    public string? CustomName { get; set; }
    public FileType Type { get; set; }
    public Guid? NextPartId { get; set; }
    public ulong? Size { get; set; }
    public int TotalCount { get; set; }
    public ulong TotalSize { get; set; }
}
