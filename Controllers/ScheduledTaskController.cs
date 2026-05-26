using Microsoft.AspNetCore.Mvc;
using WebReader.Helpers;
using WebReader.Models;
using WebReader.Models.Dtos;
using WebReader.Models.Dtos.Item;
using WebReader.Repositories;

namespace WebReader.Controllers;

[Route("[controller]/[action]")]
public class ScheduledTaskController(ScheduledTaskRepository scheduledTaskRepository) : Controller
{
    [ServiceFilter(typeof(LogRequestAttribute))]
    public async Task<IActionResult> ScheduledTasks(CancellationToken cancellationToken = default)
    {
        if (!User.GetUserRoles().Contains(RoleType.Admin)) return RedirectToAction("AccessDenied", "Account");


        return View(await GetScheduledTasks(cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> ScheduledTasksPartial(CancellationToken cancellationToken = default)
    {
        if (!User.GetUserRoles().Contains(RoleType.Admin)) return RedirectToAction("AccessDenied", "Account");


        return PartialView("_ScheduledTasksPartial", await GetScheduledTasks(cancellationToken));
    }

    private async Task<AllScheduledTasksViewModel> GetScheduledTasks(CancellationToken cancellationToken = default)
    {
        var res = await scheduledTaskRepository.AllAsync(f => f.ScheduledTaskConfig!.IsActive, cancellationToken,
            f => f.ScheduledTaskConfig);

        return new AllScheduledTasksViewModel
        {
            Items = res.Select(f => new AllScheduledTasksItem
            {
                Id = f.Id,
                CreatedDate = f.CreatedDate,
                UpdatedDate = f.UpdatedDate,
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
