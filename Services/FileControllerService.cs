using FluentResults;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models;
using WebReader.Models.Dtos;
using WebReader.Models.Dtos.Item;
using WebReader.Models.Entities;
using WebReader.Repositories;

namespace WebReader.Services;

public class FileControllerService(
    BucketRepository bucketRepository,
    FileRepository fileRepository,
    UserReadingRepository readingRepository,
    IDbContextFactory<ApplicationDbContext> contextFactory)
{
    public async Task<Result<AllBucketsViewModel>> GetAllBuckets(Guid userGuid, List<RoleType> roles)
    {
        var res = (await bucketRepository.GetAllAvailableBucketsAsync(roles, userGuid))
            .Select(bucket =>
                new AllBucketsItem
                {
                    Id = bucket.Id,
                    CustomName = bucket.CustomName ?? bucket.Name,
                    DateTime = bucket.CreatedDate,
                    Size = bucket.Size ?? 0
                });

        return new AllBucketsViewModel { Items = res };
    }

    public async Task<Result<AllFilesInBucketViewModel>> GetAllFilesInBucket(Guid userGuid, List<RoleType> roles,
        Guid bucketId, string orderBy)
    {
        var prop = typeof(AllFilesInBucketItem).GetProperty(orderBy);

        var bucket = await bucketRepository
            .FirstOrDefaultAsync(f => f.IsAvailable &&
                                      !f.IsHidden &&
                                      f.Id == bucketId &&
                                      f.AccessRoles.Intersect(roles).Any() &&
                                      (f.UserId == userGuid || f.UserId == null), null, true);

        if (bucket == null) return Result.Fail("Bucket not found");

        var allUserReadings = await readingRepository.AllAsync(f => f.UserId == userGuid);

        var res = (await fileRepository.GetAllAvailableObjectsInBucketTopLevelAsync(bucket.Name, roles))
            .Select(file =>
            {
                var reading = allUserReadings.FirstOrDefault(reading => reading.FileId == file.Id);

                return new AllFilesInBucketItem
                {
                    Id = file.Id,
                    FileName = file.CustomName ?? file.Name,
                    DateTime = file.UpdatedDate,
                    Size = file.Size ?? 0,
                    Type = TypeHelper.FileTypeNameDict[file.Type],
                    IsReading = reading != null,
                    IsParted = file.NextPartId.HasValue,
                    IsDone = reading?.IsDone ?? false
                };
            }).OrderBy(f => prop?.GetValue(f, null) ?? f.FileName);

        return new AllFilesInBucketViewModel
        {
            Id = bucket.Id,
            BucketName = bucket.CustomName ?? bucket.Name,
            IsBelongsToUser = bucket.UserId == userGuid,
            Items = res
        };
    }

    public async Task<Result<AllFilesInBucketViewModel>> GetAllPartsInFile(Guid userGuid, List<RoleType> roles,
        Guid bucketId, Guid fileId, string orderBy)
    {
        var prop = typeof(AllFilesInBucketItem).GetProperty(orderBy);

        using var contextBucket = contextFactory.CreateDbContextAsync();
        using var contextFile = contextFactory.CreateDbContextAsync();

        Task.WaitAll(contextBucket, contextFile);

        var bucket = bucketRepository.FirstOrDefaultAsync(f => f.IsAvailable &&
                                                               !f.IsHidden &&
                                                               f.Id == bucketId &&
                                                               f.AccessRoles.Intersect(roles)
                                                                   .Any() &&
                                                               (f.UserId == userGuid || f.UserId == null),
            contextBucket.Result, true);

        var file = fileRepository.FirstOrDefaultAsync(f => f.IsAvailable &&
                                                           !f.IsHidden &&
                                                           f.Id == fileId &&
                                                           f.AccessRoles.Intersect(roles).Any(),
            contextFile.Result, true);

        Task.WaitAll(bucket, file);

        if (bucket.Result == null || file.Result == null)
            return Result.Fail("Bucket or File not found");

        var allUserReadings = await readingRepository.AllAsync(f => f.UserId == userGuid);

        var headedFile = await fileRepository.GetHeadedPartedObjectAsync(fileId);

        var files = await fileRepository.GetAllAvailableObjectsWithPartsAsync(headedFile.Id);

        var res = files.Select(f =>
        {
            var reading = allUserReadings.FirstOrDefault(reading => reading.FileId == f.Id);

            return new AllFilesInBucketItem
            {
                Id = f.Id,
                FileName = f.CustomName ?? f.Name,
                DateTime = f.UpdatedDate,
                Size = f.Size ?? 0,
                Type = TypeHelper.FileTypeNameDict[f.Type],
                IsReading = reading != null,
                IsParted = f.NextPartId.HasValue,
                IsDone = reading?.IsDone ?? false,
                CurrentPartName = f.CurrentPartName
            };
        }).OrderBy(f => prop?.GetValue(f, null) ?? f.FileName);

        return new AllFilesInBucketViewModel
        {
            Id = bucket.Result.Id,
            BucketName = bucket.Result.CustomName ?? bucket.Result.Name,
            IsBelongsToUser = bucket.Result.UserId == userGuid,
            Items = res
        };
    }

    public async Task<Result<AllFilesReadingViewModel>> GetReading(Guid userGuid, List<RoleType> roles)
    {
        var allUserReadings = await readingRepository.AllAsync(f => f.UserId == userGuid);

        var allUserReadingsGroupedByFile = allUserReadings
            .GroupBy(reading => reading.FileId)
            .ToDictionary(reading => reading.Key, reading => reading.ToList());

        var idsToDelete = new List<Guid>();
        var readingsToUpdate = new List<UserReading>();

        foreach (var readingGroup in allUserReadingsGroupedByFile.Where(f => f.Value.Count > 1))
        {
            var readingsOrdered = readingGroup.Value.OrderBy(f => f.CreatedDate).ToList();
            var picked = readingsOrdered.First();

            picked.Page = readingGroup.Value.Max(reading => reading.Page);
            picked.Scale = readingGroup.Value.OrderByDescending(f => f.UpdatedDate).First().Scale;

            readingsToUpdate.Add(picked);
            idsToDelete.AddRange(readingsOrdered.Skip(1).Select(reading => reading.Id));
        }

        var delete = idsToDelete.Count != 0
            ? readingRepository.DeleteAllAsync(idsToDelete)
            : Task.CompletedTask;

        var update = readingsToUpdate.Count != 0
            ? readingRepository.SetCurrPageAndScaleAndIsDoneAsync(readingsToUpdate)
            : Task.CompletedTask;

        await Task.WhenAll(delete, update);

        var readings = (await readingRepository.AllAsync(f => f.UserId == userGuid &&
                                                              !f.File!.IsHidden &&
                                                              !f.IsDone &&
                                                              f.File.AccessRoles.Intersect(roles).Any(),
            f => f.File!,
            f => f.File!.Bucket!)).ToList();

        var res = readings.GroupBy(reading => new AllFilesReadingItemKey
            {
                BucketId = reading.File!.BucketId,
                CustomName = reading.File?.Bucket?.CustomName ?? reading.File?.Bucket?.Name!
            })
            .ToDictionary(reading => reading.Key, reading => reading
                .OrderByDescending(f => f.UpdatedDate)
                .Select(r => new AllFilesReadingItem
                {
                    ReadingId = r.Id,
                    FileId = r.FileId,
                    CustomName = r.File?.CustomName ?? r.File?.Name ?? "",
                    CurrentPartName = r.File?.CurrentPartName,
                    DateTime = r.UpdatedDate,
                    Size = r.File?.Size ?? 0,
                    Page = r.Page
                }));

        return new AllFilesReadingViewModel { Items = res };
    }

    public async Task<Result<FileViewModel>> GetFile(Guid userGuid, List<RoleType> roles, Guid bucketId,
        Guid fileId)
    {
        var file = await fileRepository.FirstOrDefaultAsync(f =>
                f.BucketId == bucketId &&
                f.Id == fileId &&
                f.AccessRoles.Intersect(roles).Any(), null, true,
            f => f.Bucket!);

        if (file == null) return Result.Fail("File not found");

        var reading = await readingRepository
            .FirstOrDefaultAsync(f => f.UserId == userGuid
                                      && f.File!.BucketId == bucketId
                                      && f.FileId == fileId, null, true);

        return new FileViewModel
        {
            UserId = userGuid,
            FileId = file.Id,
            Page = reading?.Page ?? 1,
            Scale = reading?.Scale,
            Title = file.CustomName ?? file.Name,
            BucketId = file.BucketId,
            BucketName = file.Bucket?.CustomName ?? file.Bucket?.Name ?? "",
            FileName = file.CustomName ?? file.Name,
            CurrentPartName = file.CurrentPartName,
            NextPartId = file.NextPartId,
            PrevPartId = (await fileRepository.FirstOrDefaultAsync(f =>
                f.BucketId == bucketId &&
                f.NextPartId == file.Id &&
                f.AccessRoles.Intersect(roles).Any(), null, true))?.Id,
            Settings = file.Settings,
            Type = file.Type
        };
    }
}
