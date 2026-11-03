using WebReader.Models;

namespace WebReader.Helpers;

public static class GlobalFunctions
{
    private static readonly string[] SizeSuffixes = { "Bytes", "KB", "MB", "GB", "TB", "PB", "EB" };

    public static string FormatSize(ulong bytes, int decimalPlaces = 2)
    {
        if (bytes == 0) return string.Format("{0:F" + decimalPlaces + "} Bytes", 0);

        var mag = (int)Math.Log(bytes, 1024);

        if (mag >= SizeSuffixes.Length) mag = SizeSuffixes.Length - 1;

        var adjustedSize = bytes / Math.Pow(1024, mag);

        if (Math.Round(adjustedSize, decimalPlaces) >= 1024 && mag < SizeSuffixes.Length - 1)
        {
            mag++;
            adjustedSize /= 1024;
        }

        return string.Format("{0:F" + decimalPlaces + "} {1}", adjustedSize, SizeSuffixes[mag]);
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
            TaskType.RemoveTtsForFilesThatNotExistsInDb,
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
            TaskType.DeleteOldInProgressTasks,
            TaskType.DeleteOldRequestAttemptsLogs,
            TaskType.DeleteSentNotifications,
            TaskType.ClearUserTgLinks
        ];
    }

    public static IEnumerable<TaskType> GetOtherTypes()
    {
        return
        [
            TaskType.CalcAllBooksSizes,
            TaskType.SendNotifications
        ];
    }
}
