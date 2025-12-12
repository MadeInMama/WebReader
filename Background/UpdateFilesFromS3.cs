using Minio.DataModel;
using WebReader.Models;
using WebReader.Repositories;
using WebReader.Services;

namespace WebReader.Background;

public class UpdateFilesFromS3(IServiceProvider services) : BackgroundService
{
    private static readonly TimeSpan PeriodTime = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RemoveBucketsThatNotExistsInDb();
        // await BucketBackgroundProcessing();
        // await FileBackgroundProcessing();

        using PeriodicTimer timer = new(PeriodTime);

        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RemoveBucketsThatNotExistsInDb();
        // await RemoveBucketsThatNotExistsInDb();
        // await BucketBackgroundProcessing();
        // await FileBackgroundProcessing();
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        await base.StopAsync(stoppingToken);
    }

    private async Task BucketBackgroundProcessing()
    {
        using var scope = services.CreateScope();
        var bucketRepository = scope.ServiceProvider.GetRequiredService<BucketRepository>();
        var minioService = scope.ServiceProvider.GetRequiredService<MinioService>();

        var allStoredBuckets = (await bucketRepository.AllAsync(f => true)).ToList();
        var allBucketsInS3 = (await minioService.ListBucketsAsync()).ToList();

        foreach (var storedBucket in allStoredBuckets)
            storedBucket.IsAvailable = allBucketsInS3.Exists(f => f.Name.Equals(storedBucket.Name));

        var tasks = new List<Task>();

        foreach (var storedBucket in allBucketsInS3)
            if (await bucketRepository.FirstOrDefaultAsync(f => f.Name.Equals(storedBucket.Name)) == null)
                tasks.Add(minioService.RemoveBucketAsync(storedBucket.Name));

        //TODO: remove files

        //TODO: delete buckets if user not exists

        tasks.Add(bucketRepository.UpdateAllAsync(allStoredBuckets));

        await Task.WhenAll(tasks);
    }

    private async Task RemoveBucketsThatNotExistsInDb()
    {
        //TODO: run on SettingsEntity
        using var scope = services.CreateScope();
        var bucketRepository = scope.ServiceProvider.GetRequiredService<BucketRepository>();
        var minioService = scope.ServiceProvider.GetRequiredService<MinioService>();

        var allBucketsInDb = (await bucketRepository.AllAsync(f => true)).ToList();
        var allBucketsInS3 = await minioService.ListBucketsAsync();

        var tasks = new List<Task>();

        foreach (var bucketInS3 in allBucketsInS3)
            if (allBucketsInDb.FirstOrDefault(f => f.Name == bucketInS3.Name) == null)
                tasks.Add(minioService.RemoveBucketAsync(bucketInS3.Name));

        await Task.WhenAll(tasks);
    }

    private async Task FileBackgroundProcessing()
    {
        using var scope = services.CreateScope();
        var fileRepository = scope.ServiceProvider.GetRequiredService<FileRepository>();
        var minioService = scope.ServiceProvider.GetRequiredService<MinioService>();

        var bucketFileDictionary = new Dictionary<string, IEnumerable<Item>>();

        var allStoredFiles = (await fileRepository.AllAsync(f => !f.IsHidden, f => f.Bucket!)).ToList();

        foreach (var storedFile in allStoredFiles)
        {
            var currBucketName = storedFile.Bucket!.Name;

            bucketFileDictionary.TryGetValue(currBucketName, out var filesInS3ByBucket);

            if (filesInS3ByBucket == null)
            {
                filesInS3ByBucket = (await minioService.ListObjectsAsync(currBucketName)).ToList();

                if (!filesInS3ByBucket.Any())
                {
                    storedFile.IsAvailable = false;
                    continue;
                }

                bucketFileDictionary.Add(currBucketName, filesInS3ByBucket);
            }

            var currFileInS3 = filesInS3ByBucket.FirstOrDefault(f => f.Key.Equals(storedFile.Name));

            if (currFileInS3 == null ||
                !Enum.TryParse(currFileInS3.Key.Split(".").LastOrDefault(), true, out FileType newFileType))
            {
                storedFile.IsAvailable = false;
                continue;
            }

            storedFile.Type = newFileType;
            storedFile.Size = currFileInS3.Size;
            storedFile.IsAvailable = true;
        }

        await fileRepository.UpdateAllAsync(allStoredFiles);
    }
}
