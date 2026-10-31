namespace WebReader.Models.Dtos;

public class SubscribeDto
{
    public required string Endpoint { get; set; }
    public required Dictionary<string, string> Keys { get; set; } = new();
}
