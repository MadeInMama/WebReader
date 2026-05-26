using WebReader.Helpers;
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
    public async Task<CustomUser?> AuthenticateAsync(string username, string password,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.FirstOrDefaultAsync(f => f.Username.Equals(username) && f.IsActive, null,
            cancellationToken, true);

        if (user != null && StaticFunctions.VerifyPassword(password, user.PasswordHash)) return user;

        return null;
    }

    public async Task<CustomUser?> CreateUserAsync(string username, string password,
        CancellationToken cancellationToken)
    {
        if (await userRepository.FirstOrDefaultAsync(f => f.Username.Equals(username), null, cancellationToken, true) !=
            null)
            return null;

        var user = new CustomUser
        {
            Username = username,
            PasswordHash = StaticFunctions.HashPassword(password)
        };

        var entity = await userRepository.AddAsync(user, cancellationToken);

        var bucketName = $"personal-{user.Id}";

        var bucket = minioService.CreateBucketAsync(bucketName, cancellationToken);
        var bucket1 = bucketRepository.AddAsync(new Bucket
        {
            Name = bucketName,
            CustomName = "Personal",
            IsHidden = false,
            UserId = user.Id,
            User = user
        }, cancellationToken);

        await Task.WhenAll(bucket, bucket1);

        await userRepository.SaveChangesAsync(cancellationToken);
        await bucketRepository.SaveChangesAsync(cancellationToken);

        return entity;
    }

    public async Task DeleteUserAsync(Guid id,
        CancellationToken cancellationToken)
    {
        var user = await userRepository.FirstOrDefaultAsync(
            f => f.Id == id && f.IsActive,
            null,
            cancellationToken,
            false,
            f => f.UserReadings, f => f.Bucket);

        if (user == null) return;

        var userReadingTask =
            userReadingRepository.DeleteAllAsync(user.UserReadings.Select(f => f.Id), cancellationToken);
        var bucketTask = bucketService.RemoveBucketAsync(user.Bucket, cancellationToken);

        await Task.WhenAll(userReadingTask, bucketTask);
//TODO: delete at once with custom query or context
        await userRepository.DeleteAsync(user.Id, cancellationToken);
    }
}
