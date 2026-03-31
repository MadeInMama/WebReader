namespace WebReader.Background.AutoDownloadNewParts;

public class AutoDownloadNewPartsBackground(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<AutoDownloadNewPartsBackground> logger) : BackgroundService
{
    private static readonly TimeSpan PeriodTime = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await PerformAutoDownloadNewParts(stoppingToken);

        using PeriodicTimer timer = new(PeriodTime);

        while (await timer.WaitForNextTickAsync(stoppingToken)) await PerformAutoDownloadNewParts(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        await base.StopAsync(stoppingToken);
    }

    private async Task PerformAutoDownloadNewParts(CancellationToken stoppingToken = default)
    {
        logger.LogInformation($"Start {nameof(PerformAutoDownloadNewParts)}");

        using var scope = serviceScopeFactory.CreateScope();

        // var files = await scope.ServiceProvider.GetRequiredService<FileRepository>().AllAsync(f =>
        //     f.CustomName == "Всеведущий читатель" && f.CurrentPartNumber > 77);
        // await scope.ServiceProvider.GetRequiredService<FileService>().DeleteFileAsync(files.Select(f => f.Id).ToList());

        foreach (var el in scope.ServiceProvider.GetRequiredService<IEnumerable<IAutoDownloadNewParts>>())
            await el.GetAndDownload(stoppingToken);

        logger.LogInformation($"Finished {nameof(PerformAutoDownloadNewParts)}");
    }
}
