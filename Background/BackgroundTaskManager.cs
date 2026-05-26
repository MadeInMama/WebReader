using Microsoft.AspNetCore.SignalR;
using WebReader.Models;
using WebReader.Models.Entities;
using WebReader.Models.Signal;
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
        await Task.WhenAll(
            RunHourlyTask(cancellationToken),
            RunDailyTask(cancellationToken),
            RunWeeklyTask(cancellationToken),
            RunMonthlyTask(cancellationToken),
            RunAsSoonAsPossible(cancellationToken)
        );
    }

    private async Task RunHourlyTask(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        using var scope = serviceScopeFactory.CreateScope();

        var taskConfigRepository = scope.ServiceProvider.GetRequiredService<ScheduledTaskConfigRepository>();
        var taskRepository = scope.ServiceProvider.GetRequiredService<ScheduledTaskRepository>();
        var scheduledTaskHubContext = scope.ServiceProvider.GetRequiredService<IHubContext<ScheduledTaskHub>>();

        await DoWork();

        while (await timer.WaitForNextTickAsync(cancellationToken)) await DoWork();

        return;

        async Task DoWork()
        {
            var taskConfigs =
                await taskConfigRepository.AllAsync(f => f.Cron == TaskConfigCron.EveryHour && f.IsActive,
                    cancellationToken);

            var tasks = CreateTasksFromConfigs(taskConfigs);

            await taskRepository.AddRangeAsync(tasks, cancellationToken);

            await taskRepository.SaveChangesAsync(cancellationToken);

            await scheduledTaskHubContext.Clients.All.SendAsync("ScheduledTaskHub", cancellationToken);
        }
    }

    private async Task RunDailyTask(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromDays(1));

        using var scope = serviceScopeFactory.CreateScope();

        var taskConfigRepository = scope.ServiceProvider.GetRequiredService<ScheduledTaskConfigRepository>();
        var taskRepository = scope.ServiceProvider.GetRequiredService<ScheduledTaskRepository>();
        var scheduledTaskHubContext = scope.ServiceProvider.GetRequiredService<IHubContext<ScheduledTaskHub>>();

        await DoWork();

        while (await timer.WaitForNextTickAsync(cancellationToken)) await DoWork();

        return;

        async Task DoWork()
        {
            var taskConfigs = await taskConfigRepository.AllAsync(f => f.Cron == TaskConfigCron.EveryDay && f.IsActive,
                cancellationToken);

            var tasks = CreateTasksFromConfigs(taskConfigs);

            await taskRepository.AddRangeAsync(tasks, cancellationToken);

            await taskRepository.SaveChangesAsync(cancellationToken);

            await scheduledTaskHubContext.Clients.All.SendAsync("ScheduledTaskHub", cancellationToken);
        }
    }

    private async Task RunWeeklyTask(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromDays(7));

        using var scope = serviceScopeFactory.CreateScope();

        var taskConfigRepository = scope.ServiceProvider.GetRequiredService<ScheduledTaskConfigRepository>();
        var taskRepository = scope.ServiceProvider.GetRequiredService<ScheduledTaskRepository>();
        var scheduledTaskHubContext = scope.ServiceProvider.GetRequiredService<IHubContext<ScheduledTaskHub>>();

        await DoWork();

        while (await timer.WaitForNextTickAsync(cancellationToken)) await DoWork();

        return;

        async Task DoWork()
        {
            var taskConfigs =
                await taskConfigRepository.AllAsync(f => f.Cron == TaskConfigCron.EveryWeek && f.IsActive,
                    cancellationToken);

            var tasks = CreateTasksFromConfigs(taskConfigs);

            await taskRepository.AddRangeAsync(tasks, cancellationToken);

            await taskRepository.SaveChangesAsync(cancellationToken);

            await scheduledTaskHubContext.Clients.All.SendAsync("ScheduledTaskHub", cancellationToken);
        }
    }

    private async Task RunMonthlyTask(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromDays(30));

        using var scope = serviceScopeFactory.CreateScope();

        var taskConfigRepository = scope.ServiceProvider.GetRequiredService<ScheduledTaskConfigRepository>();
        var taskRepository = scope.ServiceProvider.GetRequiredService<ScheduledTaskRepository>();
        var scheduledTaskHubContext = scope.ServiceProvider.GetRequiredService<IHubContext<ScheduledTaskHub>>();

        await DoWork();

        while (await timer.WaitForNextTickAsync(cancellationToken)) await DoWork();

        return;

        async Task DoWork()
        {
            var taskConfigs =
                await taskConfigRepository.AllAsync(f => f.Cron == TaskConfigCron.EveryMonth && f.IsActive,
                    cancellationToken);

            var tasks = CreateTasksFromConfigs(taskConfigs);

            await taskRepository.AddRangeAsync(tasks, cancellationToken);

            await taskRepository.SaveChangesAsync(cancellationToken);

            await scheduledTaskHubContext.Clients.All.SendAsync("ScheduledTaskHub", cancellationToken);
        }
    }

    private async Task RunAsSoonAsPossible(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        using var scope = serviceScopeFactory.CreateScope();

        var taskRepository = scope.ServiceProvider.GetRequiredService<ScheduledTaskRepository>();
        var scheduledTaskHubContext = scope.ServiceProvider.GetRequiredService<IHubContext<ScheduledTaskHub>>();

        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            var task = await taskRepository.GetNextTaskAsync(cancellationToken);

            if (task == null)
            {
                await Task.Delay(5000, cancellationToken);
                continue;
            }

            task.Status = TaskStatus.InProgress;
            task.Progress = new decimal(0.0);

            await taskRepository.SaveChangesAsync(cancellationToken);

            var taskExecutor = scope.ServiceProvider.GetKeyedService<IBackgroundTasked>(task.Type);

            if (taskExecutor == null)
            {
                logger.LogError("No TaskExecutorClass for type: {type}({typeCode})", task.Type, (int)task.Type);

                task.Status = TaskStatus.Error;
                task.Result = $"No TaskExecutorClass for type: {task.Type}({(int)task.Type})";
            }
            else
            {
                //TODO: set execution time limit from settings
                try
                {
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    linkedCts.CancelAfter(TimeSpan.FromHours(1));
                    var combinedToken = linkedCts.Token;

                    logger.LogInformation("Started task: {}({typeCode}) | {settings}", task.Type, (int)task.Type,
                        task.ScheduledTaskConfig!.Settings.RootElement.ToString());
                    var result = await taskExecutor.ExecuteAsync(task, combinedToken);
                    logger.LogInformation("Finished task: {}({typeCode})", task.Type, (int)task.Type);

                    if (result.IsSuccess)
                    {
                        task.Progress = new decimal(1.0);
                        task.Result = result.ValueOrDefault;
                    }
                    else if (result.Reasons.Count != 0)
                    {
                        task.Result = string.Join(", ", result.Reasons.Select(f => f.Message));
                    }

                    task.Status = TaskStatus.Completed;
                }
                catch (Exception e)
                {
                    task.Status = TaskStatus.Error;
                    task.Result = e.Message;
                }
            }

            await taskRepository.SaveChangesAsync(cancellationToken);

            await scheduledTaskHubContext.Clients.All.SendAsync("ScheduledTaskHub", cancellationToken);
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
