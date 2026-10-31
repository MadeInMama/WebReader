namespace WebReader.Models;

public enum TaskType
{
    RemoveBucketsThatNotExistsInDb,
    MakeUnavailableBucketsThatNotExistsInS3,
    RemoveFilesThatNotExistsInDb,
    RemoveTtsForFilesThatNotExistsInDb,
    UpdateBucketData,
    UpdateFilesData,

    AutoDownloadNewPartsOmniscientReader,
    AutoDownloadNewPartsSoloLeveling,
    AutoDownloadNewPartsWorldAfterDestruction,

    DeleteOldCompletedTasks,
    DeleteOldErroredTasks,
    DeleteOldInProgressTasks,
    DeleteOldRequestAttemptsLogs,

    CalcAllBooksSizes,

    ClearUserTgLinks,

    SendNotifications

    //TODO: delete in s3 tasks
}
