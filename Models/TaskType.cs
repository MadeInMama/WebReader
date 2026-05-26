namespace WebReader.Models;

public enum TaskType
{
    RemoveBucketsThatNotExistsInDb,
    MakeUnavailableBucketsThatNotExistsInS3,
    RemoveFilesThatNotExistsInDb,
    UpdateBucketData,
    UpdateFilesData,

    AutoDownloadNewPartsOmniscientReader,
    AutoDownloadNewPartsSoloLeveling,
    AutoDownloadNewPartsWorldAfterDestruction,

    DeleteOldCompletedTasks,
    DeleteOldErroredTasks,
    DeleteOldInProgressTasks

    //TODO: delete in s3 tasks
}
