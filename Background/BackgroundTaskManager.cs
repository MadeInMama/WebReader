using System.Collections.Immutable;
using AngleSharp.Common;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using WebReader.Configuration;
using WebReader.Models;
using WebReader.Models.Entities;
using WebReader.Models.Signal;
using WebReader.Repositories;
using TaskStatus = WebReader.Models.TaskStatus;

namespace WebReader.Background;

public class BackgroundTaskManager(
    ILogger<BackgroundTaskManager> logger,
    IServiceScopeFactory serviceScopeFactory,
    IOptions<BackgroundTaskConfig> backgroundTaskConfig,
    IConnectionMultiplexer redis)
    : BackgroundService
{
    private const string LockKeyRunSpreadTasks = "BackgroundTaskManager_LockKeyRunSpreadTasks";
    private const string LockKeyRunAsSoonAsPossible = "BackgroundTaskManager_LockKeyRunAsSoonAsPossible";

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!backgroundTaskConfig.Value.Enabled)
        {
            logger.LogInformation("Background tasks are disabled by configuration");
            return;
        }

        if (!redis.IsConnected)
        {
            logger.LogError("Redis is not connected. Background tasks will not run");
            return;
        }

        await Task.WhenAll(
            RunSpreadTasks(cancellationToken),
            RunAsSoonAsPossible(cancellationToken)
        );
    }

    private async Task RunSpreadTasks(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));

        using var scope = serviceScopeFactory.CreateScope();

        var taskConfigRepository = scope.ServiceProvider.GetRequiredService<ScheduledTaskConfigRepository>();
        var taskRepository = scope.ServiceProvider.GetRequiredService<ScheduledTaskRepository>();
        var scheduledTaskHubContext = scope.ServiceProvider.GetRequiredService<IHubContext<ScheduledTaskHub>>();

        await DoWork();

        while (await timer.WaitForNextTickAsync(cancellationToken)) await DoWork();

        return;

        async Task DoWork()
        {
            var lockData = await AcquireLock(LockKeyRunSpreadTasks);

            if (!lockData.LockAcquired) return;

            try
            {
                var taskConfigs = (await taskConfigRepository.AllAsync(f => f.IsActive, cancellationToken, true))
                    .GroupBy(f => f.Cron)
                    .ToImmutableDictionary(f => f.Key, f => f.ToList());

                var tasks = new List<ScheduledTask>();
                tasks.AddRange(await CreateTasksFromConfigs(taskConfigs.GetOrDefault(TaskCron.EveryHour, [])));
                tasks.AddRange(await CreateTasksFromConfigs(taskConfigs.GetOrDefault(TaskCron.EveryDay, [])));
                tasks.AddRange(await CreateTasksFromConfigs(taskConfigs.GetOrDefault(TaskCron.EveryWeek, [])));
                tasks.AddRange(await CreateTasksFromConfigs(taskConfigs.GetOrDefault(TaskCron.EveryMonth, [])));

                if (tasks.Count > 0)
                {
                    await taskRepository.AddRangeAsync(tasks, cancellationToken);

                    await taskRepository.SaveChangesAsync(cancellationToken);

                    await scheduledTaskHubContext.Clients.All.SendAsync("ScheduledTaskHub", cancellationToken);
                }
            }
            finally
            {
                await ReleaseLock(lockData.Database, lockData.LockKey, lockData.LockId);
            }
        }

        async Task<IEnumerable<ScheduledTask>> CreateTasksFromConfigs(List<ScheduledTaskConfig> configs)
        {
            var res = new List<ScheduledTask>();

            foreach (var config in configs)
            {
                var lastTask = await taskRepository.GetLastTaskByConfigIdAsync(config.Id, cancellationToken);

                if (lastTask == null) res.Add(MapConfigToTask(config));
                else if (lastTask.Status != TaskStatus.Pending)
                    res.Add(
                        MapConfigToTask(config, GetNextHaveToStartAtByCronType(lastTask.HaveToStartAt, config.Cron)));
            }

            return res;
        }

        DateTimeOffset GetNextHaveToStartAtByCronType(DateTimeOffset lastHaveToStartAt, TaskCron cron)
        {
            return cron switch
            {
                TaskCron.EveryHour => lastHaveToStartAt.AddHours(1),
                TaskCron.EveryDay => lastHaveToStartAt.AddDays(1),
                TaskCron.EveryWeek => lastHaveToStartAt.AddDays(7),
                TaskCron.EveryMonth => lastHaveToStartAt.AddDays(30),
                _ => throw new ArgumentOutOfRangeException(nameof(cron), cron, null)
            };
        }

        ScheduledTask MapConfigToTask(ScheduledTaskConfig config, DateTimeOffset? haveToStartAt = null)
        {
            return new ScheduledTask
            {
                Type = config.Type,
                Priority = config.DefaultPriority,
                ScheduledTaskConfigId = config.Id,
                HaveToStartAt = haveToStartAt ?? DateTimeOffset.UtcNow,
                Cron = config.Cron,
                Settings = config.DefaultSettings
            };
        }
    }

    private async Task RunAsSoonAsPossible(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        using var scope = serviceScopeFactory.CreateScope();

        var taskRepository = scope.ServiceProvider.GetRequiredService<ScheduledTaskRepository>();
        var scheduledTaskHubContext = scope.ServiceProvider.GetRequiredService<IHubContext<ScheduledTaskHub>>();

        while (await timer.WaitForNextTickAsync(cancellationToken)) await DoWork(scope);

        return;

        async Task DoWork(IServiceScope serviceScope)
        {
            (IDatabase? Database, string? LockKey, string? LockId, bool? LockAcquired) lockData =
                (null, null, null, null);

            try
            {
                var task = await taskRepository.GetNextTaskAsync(cancellationToken);

                if (task == null)
                {
                    await Task.Delay(5000, cancellationToken);
                    return;
                }

                lockData = await AcquireLock($"{LockKeyRunAsSoonAsPossible}_{task.Type}");

                if (!lockData.LockAcquired!.Value) return;

                var taskExecutor = serviceScope.ServiceProvider.GetKeyedService<IBackgroundTasked>(task.Type);

                if (taskExecutor == null)
                {
                    logger.LogError("No TaskExecutorClass for type: {type}({typeCode})", task.Type, (int)task.Type);

                    await taskRepository.SetStatusProgressResultAsync(task.Id, TaskStatus.Error, 0.0m,
                        $"No TaskExecutorClass for type: {task.Type}({(int)task.Type})", cancellationToken);
                }
                else
                {
                    await taskRepository.SetStatusProgressResultAsync(task.Id, TaskStatus.InProgress, 0.0m,
                        null, cancellationToken);

                    //TODO: set execution time limit from settings
                    try
                    {
                        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        linkedCts.CancelAfter(TimeSpan.FromMinutes(60));
                        var combinedToken = linkedCts.Token;

                        logger.LogInformation("Started task: {}({typeCode}) | {settings}", task.Type, (int)task.Type,
                            task.Settings.RootElement.ToString());
                        var result = await taskExecutor.ExecuteAsync(task, combinedToken);
                        logger.LogInformation("Finished task: {}({typeCode})", task.Type, (int)task.Type);

                        if (result.IsSuccess)
                            await taskRepository.SetStatusProgressResultAsync(task.Id, TaskStatus.Completed, 1.0m,
                                result.ValueOrDefault, cancellationToken);
                        else if (result.Reasons.Count != 0)
                            await taskRepository.SetStatusProgressResultAsync(task.Id, TaskStatus.Error, null,
                                string.Join(", ", result.Reasons.Select(f => f.Message)), cancellationToken);
                    }
                    catch (OperationCanceledException e)
                    {
                        await taskRepository.SetStatusProgressResultAsync(task.Id, TaskStatus.Canceled, null, e.Message,
                            cancellationToken);
                    }
                    catch (Exception e)
                    {
                        await taskRepository.SetStatusProgressResultAsync(task.Id, TaskStatus.Error, null, e.Message,
                            cancellationToken);
                    }
                }

                await scheduledTaskHubContext.Clients.All.SendAsync("ScheduledTaskHub", cancellationToken);
            }
            finally
            {
                if (lockData is not (Database: null, LockKey: null, LockId: null, LockAcquired: null))
                    await ReleaseLock(lockData.Database!, lockData.LockKey!, lockData.LockId!);
            }
        }
    }

    private async Task<(IDatabase Database, string LockKey, string LockId, bool LockAcquired)> AcquireLock(
        string lockKey)
    {
        var database = redis.GetDatabase();
        var lockTimeout = TimeSpan.FromMinutes(61);
        var lockId = Guid.NewGuid().ToString("N");
        var lockAcquired = await database.LockTakeAsync(lockKey, lockId, lockTimeout);

        if (!lockAcquired)
            logger.LogInformation("Background tasks lock {} already held by another instance, skipping", lockKey);
        else
            logger.LogInformation("Background tasks lock acquired by instance: {}", lockId);

        return (database, lockKey, lockId, lockAcquired);
    }

    private async Task ReleaseLock(IDatabase db, string lockKey, string lockId)
    {
        await db.LockReleaseAsync(lockKey, lockId);
        logger.LogInformation("Background tasks lock {lockKey} released by instance: {lockId}", lockKey, lockId);
    }
}
