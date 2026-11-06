using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models.Entities;

namespace WebReader.Repositories;

public class ScheduledTaskConfigRepository(ApplicationDbContext context) : IRepository<ScheduledTaskConfig>
{
    public Task<ScheduledTaskConfig?> FirstOrDefaultAsync(Expression<Func<ScheduledTaskConfig, bool>> predicate,
        ApplicationDbContext? ctx,
        CancellationToken cancellationToken,
        bool asNoTracking = false,
        params Expression<Func<ScheduledTaskConfig, object>>[] includes)
    {
        IQueryable<ScheduledTaskConfig> query = (ctx ?? context).Set<ScheduledTaskConfig>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

        return query.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public Task<List<ScheduledTaskConfig>> AllAsync(
        Expression<Func<ScheduledTaskConfig, bool>> predicate,
        CancellationToken cancellationToken,
        bool asNoTracking = false,
        ApplicationDbContext? ctx = null,
        int take = int.MaxValue,
        int skip = 0,
        params Expression<Func<ScheduledTaskConfig, object>>[] includes)
    {
        IQueryable<ScheduledTaskConfig> query = (ctx ?? context).Set<ScheduledTaskConfig>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

        return query.Where(predicate)
            .OrderByDescending(f => f.DefaultPriority)
            .ThenBy(f => f.Type)
            .Skip(skip).Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        Expression<Func<ScheduledTaskConfig, bool>> predicate,
        CancellationToken cancellationToken,
        ApplicationDbContext? ctx = null,
        int take = int.MaxValue,
        int skip = 0,
        params Expression<Func<ScheduledTaskConfig, object>>[] includes)
    {
        IQueryable<ScheduledTaskConfig> query = (ctx ?? context).Set<ScheduledTaskConfig>();

        foreach (var include in includes)
            query = query.Include(include);

        query = query.AsNoTracking();

        return query.Where(predicate)
            .OrderByDescending(f => f.DefaultPriority)
            .ThenBy(f => f.Type)
            .CountAsync(cancellationToken);
    }

    public async Task<ScheduledTaskConfig> AddAsync(ScheduledTaskConfig entity, CancellationToken cancellationToken)
    {
        var res = await context.ScheduledTaskConfigs.AddAsync(entity, cancellationToken);
        return res.Entity;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return context.SaveChangesAsync(cancellationToken);
    }
}
