using WebReader.Models;
using WebReader.Models.Entities;
using WebReader.Repositories;
using TaskStatus = WebReader.Models.TaskStatus;

namespace WebReader.Background;

public class BackgroundTaskManager(
    ILogger<BackgroundTaskManager> logger,
    IServiceScopeFactory serviceScopeFactory)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Multi-Schedule Background Service starting");

        await Task.WhenAll(
            RunHourlyTask(cancellationToken),
            RunDailyTask(cancellationToken),
            RunWeeklyTask(cancellationToken),
            RunAsSoonAsPossible(cancellationToken)
        );
    }

    private async Task RunHourlyTask(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        using var scope = serviceScopeFactory.CreateScope();

        var taskConfigRepository = scope.ServiceProvider.GetRequiredService<ScheduledTaskConfigRepository>();
        var taskRepository = scope.ServiceProvider.GetRequiredService<ScheduledTaskRepository>();

        await DoWork();

        while (await timer.WaitForNextTickAsync(cancellationToken)) await DoWork();

        return;

        async Task DoWork()
        {
            var taskConfigs =
                await taskConfigRepository.AllAsync(f => f.Cron == TaskConfigCron.EveryHour && f.IsActive);

            var tasks = CreateTasksFromConfigs(taskConfigs);

            await taskRepository.AddRangeAsync(tasks, cancellationToken);

            await taskRepository.SaveChangesAsync();
        }
    }

    private async Task RunDailyTask(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1));

        using var scope = serviceScopeFactory.CreateScope();

        var taskConfigRepository = scope.ServiceProvider.GetRequiredService<ScheduledTaskConfigRepository>();
        var taskRepository = scope.ServiceProvider.GetRequiredService<ScheduledTaskRepository>();

        await DoWork();

        while (await timer.WaitForNextTickAsync(cancellationToken)) await DoWork();

        return;

        async Task DoWork()
        {
            var taskConfigs = await taskConfigRepository.AllAsync(f => f.Cron == TaskConfigCron.EveryDay && f.IsActive);

            var tasks = CreateTasksFromConfigs(taskConfigs);

            await taskRepository.AddRangeAsync(tasks, cancellationToken);

            await taskRepository.SaveChangesAsync();
        }
    }

    private async Task RunWeeklyTask(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromDays(7));

        using var scope = serviceScopeFactory.CreateScope();

        var taskConfigRepository = scope.ServiceProvider.GetRequiredService<ScheduledTaskConfigRepository>();
        var taskRepository = scope.ServiceProvider.GetRequiredService<ScheduledTaskRepository>();

        await DoWork();

        while (await timer.WaitForNextTickAsync(cancellationToken)) await DoWork();

        return;

        async Task DoWork()
        {
            var taskConfigs =
                await taskConfigRepository.AllAsync(f => f.Cron == TaskConfigCron.EveryWeek && f.IsActive);

            var tasks = CreateTasksFromConfigs(taskConfigs);

            await taskRepository.AddRangeAsync(tasks, cancellationToken);

            await taskRepository.SaveChangesAsync();
        }
    }

    private async Task RunAsSoonAsPossible(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        using var scope = serviceScopeFactory.CreateScope();

        var taskRepository = scope.ServiceProvider.GetRequiredService<ScheduledTaskRepository>();

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var task = await taskRepository.GetNextTaskAsync(cancellationToken);

            if (task == null) continue;

            task.Status = TaskStatus.InProgress;
            task.Progress = new decimal(0.0);

            await taskRepository.SaveChangesAsync();

            var taskExecutor = scope.ServiceProvider.GetKeyedService<IBackgroundTasked>(task.Type);

            if (taskExecutor == null)
            {
                logger.LogError("No TaskExecutorClass for type:{type}", task.Type);

                task.Status = TaskStatus.Error;
                task.ErrorMessage = $"No TaskExecutorClass for type:{task.Type}";
            }
            else
            {
                //TODO: set progress in executions
                //TODO: catch exceptions and set ErrorMessage
                //TODO: return FluentResult and set ErrorMessage on fail
                //TODO: set execution time from settings
                await taskExecutor.ExecuteAsync(task, cancellationToken);

                task.Status = TaskStatus.Completed;
                task.Progress = new decimal(1.0);
            }

            await taskRepository.SaveChangesAsync();
        }
    }

    private static IEnumerable<ScheduledTask> CreateTasksFromConfigs(IEnumerable<ScheduledTaskConfig> taskConfigs)
    {
        return taskConfigs.Select(f => new ScheduledTask
        {
            Type = f.Type,
            Priority = f.DefaultPriority,
            ScheduledTaskConfigId = f.Id,
            ScheduledTaskConfig = f
        });
    }
}
