using Minio.DataModel;
using WebReader.Models;
using WebReader.Models.Entities;
using WebReader.Repositories;
using WebReader.Services;
using File = WebReader.Models.Entities.File;

namespace WebReader.Background.SyncDbWithS3;

public class UpdateFilesData(IServiceProvider services, ILogger<UpdateFilesData> logger) : IBackgroundTasked
{
    public async Task ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken)
    {
        logger.LogInformation($"Start {nameof(UpdateFilesData)}");

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
