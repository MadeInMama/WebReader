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
                Id = f.Id,
                Type = f.Type
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

        _ = StaticFunctions.TryParseNullable(typeStr, out TaskType? type);
        _ = StaticFunctions.TryParseNullable(statusStr, out TaskStatus? status);
        _ = StaticFunctions.TryParseNullable(cronStr, out TaskConfigCron? cron);

        var res = await taskRepository.AllAsync(f => f.ScheduledTaskConfig!.IsActive &&
                                                     (type == null || f.Type == type) &&
                                                     (status == null || f.Status == status) &&
                                                     (cron == null || f.ScheduledTaskConfig.Cron == cron),
            cancellationToken, true,
            f => f.ScheduledTaskConfig);

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
                Cron = f.ScheduledTaskConfig!.Cron,
                Settings = f.ScheduledTaskConfig.Settings
            })
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.GetUserRoles().Contains(RoleType.Admin)) return RedirectToAction("AccessDenied", "Account");

        var config = await configRepository.FirstOrDefaultAsync(f => f.Id == request.ScheduledTaskConfigId,
            null, cancellationToken);

        if (config is not { IsActive: true }) return NotFound();

        await taskRepository.AddAsync(new ScheduledTask
        {
            Type = config.Type,
            Priority = config.DefaultPriority,
            ScheduledTaskConfigId = request.ScheduledTaskConfigId,
            HaveToStartAt = request.HaveToStartAt
        }, cancellationToken);

        await taskRepository.SaveChangesAsync(cancellationToken);

        await scheduledTaskHubContext.Clients.All.SendAsync("ScheduledTaskHub", cancellationToken);

        return Created();
    }
}
