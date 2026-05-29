using WebReader.Models;

namespace WebReader.Helpers;

public static class GlobalFunctions
{
    public static string FormatSize(ulong size)
    {
        if (size == 0)
            return "0B";

        var digits = size.ToString().Length;

        return digits switch
        {
            > 7 => size / 1024 / 1024 + "MB",
            > 4 => size / 1024 + "KB",
            _ => size + "B"
        };
    }

    public static string FormatSize(long size)
    {
        return FormatSize((ulong)size);
    }

    public static IEnumerable<TaskType> GetS3Types()
    {
        return
        [
            TaskType.RemoveBucketsThatNotExistsInDb,
            TaskType.MakeUnavailableBucketsThatNotExistsInS3,
            TaskType.RemoveFilesThatNotExistsInDb,
            TaskType.UpdateBucketData,
            TaskType.UpdateFilesData
        ];
    }

    public static IEnumerable<TaskType> GetDownloadTypes()
    {
        return
        [
            TaskType.AutoDownloadNewPartsOmniscientReader,
            TaskType.AutoDownloadNewPartsSoloLeveling,
            TaskType.AutoDownloadNewPartsWorldAfterDestruction
        ];
    }

    public static IEnumerable<TaskType> GetDeleteTypes()
    {
        return
        [
            TaskType.DeleteOldCompletedTasks,
            TaskType.DeleteOldErroredTasks,
            TaskType.DeleteOldInProgressTasks
        ];
    }
}
