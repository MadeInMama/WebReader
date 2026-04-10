namespace WebReader.Models.Dtos.Rest;

public class BaseResponseDto<T>
{
    public T? Data { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
}
