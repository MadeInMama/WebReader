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
    DeleteOldErroredTasks

    //TODO: delete in s3 tasks
}
