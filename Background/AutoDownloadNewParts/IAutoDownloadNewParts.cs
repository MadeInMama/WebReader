namespace WebReader.Background.AutoDownloadNewParts;

public interface IAutoDownloadNewParts
{
    Task GetAndDownload(CancellationToken stoppingToken);
}
