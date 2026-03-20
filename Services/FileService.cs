using WebReader.Repositories;

namespace WebReader.Services;

public class FileService(
    FileRepository fileRepository,
    UserReadingRepository readingRepository,
    MinioService minioService,
    ILogger<FileService> logger)
{
    public async Task DeleteFileAsync(List<Guid> guids)
    {
        logger.LogInformation("Deleting files with ids: ({count}, {guids})", guids.Count, string.Join(", ", guids));

        foreach (var guid in guids.Index())
        {
            logger.LogInformation("Deleting file with id: {guid}", guid.ToString());

            var file = await fileRepository.FirstOrDefaultAsync(f => f.Id == guid.Item, null, f => f.Bucket);

            if (file == null) continue;

            var prevFile = await fileRepository.FirstOrDefaultAsync(f => f.NextPartId == guid.Item, null);

            if (prevFile != null)
            {
                if (file.NextPartId.HasValue)
                    prevFile.NextPartId = file.NextPartId.Value;
                else
                    prevFile.NextPartId = null;
            }

            await fileRepository.SaveChangesAsync();

            await fileRepository.DeleteAsync(guid.Item);
            await readingRepository.DeleteAllByFileIdAsync([guid.Item]);
            await minioService.RemoveObjectsAsync(file.Bucket!.Name, [file.Name]);
        }

        logger.LogInformation("Deleting files done");
    }
}
