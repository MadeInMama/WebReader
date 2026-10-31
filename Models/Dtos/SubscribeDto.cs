namespace WebReader.Models.Dtos;

public class SubscribeDto
{
    public string? Endpoint { get; set; }
    public Dictionary<string, string> Keys { get; set; } = new();
}
