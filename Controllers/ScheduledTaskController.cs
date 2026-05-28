using Microsoft.AspNetCore.Mvc;
using WebReader.Helpers;
using WebReader.Models;
using WebReader.Models.Dtos;
using WebReader.Models.Dtos.Item;
using WebReader.Repositories;
using TaskStatus = WebReader.Models.TaskStatus;

namespace WebReader.Controllers;

[Route("[controller]/[action]")]
public class ScheduledTaskController(ScheduledTaskRepository scheduledTaskRepository) : Controller
{
    [ServiceFilter(typeof(LogRequestAttribute))]
    public IActionResult ScheduledTasks()
    {
        if (!User.GetUserRoles().Contains(RoleType.Admin)) return RedirectToAction("AccessDenied", "Account");

        return View(new AllScheduledTasksViewModel
        {
            Items = []
        });
    }

    [HttpPost]
    public async Task<IActionResult> ScheduledTasksPartial([FromBody] FilterRequest filters,
        CancellationToken cancellationToken = default)
    {
        if (!User.GetUserRoles().Contains(RoleType.Admin)) return RedirectToAction("AccessDenied", "Account");

        return PartialView("_ScheduledTasksPartial", await GetScheduledTasks(filters.Values, cancellationToken));
    }

    private async Task<AllScheduledTasksViewModel> GetScheduledTasks(IDictionary<string, string> filters,
        CancellationToken cancellationToken = default)
    {
        filters.TryGetValue("Type", out var typeStr);
        filters.TryGetValue("Status", out var statusStr);
        filters.TryGetValue("Cron", out var cronStr);

        StaticFunctions.TryParseNullable(typeStr, out TaskType? type);
        StaticFunctions.TryParseNullable(statusStr, out TaskStatus? status);
        StaticFunctions.TryParseNullable(cronStr, out TaskConfigCron? cron);

        var res = await scheduledTaskRepository.AllAsync(f => f.ScheduledTaskConfig!.IsActive &&
                                                              (type == null || f.Type == type) &&
                                                              (status == null || f.Status == status) &&
                                                              (cron == null || f.ScheduledTaskConfig.Cron == cron)
            , cancellationToken, true,
            f => f.ScheduledTaskConfig);

        return new AllScheduledTasksViewModel
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
        };
    }
}
