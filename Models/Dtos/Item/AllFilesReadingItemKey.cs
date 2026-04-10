namespace WebReader.Models.Dtos.Item;

public class AllFilesReadingItemKey
{
    public required Guid BucketId { get; init; }
    public required string CustomName { get; init; }

    private bool Equals(AllFilesReadingItemKey? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Equals(BucketId, other.BucketId) &&
               string.Equals(CustomName, other.CustomName, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as AllFilesReadingItemKey);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(BucketId, CustomName);
    }

    public static bool operator ==(AllFilesReadingItemKey? left, AllFilesReadingItemKey? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(AllFilesReadingItemKey? left, AllFilesReadingItemKey? right)
    {
        return !Equals(left, right);
    }
}
