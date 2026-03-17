using WebReader.Repositories;

namespace WebReader.Services;

public class FileService(
    FileRepository fileRepository,
    MinioService minioService)
{
    public async Task DeleteFileAsync(List<Guid> guids)
    {
        foreach (var guid in guids)
        {
            var file = await fileRepository.FirstOrDefaultAsync(f => f.Id == guid, null, f => f.Bucket);

            if (file == null) continue;

            await fileRepository.DeleteAsync(guid);
            await minioService.RemoveObjectsAsync(file.Bucket!.Name, [file.Name]);
        }
    }
}
