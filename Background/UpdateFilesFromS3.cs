using Minio.DataModel;
using WebReader.Helpers;
using WebReader.Models;
using WebReader.Repositories;
using WebReader.Services;
using Bucket = WebReader.Models.Entities.Bucket;
using File = WebReader.Models.Entities.File;

namespace WebReader.Background;

public class UpdateFilesFromS3(IServiceProvider services, ILogger<UpdateFilesFromS3> logger) : BackgroundService
{
    private static readonly TimeSpan PeriodTime = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RemoveBucketsThatNotExistsInDb(stoppingToken);
        await MakeUnavailableBucketsThatNotExistsInS3(stoppingToken);
        await RemoveFilesThatNotExistsInDb(stoppingToken);
        await UpdateBucketData(stoppingToken);
        await UpdateFilesData(stoppingToken);

        using PeriodicTimer timer = new(PeriodTime);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RemoveBucketsThatNotExistsInDb(stoppingToken);
            await MakeUnavailableBucketsThatNotExistsInS3(stoppingToken);
            await RemoveFilesThatNotExistsInDb(stoppingToken);
            await UpdateBucketData(stoppingToken);
            await UpdateFilesData(stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        await base.StopAsync(stoppingToken);
    }

    private async Task RemoveBucketsThatNotExistsInDb(CancellationToken stoppingToken = default)
    {
        logger.LogInformation($"Start {nameof(RemoveBucketsThatNotExistsInDb)}");
        //TODO: run on SettingsEntity
        using var scope = services.CreateScope();
        var bucketRepository = scope.ServiceProvider.GetRequiredService<BucketRepository>();
        var minioService = scope.ServiceProvider.GetRequiredService<MinioService>();

        var allBucketsInDb = (await bucketRepository.AllAsync(f => true)).ToList();

        logger.LogInformation(
            $"{nameof(RemoveBucketsThatNotExistsInDb)}: Found {{count}} buckets in database", allBucketsInDb.Count);

        var allBucketsInS3 = (await minioService.ListBucketsAsync()).ToList();

        logger.LogInformation(
            $"{nameof(RemoveBucketsThatNotExistsInDb)}: Found {{count}} buckets in s3", allBucketsInS3.Count);

        var tasks = new List<Task>();

        foreach (var bucketInS3 in allBucketsInS3)
            if (allBucketsInDb.FirstOrDefault(f => f.Name == bucketInS3.Name) == null)
            {
                logger.LogInformation(
                    $"{nameof(RemoveBucketsThatNotExistsInDb)}: Bucket {{bucketName}} will be removed",
                    bucketInS3.Name);
                tasks.Add(minioService.RemoveBucketAsync(bucketInS3.Name));
            }

        await Task.WhenAll(tasks);
        logger.LogInformation($"Finished {nameof(RemoveBucketsThatNotExistsInDb)}");
    }

    private async Task MakeUnavailableBucketsThatNotExistsInS3(CancellationToken stoppingToken = default)
    {
        logger.LogInformation($"Start {nameof(MakeUnavailableBucketsThatNotExistsInS3)}");
        //TODO: run on SettingsEntity
        using var scope = services.CreateScope();
        var bucketRepository = scope.ServiceProvider.GetRequiredService<BucketRepository>();
        var minioService = scope.ServiceProvider.GetRequiredService<MinioService>();

        var allBucketsInDb = (await bucketRepository.AllAsync(f => true)).ToList();

        logger.LogInformation(
            $"{nameof(MakeUnavailableBucketsThatNotExistsInS3)}: Found {{count}} buckets in database",
            allBucketsInDb.Count);

        var allBucketsInS3 = (await minioService.ListBucketsAsync()).ToList();

        logger.LogInformation(
            $"{nameof(MakeUnavailableBucketsThatNotExistsInS3)}: Found {{count}} buckets in s3", allBucketsInS3.Count);

        var toSave = new List<Bucket>();

        foreach (var bucketInDb in allBucketsInDb)
        {
            var isAvailable = allBucketsInS3.Exists(f => f.Name.Equals(bucketInDb.Name));

            if (bucketInDb.IsAvailable == isAvailable) continue;

            logger.LogInformation(
                $"{nameof(RemoveBucketsThatNotExistsInDb)}: Bucket {{bucketName}} availability will be set to {{}}",
                bucketInDb.Name, isAvailable);

            bucketInDb.IsAvailable = isAvailable;

            toSave.Add(bucketInDb);
        }

        if (toSave.Count != 0) bucketRepository.UpdateAll(toSave);

        logger.LogInformation($"{nameof(MakeUnavailableBucketsThatNotExistsInS3)}: Total update count {{updated}}",
            await bucketRepository.SaveChangesAsync());

        logger.LogInformation($"Finished {nameof(MakeUnavailableBucketsThatNotExistsInS3)}");
    }

    private async Task RemoveFilesThatNotExistsInDb(CancellationToken stoppingToken = default)
    {
        logger.LogInformation($"Start {nameof(RemoveFilesThatNotExistsInDb)}");
        //TODO: run on SettingsEntity AND log
        using var scope = services.CreateScope();
        var fileRepository = scope.ServiceProvider.GetRequiredService<FileRepository>();
        var minioService = scope.ServiceProvider.GetRequiredService<MinioService>();

        var allFilesInDb = (await fileRepository.AllAsync(f => true, f => f.Bucket)).ToList();
        var allBucketsInS3 = (await minioService.ListBucketsAsync()).ToList();
        var allFilesInS3 =
            allBucketsInS3.ToDictionary(f => f.Name, async f => await minioService.ListObjectsAsync(f.Name));

        await Task.WhenAll(allFilesInS3.Values);

        var objToRemove = new Dictionary<string, List<string>>();

        foreach (var (bucketName, filesInS3) in allFilesInS3)
        foreach (var fileInS3 in filesInS3.Result)
            if (allFilesInDb.FirstOrDefault(f => f.Bucket.Name == bucketName && f.Name == fileInS3.Key) == null)
                objToRemove.AddOrAppend(bucketName, fileInS3.Key);

        var tasks = new List<Task>();

        foreach (var (bucketName, objectNames) in objToRemove)
            tasks.Add(minioService.RemoveObjectsAsync(bucketName, objectNames));

        await Task.WhenAll(tasks);
        logger.LogInformation($"Finished {nameof(RemoveFilesThatNotExistsInDb)}");
    }

    private async Task UpdateBucketData(CancellationToken stoppingToken = default)
    {
        logger.LogInformation($"Start {nameof(UpdateBucketData)}");

        using var scope = services.CreateScope();
        var bucketRepository = scope.ServiceProvider.GetRequiredService<BucketRepository>();
        var minioService = scope.ServiceProvider.GetRequiredService<MinioService>();

        var allBucketsInDb = (await bucketRepository.AllAsync(f => true)).ToList();

        var toSave = new List<Bucket>();

        foreach (var bucketInDb in allBucketsInDb)
        {
            var size = (await minioService.ListObjectsAsync(bucketInDb.Name)).ToList()
                .Aggregate<Item, ulong>(0, (current, s) => current + s.Size);

            if (size == bucketInDb.Size) continue;

            bucketInDb.Size = size;
            toSave.Add(bucketInDb);
        }

        if (toSave.Count != 0) bucketRepository.UpdateAll(toSave);

        logger.LogInformation($"{nameof(UpdateBucketData)}: Total update count {{updated}}",
            await bucketRepository.SaveChangesAsync());

        logger.LogInformation($"Finished {nameof(UpdateBucketData)}");
    }

    private async Task UpdateFilesData(CancellationToken stoppingToken = default)
    {
        logger.LogInformation($"Start {nameof(UpdateFilesData)}");
        //TODO: run on SettingsEntity AND log
        using var scope = services.CreateScope();
        var fileRepository = scope.ServiceProvider.GetRequiredService<FileRepository>();
        var minioService = scope.ServiceProvider.GetRequiredService<MinioService>();

        var allFilesInDb = (await fileRepository.AllAsync(f => true, f => f.Bucket)).ToList();
        var allBucketsInS3 = (await minioService.ListBucketsAsync()).ToList();
        Dictionary<string, Task<List<Item>>> allFilesInS3 =
            allBucketsInS3.ToDictionary(f => f.Name, async f => (await minioService.ListObjectsAsync(f.Name)).ToList());

        await Task.WhenAll(allFilesInS3.Values);

        var toSave = new List<File>();

        foreach (var fileInDb in allFilesInDb)
        {
            var isChanged = false;

            if (allFilesInS3.TryGetValue(fileInDb.Bucket.Name, out var filesInS3))
            {
                var fileInS3 = filesInS3.Result.FirstOrDefault(f => f.Key.Equals(fileInDb.Name));

                if (fileInS3 != null)
                {
                    if (fileInS3.Key.TryGetFileType(out var fileType))
                    {
                        if (!fileInDb.IsAvailable ||
                            fileInDb.Size != fileInS3.Size ||
                            fileInDb.Type != fileType)
                            isChanged = true;

                        fileInDb.IsAvailable = true;
                        fileInDb.Size = fileInS3.Size;
                        fileInDb.Type = fileType;
                    }
                    else
                    {
                        if (fileInDb.IsAvailable)
                            isChanged = true;

                        fileInDb.IsAvailable = false;
                    }
                }
                else
                {
                    if (fileInDb.IsAvailable)
                        isChanged = true;

                    fileInDb.IsAvailable = false;
                }
            }
            else
            {
                if (fileInDb.IsAvailable)
                    isChanged = true;

                fileInDb.IsAvailable = false;
            }

            if (isChanged) toSave.Add(fileInDb);
        }

        if (toSave.Count != 0) fileRepository.UpdateAll(toSave);

        logger.LogInformation($"{nameof(UpdateFilesData)}: Total update count {{updated}}",
            await fileRepository.SaveChangesAsync());
        logger.LogInformation($"Finished {nameof(UpdateFilesData)}");
    }
}
