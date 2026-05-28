namespace WebReader.Models.Dtos;

public class FilterRequest
{
    public IDictionary<string, string> Values { get; init; } = new Dictionary<string, string>();
}
