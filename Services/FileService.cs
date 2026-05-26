using WebReader.Repositories;

namespace WebReader.Services;

public class FileService(
    FileRepository fileRepository,
    UserReadingRepository readingRepository,
    MinioService minioService,
    ILogger<FileService> logger)
{
    public async Task DeleteFileAsync(List<Guid> guids, CancellationToken cancellationToken)
    {
        logger.LogInformation("Deleting files with ids: ({count}, {guids})", guids.Count, string.Join(", ", guids));

        foreach (var guid in guids.Index())
        {
            logger.LogTrace("Deleting file with id: ({index}, {guid})", guid.Index + 1, guid.Item);

            var file = await fileRepository.FirstOrDefaultAsync(f => f.Id == guid.Item, null, cancellationToken, false,
                f => f.Bucket);

            if (file == null) continue;

            var prevFile =
                await fileRepository.FirstOrDefaultAsync(f => f.NextPartId == guid.Item, null, cancellationToken,
                    false);

            if (prevFile != null)
            {
                if (file.NextPartId.HasValue)
                    prevFile.NextPartId = file.NextPartId.Value;
                else
                    prevFile.NextPartId = null;
            }

            await fileRepository.SaveChangesAsync(cancellationToken);

            await fileRepository.DeleteAsync(guid.Item, cancellationToken);
            await readingRepository.DeleteAllByFileIdAsync([guid.Item], cancellationToken);
            await minioService.RemoveObjectsAsync(file.Bucket!.Name, [file.Name], cancellationToken);
        }

        logger.LogInformation("Deleting files done");
    }
}
