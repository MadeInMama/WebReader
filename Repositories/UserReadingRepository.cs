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
        params Expression<Func<UserReading, object>>[] includes)
    {
        IQueryable<UserReading> query = (ctx ?? context).Set<UserReading>();

        foreach (var include in includes)
            query = query.Include(include);

        return await query.FirstOrDefaultAsync(predicate);
    }

    public async Task<UserReading> AddAsync(UserReading entity)
    {
        var res = await context.UserReadings.AddAsync(entity);
        return res.Entity;
    }

    public async Task<int> SaveChangesAsync()
    {
        return await context.SaveChangesAsync();
    }

    public async Task<IEnumerable<UserReading>> AllAsync(Expression<Func<UserReading, bool>> predicate,
        params Expression<Func<UserReading, object>>[] includes)
    {
        IQueryable<UserReading> query = context.Set<UserReading>();

        foreach (var include in includes)
            query = query.Include(include);

        return await query.Where(predicate).ToListAsync();
    }

    public async Task SetCurrPageAndScaleAndIsDoneAsync(Guid id, int page, int scale, bool isDone,
        ApplicationDbContext? ctx = null)
    {
        await (ctx ?? context).UserReadings
            .Where(r => r.Id == id)
            .ExecuteUpdateAsync(f =>
                f.SetProperty(e => e.Page, page)
                    .SetProperty(e => e.Scale, scale)
                    .SetProperty(e => e.IsDone, isDone)
                    .SetProperty(e => e.UpdatedDate, DateTimeOffset.UtcNow));
    }

    public async Task SetCurrPageAndScaleAndIsDoneAsync(IEnumerable<UserReading> readings)
    {
        var tasks = new List<Task>();

        foreach (var reading in readings)
        {
            var ctx = await contextFactory.CreateDbContextAsync();
            tasks.Add(SetCurrPageAndScaleAndIsDoneAsync(reading.Id, reading.Page, reading.Scale, reading.IsDone, ctx));
        }

        await Task.WhenAll(tasks);
    }

    public async Task DeleteAllAsync(IEnumerable<Guid>? ids)
    {
        var idsArray = ids?.ToArray() ?? [];

        if (idsArray.Length == 0)
            return;

        await context.UserReadings
            .Where(r => idsArray.Contains(r.Id))
            .ExecuteDeleteAsync();
    }

    public async Task DeleteAllByFileIdAsync(IEnumerable<Guid>? ids)
    {
        var idsArray = ids?.ToArray() ?? [];

        if (idsArray.Length == 0)
            return;

        await context.UserReadings
            .Where(r => idsArray.Contains(r.FileId))
            .ExecuteDeleteAsync();
    }
}
