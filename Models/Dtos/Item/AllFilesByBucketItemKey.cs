using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebReader.Models.Dtos.Item;

[JsonConverter(typeof(AllFilesByBucketItemKeyConverter))]
public class AllFilesByBucketItemKey : IEquatable<AllFilesByBucketItemKey>
{
    public required Guid Id { get; init; }
    public required string CustomName { get; init; }
    public required bool IsBelongsToUser { get; init; }

    public bool Equals(AllFilesByBucketItemKey? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return Equals(Id, other.Id);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as AllFilesByBucketItemKey);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public static bool operator ==(AllFilesByBucketItemKey? left, AllFilesByBucketItemKey? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(AllFilesByBucketItemKey? left, AllFilesByBucketItemKey? right)
    {
        return !Equals(left, right);
    }
}

public class AllFilesByBucketItemKeyConverter : JsonConverter<AllFilesByBucketItemKey>
{
    public override AllFilesByBucketItemKey Read(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        var json = reader.GetString() ?? throw new JsonException("Expected a string value");
        return ParseString(json);
    }

    public override void Write(Utf8JsonWriter writer, AllFilesByBucketItemKey value, JsonSerializerOptions options)
    {
        writer.WriteStringValue($"{value.Id}|{value.CustomName}|{value.IsBelongsToUser}");
    }

    public override AllFilesByBucketItemKey ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert,
        JsonSerializerOptions options)
    {
        var json = reader.GetString() ?? throw new JsonException("Expected a string value for property name");
        return ParseString(json);
    }

    public override void WriteAsPropertyName(Utf8JsonWriter writer, AllFilesByBucketItemKey value,
        JsonSerializerOptions options)
    {
        writer.WritePropertyName($"{value.Id}|{value.CustomName}|{value.IsBelongsToUser}");
    }

    private static AllFilesByBucketItemKey ParseString(string json)
    {
        var parts = json.Split('|');

        if (parts.Length != 3 ||
            !Guid.TryParse(parts[0], out var bucketId) ||
            !bool.TryParse(parts[2], out var isBelongs))
            throw new JsonException(
                $"Invalid format for AllFilesByBucketItemKey: {json}. Expected format: 'Guid|String|Boolean'");

        return new AllFilesByBucketItemKey
        {
            Id = bucketId,
            CustomName = parts[1],
            IsBelongsToUser = isBelongs
        };
    }
}
