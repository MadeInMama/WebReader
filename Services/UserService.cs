using System.Security.Cryptography;
using System.Text;
using WebReader.Models.Entities;
using WebReader.Repositories;

namespace WebReader.Services;

public class UserService(CustomUserRepository userRepository, BucketService bucketService)
{
    public async Task<CustomUser?> AuthenticateAsync(string username, string password)
    {
        var user = await userRepository.FirstOrDefaultAsync(f => f.Username.Equals(username) && f.IsActive);

        if (user != null && VerifyPassword(password, user.PasswordHash)) return user;

        return null;
    }

    public async Task<CustomUser?> CreateUserAsync(string username, string password)
    {
        if (await userRepository.FirstOrDefaultAsync(f => f.Username.Equals(username)) != null)
            return null;

        var user = new CustomUser
        {
            Username = username,
            PasswordHash = HashPassword(password)
        };

        var entity = await userRepository.AddAsync(user);

        await bucketService.CreatePersonalBucketAsync(entity);

        return entity;
    }

    private static string HashPassword(string password)
    {
        var hashedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }

    private static bool VerifyPassword(string password, string hash)
    {
        var hashOfInput = HashPassword(password);
        return hashOfInput == hash;
    }
}
