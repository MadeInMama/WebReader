using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models;
using WebReader.Models.Entities;

namespace WebReader.Repositories;

public class BucketRepository(ApplicationDbContext context) : IRepository<Bucket>
{
    public async Task<Bucket?> FirstOrDefaultAsync(Expression<Func<Bucket, bool>> predicate,
        params Expression<Func<Bucket, object>>[] includes)
    {
        IQueryable<Bucket> query = context.Set<Bucket>();

        foreach (var include in includes)
            query = query.Include(include);

        return await query.FirstOrDefaultAsync(predicate);
    }

    public async Task<IEnumerable<Bucket>> AllAsync(Expression<Func<Bucket, bool>> predicate,
        params Expression<Func<Bucket, object>>[] includes)
    {
        IQueryable<Bucket> query = context.Set<Bucket>();

        foreach (var include in includes)
            query = query.Include(include);

        return await query.Where(predicate).ToListAsync();
    }

    public async Task<Bucket> AddAsync(Bucket entity)
    {
        var res = await context.Buckets.AddAsync(entity);
        await context.SaveChangesAsync();
        return res.Entity;
    }

    public async Task UpdateAllAsync(IEnumerable<Bucket> entities)
    {
        context.Buckets.UpdateRange(entities);
        await context.SaveChangesAsync();
    }

    public async Task<IEnumerable<Bucket>> GetAllAvailableBucketsAsync(IEnumerable<RoleType> roles, Guid userId)
    {
        return await context.Buckets
            .Where(f => f.IsAvailable && !f.IsHidden && f.AccessRoles.Intersect(roles).Any() &&
                        (f.UserId == userId || f.UserId == null))
            .ToListAsync();
    }

    public async Task DeleteAllAsync(IEnumerable<Guid>? ids)
    {
        var idsArray = ids?.ToArray() ?? [];

        if (idsArray.Length == 0) return;

        await context.Buckets
            .Where(r => idsArray.Contains(r.Id))
            .ExecuteDeleteAsync();
    }

    public async Task DeleteAsync(Guid? id)
    {
        if (id == null) return;

        await DeleteAllAsync([id.Value]);
    }
}
