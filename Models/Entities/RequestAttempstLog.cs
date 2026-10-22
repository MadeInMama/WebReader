namespace WebReader.Models.Entities;

public class RequestAttemptsLog
{
    public required string Ip { get; init; }

    //context.HttpContext.Request.Path + context.HttpContext.Request.QueryString
    public required string Link { get; init; }

    public DateTimeOffset CreatedDate { get; } = DateTimeOffset.UtcNow;

    public required bool IsAuthenticated { get; init; }

    //TODO:blacklist ips
}
