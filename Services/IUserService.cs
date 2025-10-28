using WebReader.Models.Entities;

namespace WebReader.Services;

public interface IUserService
{
    Task<CustomUser?> AuthenticateAsync(string username, string password);
    Task<CustomUser?> CreateUserAsync(string username, string password);
}