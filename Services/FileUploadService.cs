using WebReader.Models;
using WebReader.Models.Entities;
using WebReader.Repositories;

namespace WebReader.Services;

using File = Models.Entities.File;

public class FileUploadService(
    FileRepository fileRepository,
    MinioService minioService)
{
    public async Task<(bool isSuccessfull, string? errorMsg, File? currentFile)> UploadFileAsync(
        Guid? asPartOfId,
        Guid? asParentOfId,
        Bucket bucket,
        List<RoleType> userRoles,
        Stream fileStream,
        string fileName,
        string? fileCustomName,
        string fileContentType,
        FileType fileType,
        string? filePartName)
    {
        File? asPartOfFile = null, asParentOfFile = null;

        if (asPartOfId.HasValue)
        {
            asPartOfFile = await fileRepository.FirstOrDefaultAsync(f =>
                f.BucketId == bucket.Id && !f.IsHidden && f.Id == asPartOfId.Value &&
                f.AccessRoles.Intersect(userRoles).Any(), null);

            if (asPartOfFile == null) return (false, "Part of file not available.", null);
        }

        if (asParentOfId.HasValue)
        {
            asParentOfFile = await fileRepository.FirstOrDefaultAsync(f =>
                f.BucketId == bucket.Id && !f.IsHidden && f.Id == asParentOfId.Value &&
                f.AccessRoles.Intersect(userRoles).Any(), null);

            if (asParentOfFile == null) return (false, "Parent of file not available.", null);
        }

        var uploadToS3Successful =
            await minioService.UploadObjectAsync(bucket.Name, fileStream, fileName, fileContentType);

        if (!uploadToS3Successful)
            return (false, "File upload failed. Try again later. Storage is not accessible now.", null);

        //TODO: part-parent check and set

        var currentFile = new File
        {
            BucketId = bucket.Id,
            Name = fileName,
            CustomName = fileCustomName?.Trim(),
            Type = fileType,
            IsAvailable = true,
            IsHidden = false,
            Size = (ulong?)fileStream.Length,
            NextPartId = asParentOfFile?.Id,
            CurrentPartName = filePartName
        };

        if (asPartOfFile != null)
        {
            asPartOfFile.NextPartId = currentFile.Id;
            asPartOfFile.NextPart = currentFile;

            fileRepository.Update(asPartOfFile);
        }
        else
        {
            await fileRepository.AddAsync(currentFile);
        }

        try
        {
            await fileRepository.SaveChangesAsync();
        }
        catch (Exception _)
        {
            await minioService.RemoveObjectsAsync(bucket.Name, [currentFile.Name]);

            return (false, "File save failed. Try again later.", null);
        }

        return (true, null, currentFile);
    }
}
