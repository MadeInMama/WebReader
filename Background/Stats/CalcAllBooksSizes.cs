using System.Text.Encodings.Web;
using System.Text.Json;
using FluentResults;
using Microsoft.AspNetCore.SignalR;
using WebReader.Helpers;
using WebReader.Models;
using WebReader.Models.Entities;
using WebReader.Models.Extended;
using WebReader.Models.Signal;
using WebReader.Repositories;
using WebReader.Services;
using File = WebReader.Models.Entities.File;
using TaskStatus = WebReader.Models.TaskStatus;

namespace WebReader.Background.Stats;

public class CalcAllBooksSizes(
    BucketRepository bucketRepository,
    FileRepository fileRepository,
    ScheduledTaskRepository taskRepository,
    CustomUserRepository userRepository,
    MinioService minioService,
    IHubContext<ScheduledTaskHub> scheduledTaskHubContext,
    ILogger<CalcAllBooksSizes> logger)
    : AbstractBackgroundTasked<CalcAllBooksSizes>(taskRepository, scheduledTaskHubContext, logger)
{
    private static readonly JsonSerializerOptions JsonSerializerOptions =
        new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public override async Task<Result<string>> ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken)
    {
        var allUsersInDb = await userRepository.AllAsync(f => true, cancellationToken, true);

        var allBucketsInDb = new List<Bucket>();
        var allTopLevelFilesInDb = new List<ExtendedFile>();
        foreach (var userInDb in allUsersInDb)
        {
            var buckets = await bucketRepository.GetAllAvailableBucketsAsync(Enum.GetValues<RoleType>(), userInDb.Id,
                cancellationToken);
            var files = await fileRepository.GetAllAvailableObjectsTopLevelAsync(
                userInDb.Id, Enum.GetValues<RoleType>(), cancellationToken);

            foreach (var bucket in buckets)
                if (allBucketsInDb.FirstOrDefault(f => f.Id == bucket.Id) == null)
                    allBucketsInDb.Add(bucket);

            foreach (var file in files)
                if (allTopLevelFilesInDb.FirstOrDefault(f => f.Id == file.Id) == null)
                    allTopLevelFilesInDb.Add(file);
        }

        await UpdateProgress(task.Id, TaskStatus.InProgress, 0.05m, null, cancellationToken);

        var allFilesByTitleInDb = new Dictionary<string, List<File>>();
        foreach (var file in allTopLevelFilesInDb)
            allFilesByTitleInDb.AddOrAppend(file.CustomName ?? file.Name,
                [.. await fileRepository.GetAllAvailableObjectsWithPartsAsync(file.Id, cancellationToken)]);

        await UpdateProgress(task.Id, TaskStatus.InProgress, 0.1m, null, cancellationToken);

        var resBooks = new List<(string Name, int Count, ulong Size)>();

        var totalCount = allFilesByTitleInDb.Values.Sum(list => list.Count);
        var currentCount = 0;

        foreach (var el in allFilesByTitleInDb.Index())
        {
            var count = el.Item.Value.Count;
            var size = 0ul;

            foreach (var file in el.Item.Value)
            {
                var fileStats = await minioService.StatObjectAsync(
                    allBucketsInDb.First(f => f.Id == file.BucketId).Name,
                    file.Name, cancellationToken);
                size += (ulong)fileStats.Size;

                if (file.CoverName != null)
                {
                    var coverStats = await minioService.StatObjectAsync("covers", file.CoverName, cancellationToken);
                    size += (ulong)coverStats.Size;
                }

                if (file.Type == FileType.Fb2)
                {
                    var ttsStats =
                        await minioService.GetFolderStatsAsync("tts", file.Name.Split(".")[0], cancellationToken);
                    size += ttsStats.Size;
                }

                await UpdateProgress(task.Id, TaskStatus.InProgress, 0.1m + 0.89m * ++currentCount / totalCount, null,
                    cancellationToken);
            }

            resBooks.Add((el.Item.Key, count, size));
        }

        await UpdateProgress(task.Id, TaskStatus.InProgress, 0.99m, null, cancellationToken);

        return Result.Ok(JsonSerializer.Serialize(new
        {
            Books = resBooks.Select(f => new { f.Name, f.Count, Size = GlobalFunctions.FormatSize(f.Size) }),
            TotalCount = resBooks.Sum(f => f.Count),
            TotalSize = GlobalFunctions.FormatSize(resBooks
                .Aggregate<(string Name, int Count, ulong Size), ulong>(0, (current, s) => current + s.Size))
        }, JsonSerializerOptions));
    }
}
