namespace WebReader.Models.Dtos;

public class FilterRequest
{
    public IDictionary<string, List<string>> Values { get; init; } = new Dictionary<string, List<string>>();
}
