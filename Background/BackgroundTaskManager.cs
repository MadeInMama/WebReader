using System.Collections.Immutable;
using AngleSharp.Common;
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
            var taskConfigs = (await taskConfigRepository.AllAsync(f => f.IsActive, cancellationToken, true))
                .GroupBy(f => f.Cron)
                .ToImmutableDictionary(f => f.Key, f => f.ToList());

            var tasks = new List<ScheduledTask>(
                await CreateTasksFromConfigs(taskConfigs.GetOrDefault(TaskConfigCron.EveryHour, [])));
            tasks.AddRange(await CreateTasksFromConfigs(taskConfigs.GetOrDefault(TaskConfigCron.EveryDay, [])));
            tasks.AddRange(await CreateTasksFromConfigs(taskConfigs.GetOrDefault(TaskConfigCron.EveryWeek, [])));
            tasks.AddRange(await CreateTasksFromConfigs(taskConfigs.GetOrDefault(TaskConfigCron.EveryMonth, [])));

            if (tasks.Count > 0)
            {
                await taskRepository.AddRangeAsync(tasks, cancellationToken);

                await taskRepository.SaveChangesAsync(cancellationToken);

                await scheduledTaskHubContext.Clients.All.SendAsync("ScheduledTaskHub", cancellationToken);
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

        DateTimeOffset GetNextHaveToStartAtByCronType(DateTimeOffset lastHaveToStartAt, TaskConfigCron cron)
        {
            return cron switch
            {
                TaskConfigCron.EveryHour => lastHaveToStartAt.AddHours(1),
                TaskConfigCron.EveryDay => lastHaveToStartAt.AddDays(1),
                TaskConfigCron.EveryWeek => lastHaveToStartAt.AddDays(7),
                TaskConfigCron.EveryMonth => lastHaveToStartAt.AddDays(30),
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
                HaveToStartAt = haveToStartAt ?? DateTimeOffset.UtcNow
            };
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

            var taskExecutor = scope.ServiceProvider.GetKeyedService<IBackgroundTasked>(task.Type);

            if (taskExecutor == null)
            {
                logger.LogError("No TaskExecutorClass for type: {type}({typeCode})", task.Type, (int)task.Type);

                await taskRepository.SetStatusProgressResultAsync(task.Id, TaskStatus.Error, new decimal(0.0),
                    $"No TaskExecutorClass for type: {task.Type}({(int)task.Type})", cancellationToken);
            }
            else
            {
                await taskRepository.SetStatusProgressResultAsync(task.Id, TaskStatus.InProgress, new decimal(0.0),
                    null, cancellationToken);

                //TODO: set execution time limit from settings
                try
                {
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    linkedCts.CancelAfter(TimeSpan.FromMinutes(60));
                    var combinedToken = linkedCts.Token;

                    logger.LogInformation("Started task: {}({typeCode}) | {settings}", task.Type, (int)task.Type,
                        task.ScheduledTaskConfig!.DefaultSettings.RootElement.ToString());
                    var result = await taskExecutor.ExecuteAsync(task, combinedToken);
                    logger.LogInformation("Finished task: {}({typeCode})", task.Type, (int)task.Type);

                    if (result.IsSuccess)
                        await taskRepository.SetStatusProgressResultAsync(task.Id, TaskStatus.Completed,
                            new decimal(1.0), result.ValueOrDefault, cancellationToken);
                    else if (result.Reasons.Count != 0)
                        await taskRepository.SetStatusProgressResultAsync(task.Id, TaskStatus.Completed, null,
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
    }
}
