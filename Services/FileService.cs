using WebReader.Repositories;

namespace WebReader.Services;

public class FileService(
    FileRepository fileRepository,
    UserReadingRepository readingRepository,
    MinioService minioService)
{
    public async Task DeleteFileAsync(List<Guid> guids)
    {
        foreach (var guid in guids)
        {
            var file = await fileRepository.FirstOrDefaultAsync(f => f.Id == guid, null, f => f.Bucket);

            if (file == null) continue;

            var prevFile = await fileRepository.FirstOrDefaultAsync(f => f.NextPartId == guid, null);

            if (prevFile != null)
            {
                if (file.NextPartId.HasValue)
                    prevFile.NextPartId = file.NextPartId.Value;
                else
                    prevFile.NextPartId = null;
            }

            await fileRepository.SaveChangesAsync();

            await fileRepository.DeleteAsync(guid);
            await readingRepository.DeleteAllByFileIdAsync([guid]);
            await minioService.RemoveObjectsAsync(file.Bucket!.Name, [file.Name]);
        }
    }
}
