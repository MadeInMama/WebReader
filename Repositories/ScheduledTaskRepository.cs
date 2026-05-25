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
        bool asNoTracking = false,
        params Expression<Func<ScheduledTask, object>>[] includes)
    {
        IQueryable<ScheduledTask> query = (ctx ?? context).Set<ScheduledTask>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(predicate);
    }

    public async Task<IEnumerable<ScheduledTask>> AllAsync(Expression<Func<ScheduledTask, bool>> predicate,
        params Expression<Func<ScheduledTask, object>>[] includes)
    {
        IQueryable<ScheduledTask> query = context.Set<ScheduledTask>();

        foreach (var include in includes)
            query = query.Include(include);

        return await query.Where(predicate).ToListAsync();
    }

    public async Task<ScheduledTask> AddAsync(ScheduledTask entity)
    {
        var res = await context.ScheduledTasks.AddAsync(entity);
        return res.Entity;
    }

    public async Task<int> SaveChangesAsync()
    {
        return await context.SaveChangesAsync();
    }

    public async Task<ScheduledTask?> GetNextTaskAsync(CancellationToken cancellationToken)
    {
        return await context.ScheduledTasks
            .Where(f => f.Status == TaskStatus.Pending)
            .OrderByDescending(f => f.Priority)
            .ThenBy(f => f.CreatedDate)
            .Include(f => f.ScheduledTaskConfig)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<ScheduledTask> entities, CancellationToken cancellationToken)
    {
        await context.ScheduledTasks.AddRangeAsync(entities, cancellationToken);
    }

    public async Task<int> DeleteAllOlderThenAsync(DateTimeOffset updatedDate)
    {
        return await context.ScheduledTasks
            .Where(f => f.UpdatedDate < updatedDate)
            .ExecuteDeleteAsync();
    }

    public async Task<int> DeleteAllOlderThenAsync(DateTimeOffset updatedDate, TaskStatus status)
    {
        return await context.ScheduledTasks
            .Where(f => f.UpdatedDate < updatedDate && f.Status == status)
            .ExecuteDeleteAsync();
    }
}
