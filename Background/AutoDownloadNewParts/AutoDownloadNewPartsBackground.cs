using WebReader.Repositories;
using WebReader.Services;

namespace WebReader.Background.AutoDownloadNewParts;

public class AutoDownloadNewPartsBackground(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<AutoDownloadNewPartsBackground> logger) : BackgroundService
{
    private static readonly TimeSpan PeriodTime = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        await PerformAutoDownloadNewParts(cancellationToken);

        using PeriodicTimer timer = new(PeriodTime);

        while (await timer.WaitForNextTickAsync(cancellationToken))
            await PerformAutoDownloadNewParts(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
    }

    private async Task PerformAutoDownloadNewParts(CancellationToken cancellationToken = default)
    {
        logger.LogInformation($"Start {nameof(PerformAutoDownloadNewParts)}");

        using var scope = serviceScopeFactory.CreateScope();

        var files = await scope.ServiceProvider.GetRequiredService<FileRepository>()
            .AllAsync(f => f.CurrentPartNumber > 271);
        await scope.ServiceProvider.GetRequiredService<FileService>().DeleteFileAsync(files.Select(f => f.Id).ToList());

        foreach (var el in scope.ServiceProvider.GetRequiredService<IEnumerable<IAutoDownloadNewParts>>())
            await el.GetAndDownload(cancellationToken);

        logger.LogInformation($"Finished {nameof(PerformAutoDownloadNewParts)}");
    }
}
