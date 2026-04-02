using System.Security.Cryptography;
using System.Text;
using WebReader.Models.Entities;
using WebReader.Repositories;

namespace WebReader.Services;

public class UserService(
    CustomUserRepository userRepository,
    BucketService bucketService,
    UserReadingRepository userReadingRepository,
    MinioService minioService,
    BucketRepository bucketRepository)
{
    public async Task<CustomUser?> AuthenticateAsync(string username, string password)
    {
        var user = await userRepository.FirstOrDefaultAsync(f => f.Username.Equals(username) && f.IsActive, null, true);

        if (user != null && VerifyPassword(password, user.PasswordHash)) return user;

        return null;
    }

    public async Task<CustomUser?> CreateUserAsync(string username, string password)
    {
        if (await userRepository.FirstOrDefaultAsync(f => f.Username.Equals(username), null, true) != null)
            return null;

        var user = new CustomUser
        {
            Username = username,
            PasswordHash = HashPassword(password)
        };

        var entity = await userRepository.AddAsync(user);

        var bucketName = $"personal-{user.Id}";

        var bucket = minioService.CreateBucketAsync(bucketName);
        var bucket1 = bucketRepository.AddAsync(new Bucket
        {
            Name = bucketName,
            CustomName = "Personal",
            IsHidden = false,
            UserId = user.Id,
            User = user
        });

        await Task.WhenAll(bucket, bucket1);

        await userRepository.SaveChangesAsync();
        await bucketRepository.SaveChangesAsync();

        return entity;
    }

    public async Task DeleteUserAsync(Guid id)
    {
        var user = await userRepository.FirstOrDefaultAsync(
            f => f.Id == id && f.IsActive,
            null,
            false,
            f => f.UserReadings, f => f.Bucket);

        if (user == null) return;

        var userReadingTask = userReadingRepository.DeleteAllAsync(user.UserReadings.Select(f => f.Id));
        var bucketTask = bucketService.RemoveBucketAsync(user.Bucket);

        await Task.WhenAll(userReadingTask, bucketTask);
//TODO: delete at once with custom query or context
        await userRepository.DeleteAsync(user.Id);
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
