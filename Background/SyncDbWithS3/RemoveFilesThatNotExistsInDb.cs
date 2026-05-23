using WebReader.Helpers;
using WebReader.Models.Entities;
using WebReader.Repositories;
using WebReader.Services;

namespace WebReader.Background.SyncDbWithS3;

public class RemoveFilesThatNotExistsInDb(IServiceProvider services, ILogger<RemoveFilesThatNotExistsInDb> logger)
    : IBackgroundTasked
{
    public async Task ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken)
    {
        logger.LogInformation($"Start {nameof(RemoveFilesThatNotExistsInDb)}");

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
}
