using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models.Entities;

namespace WebReader.Repositories;

public class ScheduledTaskConfigRepository(ApplicationDbContext context) : IRepository<ScheduledTaskConfig>
{
    public async Task<ScheduledTaskConfig?> FirstOrDefaultAsync(Expression<Func<ScheduledTaskConfig, bool>> predicate,
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

        return await query.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public async Task<IEnumerable<ScheduledTaskConfig>> AllAsync(Expression<Func<ScheduledTaskConfig, bool>> predicate,
        CancellationToken cancellationToken,
        params Expression<Func<ScheduledTaskConfig, object>>[] includes)
    {
        IQueryable<ScheduledTaskConfig> query = context.Set<ScheduledTaskConfig>();

        foreach (var include in includes)
            query = query.Include(include);

        return await query.Where(predicate).OrderBy(f => f.Type).ToListAsync(cancellationToken);
    }

    public async Task<ScheduledTaskConfig> AddAsync(ScheduledTaskConfig entity, CancellationToken cancellationToken)
    {
        var res = await context.ScheduledTaskConfigs.AddAsync(entity, cancellationToken);
        return res.Entity;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await context.SaveChangesAsync(cancellationToken);
    }
}
