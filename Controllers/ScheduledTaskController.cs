using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WebReader.Helpers;
using WebReader.Models;
using WebReader.Models.Dtos;
using WebReader.Models.Dtos.Item;
using WebReader.Models.Entities;
using WebReader.Models.Signal;
using WebReader.Repositories;
using TaskStatus = WebReader.Models.TaskStatus;

namespace WebReader.Controllers;

[Authorize]
[Route("[controller]/[action]")]
public class ScheduledTaskController(
    ScheduledTaskConfigRepository configRepository,
    ScheduledTaskRepository taskRepository,
    IHubContext<ScheduledTaskHub> scheduledTaskHubContext) : Controller
{
    [ServiceFilter(typeof(LogRequestAttribute))]
    public async Task<IActionResult> ScheduledTasks(CancellationToken cancellationToken = default)
    {
        if (!User.GetUserRoles().Contains(RoleType.Admin)) return RedirectToAction("AccessDenied", "Account");

        var configs = await configRepository.AllAsync(f => f.IsActive, cancellationToken, true);

        return View(new AllScheduledTaskConfigsViewModel
        {
            Items = configs.Select(f => new AllScheduledTaskConfigsItem
            {
                Type = f.Type,
                Priority = f.DefaultPriority,
                Settings = f.DefaultSettings.RootElement
            })
        });
    }

    [HttpPost]
    public async Task<IActionResult> ScheduledTasksPartial([FromBody] FilterRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!User.GetUserRoles().Contains(RoleType.Admin)) return RedirectToAction("AccessDenied", "Account");

        request.Values.TryGetValue("Type", out var typeStr);
        request.Values.TryGetValue("Status", out var statusStr);
        request.Values.TryGetValue("Cron", out var cronStr);

        List<TaskType> types = [];
        List<TaskStatus> statuses = [];
        List<TaskCron> crons = [];

        typeStr?.ForEach(f =>
        {
            if (StaticFunctions.TryParseNullable(f, out TaskType? type) && type.HasValue)
                types.Add(type.Value);
        });
        statusStr?.ForEach(f =>
        {
            if (StaticFunctions.TryParseNullable(f, out TaskStatus? status) && status.HasValue)
                statuses.Add(status.Value);
        });
        cronStr?.ForEach(f =>
        {
            if (StaticFunctions.TryParseNullable(f, out TaskCron? cron) && cron.HasValue)
                crons.Add(cron.Value);
        });

        var res = await taskRepository.AllAsync(f => (types.Count == 0 || types.Contains(f.Type)) &&
                                                     (statuses.Count == 0 || statuses.Contains(f.Status)) &&
                                                     (crons.Count == 0 || crons.Contains(f.Cron)),
            cancellationToken, true);

        return PartialView("_ScheduledTasksPartial", new AllScheduledTasksViewModel
        {
            Items = res.Select(f => new AllScheduledTasksItem
            {
                Id = f.Id,
                CreatedDate = f.CreatedDate,
                UpdatedDate = f.UpdatedDate,
                HaveToStartAt = f.HaveToStartAt,
                Type = f.Type,
                Priority = f.Priority,
                Status = f.Status,
                Result = f.Result,
                Progress = f.Progress,
                ScheduledTaskConfigId = f.ScheduledTaskConfigId,
                Cron = f.Cron,
                Settings = f.Settings
            })
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.GetUserRoles().Contains(RoleType.Admin)) return RedirectToAction("AccessDenied", "Account");

        await taskRepository.AddAsync(new ScheduledTask
        {
            Type = request.Type,
            Priority = request.Priority,
            HaveToStartAt = request.HaveToStartAt,
            Cron = TaskCron.Manually,
            Settings = JsonDocument.Parse(request.Settings)
        }, cancellationToken);

        await taskRepository.SaveChangesAsync(cancellationToken);

        await scheduledTaskHubContext.Clients.All.SendAsync("ScheduledTaskHub", cancellationToken);

        return Created();
    }
}
