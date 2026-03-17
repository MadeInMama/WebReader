namespace WebReader.Background.AutoDownloadNewParts;

public class AutoDownloadNewPartsBackground(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<AutoDownloadNewPartsBackground> logger) : BackgroundService
{
    private static readonly TimeSpan PeriodTime = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Perform(stoppingToken);

        using PeriodicTimer timer = new(PeriodTime);

        while (await timer.WaitForNextTickAsync(stoppingToken)) await Perform(stoppingToken);
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        await base.StopAsync(stoppingToken);
    }

    private async Task Perform(CancellationToken stoppingToken = default)
    {
        logger.LogInformation($"Start {nameof(Perform)}");

        using var scope = serviceScopeFactory.CreateScope();

        foreach (var el in scope.ServiceProvider.GetRequiredService<IEnumerable<IAutoDownloadNewParts>>())
            await el.GetAndDownload(stoppingToken);

        logger.LogInformation($"Finished {nameof(Perform)}");
    }
}
