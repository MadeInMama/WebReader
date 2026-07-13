using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models.Entities;
using TaskStatus = WebReader.Models.TaskStatus;

namespace WebReader.Repositories;

public class ScheduledTaskRepository(ApplicationDbContext context) : IRepository<ScheduledTask>
{
    public async Task<ScheduledTask?> FirstOrDefaultAsync(Expression<Func<ScheduledTask, bool>> predicate,
        ApplicationDbContext? ctx,
        CancellationToken cancellationToken,
        bool asNoTracking = false,
        params Expression<Func<ScheduledTask, object>>[] includes)
    {
        IQueryable<ScheduledTask> query = (ctx ?? context).Set<ScheduledTask>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public async Task<IEnumerable<ScheduledTask>> AllAsync(Expression<Func<ScheduledTask, bool>> predicate,
        CancellationToken cancellationToken, bool asNoTracking = false, ApplicationDbContext? ctx = null,
        params Expression<Func<ScheduledTask, object>>[] includes)
    {
        IQueryable<ScheduledTask> query = (ctx ?? context).Set<ScheduledTask>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

        return await query.Where(predicate)
            .OrderBy(f => f.Status)
            .ThenByDescending(f => f.HaveToStartAt)
            .ThenBy(f => f.Priority)
            .ThenByDescending(f => f.Type)
            .ToListAsync(cancellationToken);
    }

    public async Task<ScheduledTask> AddAsync(ScheduledTask entity, CancellationToken cancellationToken)
    {
        var res = await context.ScheduledTasks.AddAsync(entity, cancellationToken);
        return res.Entity;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<ScheduledTask?> GetNextTaskAsync(CancellationToken cancellationToken)
    {
        return await context.ScheduledTasks
            .Where(f => f.Status == TaskStatus.Pending &&
                        (f.ScheduledTaskConfigId == null || f.ScheduledTaskConfig!.IsActive) &&
                        f.HaveToStartAt < DateTimeOffset.UtcNow) //TODO: delete not active pending tasks
            .OrderByDescending(f => f.Priority)
            .ThenBy(f => f.HaveToStartAt)
            .ThenBy(f => f.Type)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ScheduledTask?> GetLastTaskByConfigIdAsync(Guid configId, CancellationToken cancellationToken)
    {
        return await context.ScheduledTasks
            .Where(f => f.ScheduledTaskConfigId == configId)
            .OrderByDescending(f => f.HaveToStartAt)
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<ScheduledTask> entities, CancellationToken cancellationToken)
    {
        await context.ScheduledTasks.AddRangeAsync(entities, cancellationToken);
    }

    public async Task<int> DeleteAllOlderThenAsync(DateTimeOffset updatedDate, List<TaskStatus> status,
        CancellationToken cancellationToken)
    {
        return await context.ScheduledTasks
            .Where(f => f.UpdatedDate < updatedDate && status.Contains(f.Status))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task SetStatusProgressResultAsync(Guid id, TaskStatus status, decimal? progress, string? result,
        CancellationToken cancellationToken)
    {
        await context.ScheduledTasks
            .Where(r => r.Id == id)
            .ExecuteUpdateAsync(f => f
                    .SetProperty(e => e.Status, status)
                    .SetProperty(e => e.Progress, e => progress ?? e.Progress)
                    .SetProperty(e => e.Result, e => result ?? e.Result)
                    .SetProperty(e => e.UpdatedDate, DateTimeOffset.UtcNow),
                cancellationToken);
    }
}
