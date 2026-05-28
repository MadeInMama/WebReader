using FluentResults;
using WebReader.Models;
using WebReader.Models.Entities;
using WebReader.Repositories;

namespace WebReader.Services;

using File = Models.Entities.File;

public class FileUploadService(
    FileRepository fileRepository,
    MinioService minioService)
{
    public async Task<Result<File>> UploadFileAsync(
        Guid? asPartOfId,
        Guid? asParentOfId,
        Bucket bucket,
        List<RoleType> userRoles,
        Stream fileStream,
        string fileName,
        string? fileCustomName,
        string fileContentType,
        FileType fileType,
        string? filePartName,
        uint? filePartNumber,
        CancellationToken cancellationToken)
    {
        File? asPartOfFile = null, asParentOfFile = null;

        if (asPartOfId.HasValue)
        {
            asPartOfFile = await fileRepository.FirstOrDefaultAsync(f =>
                f.BucketId == bucket.Id && !f.IsHidden && f.Id == asPartOfId.Value &&
                f.AccessRoles.Intersect(userRoles).Any(), null, cancellationToken, false);

            if (asPartOfFile == null) return Result.Fail(new Error("Part of file not available."));
        }

        if (asParentOfId.HasValue)
        {
            asParentOfFile = await fileRepository.FirstOrDefaultAsync(f =>
                f.BucketId == bucket.Id && !f.IsHidden && f.Id == asParentOfId.Value &&
                f.AccessRoles.Intersect(userRoles).Any(), null, cancellationToken, false);

            if (asParentOfFile == null) return Result.Fail(new Error("Parent of file not available."));
        }

        var fileStreamLength = (ulong?)fileStream.Length;

        var uploadToS3Successful =
            await minioService.UploadObjectAsync(bucket.Name, fileStream, fileName, fileContentType, cancellationToken);

        if (!uploadToS3Successful)
            return Result.Fail(new Error("File upload failed. Try again later. Storage is not accessible now."));

        //TODO: part-parent check and set

        var currentFile = new File
        {
            BucketId = bucket.Id,
            Name = fileName,
            CustomName = fileCustomName?.Trim(),
            Type = fileType,
            IsAvailable = true,
            IsHidden = false,
            Size = fileStreamLength,
            NextPartId = asParentOfFile?.Id,
            CurrentPartName = filePartName,
            CurrentPartNumber = filePartNumber
        };

        if (asPartOfFile != null)
        {
            asPartOfFile.NextPartId = currentFile.Id;
            asPartOfFile.NextPart = currentFile;

            fileRepository.Attach(asPartOfFile);
        }
        else
        {
            await fileRepository.AddAsync(currentFile, cancellationToken);
        }

        try
        {
            await fileRepository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Fail(new Error("File save failed. Try again later.").CausedBy(ex));
        }

        return Result.Ok(currentFile);
    }
}
