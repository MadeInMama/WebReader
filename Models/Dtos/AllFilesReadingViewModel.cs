namespace WebReader.Models.Dtos;

public class AllFilesReadingViewModel
{
    public required IDictionary<AllFilesReadingItemKey, IEnumerable<AllFilesReadingItem>> Items { get; init; }
}

public class AllFilesReadingItemKey
{
    public required string Name { get; init; }
    public required string CustomName { get; init; }

    private bool Equals(AllFilesReadingItemKey? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return string.Equals(Name, other.Name, StringComparison.Ordinal) &&
               string.Equals(CustomName, other.CustomName, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as AllFilesReadingItemKey);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, CustomName);
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

public class AllFilesReadingItem
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string CustomName { get; init; }
    public DateTimeOffset DateTime { get; init; }
    public ulong Size { get; init; }
    public int Page { get; init; }
}
