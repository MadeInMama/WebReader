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
        CancellationToken cancellationToken,
        params Expression<Func<ScheduledTask, object>>[] includes)
    {
        IQueryable<ScheduledTask> query = context.Set<ScheduledTask>();

        foreach (var include in includes)
            query = query.Include(include);

        return await query.Where(predicate)
            .OrderBy(f => f.Priority)
            .ThenByDescending(f => f.Type)
            .ThenByDescending(f => f.CreatedDate)
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
                        f.ScheduledTaskConfig!.IsActive) //TODO: is config active & delete not active pending tasks
            .OrderByDescending(f => f.Priority)
            .ThenBy(f => f.Type)
            .ThenBy(f => f.CreatedDate)
            .Include(f => f.ScheduledTaskConfig)
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
}
