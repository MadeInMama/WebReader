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
    AutoDownloadNewPartsSoloLevelingHunterOrigin,
    AutoDownloadNewPartsSoloLevelingRagnarok,
    AutoDownloadNewPartsWorldAfterDestruction,

    DeleteOldCompletedTasks,
    DeleteOldErroredTasks,
    DeleteOldInProgressTasks,
    DeleteOldRequestAttemptsLogs,
    DeleteSentNotifications,

    CalcAllBooksSizes,

    ClearUserTgLinks,

    SendNotifications
}
