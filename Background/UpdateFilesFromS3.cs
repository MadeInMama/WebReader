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
        await BucketBackgroundProcessing();
        await FileBackgroundProcessing();

        using PeriodicTimer timer = new(PeriodTime);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await BucketBackgroundProcessing();
            await FileBackgroundProcessing();
        }
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

        var allStoredBuckets = (await bucketRepository.AllAsync(f => !f.IsHidden)).ToList();
        var allBucketsInS3 = (await minioService.ListBucketsAsync()).ToList();

        foreach (var storedBucket in allStoredBuckets)
            storedBucket.IsAvailable = allBucketsInS3.Exists(f => f.Name.Equals(storedBucket.Name));

        await bucketRepository.UpdateAllAsync(allStoredBuckets);
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
