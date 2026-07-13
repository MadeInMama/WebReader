using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebReader.Models.Dtos.Item;

[JsonConverter(typeof(AllFilesReadingItemKeyConverter))]
public class AllFilesReadingItemKey
{
    public required Guid BucketId { get; init; }
    public required string CustomName { get; init; }

    private bool Equals(AllFilesReadingItemKey? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Equals(BucketId, other.BucketId);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as AllFilesReadingItemKey);
    }

    public override int GetHashCode()
    {
        return BucketId.GetHashCode();
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

public class AllFilesReadingItemKeyConverter : JsonConverter<AllFilesReadingItemKey>
{
    public override AllFilesReadingItemKey Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        var json = reader.GetString() ?? throw new JsonException("Expected a string value");
        var parts = json.Split('|');
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var bucketId))
            throw new JsonException($"Invalid format for AllFilesReadingItemKey: {json}");
        return new AllFilesReadingItemKey { BucketId = bucketId, CustomName = parts[1] };
    }

    public override void Write(Utf8JsonWriter writer, AllFilesReadingItemKey value, JsonSerializerOptions options)
    {
        writer.WriteStringValue($"{value.BucketId}|{value.CustomName}");
    }

    public override AllFilesReadingItemKey ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        var json = reader.GetString() ?? throw new JsonException("Expected a string value");
        var parts = json.Split('|');
        if (parts.Length != 2 || !Guid.TryParse(parts[0], out var bucketId))
            throw new JsonException($"Invalid format for AllFilesReadingItemKey: {json}");
        return new AllFilesReadingItemKey { BucketId = bucketId, CustomName = parts[1] };
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, AllFilesReadingItemKey value,
        JsonSerializerOptions options)
    {
        writer.WritePropertyName($"{value.BucketId}|{value.CustomName}");
    }
}
