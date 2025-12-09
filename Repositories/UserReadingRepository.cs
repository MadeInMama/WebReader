using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models.Entities;

namespace WebReader.Repositories;

public class UserReadingRepository(ApplicationDbContext context) : IRepository<UserReading>
{
    public async Task<UserReading?> FirstOrDefaultAsync(Expression<Func<UserReading, bool>> predicate)
    {
        return await context.UserReadings.FirstOrDefaultAsync(predicate);
    }

    public async Task<UserReading> AddAsync(UserReading entity)
    {
        var res = await context.UserReadings.AddAsync(entity);
        await context.SaveChangesAsync();
        return res.Entity;
    }

    public async Task<IEnumerable<UserReading>> AllAsync(Expression<Func<UserReading, bool>> predicate,
        params Expression<Func<UserReading, object>>[] includes)
    {
        IQueryable<UserReading> query = context.Set<UserReading>();

        foreach (var include in includes)
            query = query.Include(include);

        return await query.Where(predicate).ToListAsync();
    }

    public async Task SetCurrPageAsync(Guid id, int page, int scale)
    {
        await context.UserReadings
            .Where(x => x.Id == id)
            .ExecuteUpdateAsync(f =>
                f.SetProperty(e => e.Page, page)
                    .SetProperty(e => e.Scale, scale)
                    .SetProperty(e => e.UpdatedDate, DateTimeOffset.UtcNow));
    }
}
