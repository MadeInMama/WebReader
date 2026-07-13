using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models.Entities;

namespace WebReader.Repositories;

public class UserReadingRepository(
    ApplicationDbContext context,
    IDbContextFactory<ApplicationDbContext> contextFactory) : IRepository<UserReading>
{
    public async Task<UserReading?> FirstOrDefaultAsync(Expression<Func<UserReading, bool>> predicate,
        ApplicationDbContext? ctx,
        CancellationToken cancellationToken,
        bool asNoTracking,
        params Expression<Func<UserReading, object>>[] includes)
    {
        IQueryable<UserReading> query = (ctx ?? context).Set<UserReading>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public async Task<UserReading> AddAsync(UserReading entity, CancellationToken cancellationToken)
    {
        var res = await context.UserReadings.AddAsync(entity, cancellationToken);
        return res.Entity;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<UserReading>> AllAsync(Expression<Func<UserReading, bool>> predicate,
        CancellationToken cancellationToken, bool asNoTracking = false, ApplicationDbContext? ctx = null,
        params Expression<Func<UserReading, object>>[] includes)
    {
        IQueryable<UserReading> query = (ctx ?? context).Set<UserReading>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

        return await query.Where(predicate).ToListAsync(cancellationToken);
    }

    public async Task SetCurrPageAndScaleAndIsDoneAsync(Guid id, int page, int scale, bool isDone,
        CancellationToken cancellationToken, ApplicationDbContext? ctx = null)
    {
        await (ctx ?? context).UserReadings
            .Where(r => r.Id == id)
            .ExecuteUpdateAsync(f =>
                f.SetProperty(e => e.Page, page)
                    .SetProperty(e => e.Scale, scale)
                    .SetProperty(e => e.IsDone, isDone)
                    .SetProperty(e => e.UpdatedDate, DateTimeOffset.UtcNow), cancellationToken);
    }

    public async Task SetCurrPageAndScaleAndIsDoneAsync(IEnumerable<UserReading> readings,
        CancellationToken cancellationToken)
    {
        var tasks = new List<Task>();

        foreach (var reading in readings)
        {
            await using var ctx = await contextFactory.CreateDbContextAsync(cancellationToken);
            tasks.Add(SetCurrPageAndScaleAndIsDoneAsync(reading.Id, reading.Page, reading.Scale, reading.IsDone,
                cancellationToken, ctx));
        }

        await Task.WhenAll(tasks);
    }

    public async Task DeleteAllAsync(IEnumerable<Guid>? ids, CancellationToken cancellationToken)
    {
        var idsArray = ids?.ToArray() ?? [];

        if (idsArray.Length == 0)
            return;

        await context.UserReadings
            .Where(r => idsArray.Contains(r.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
