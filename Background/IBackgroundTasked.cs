using WebReader.Models.Entities;

namespace WebReader.Background;

public interface IBackgroundTasked
{
    Task ExecuteAsync(ScheduledTask task, CancellationToken cancellationToken);
}
