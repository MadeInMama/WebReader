using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models;
using WebReader.Models.Entities;

namespace WebReader.Repositories;

public class BucketRepository(ApplicationDbContext context) : IRepository<Bucket>
{
    public async Task<Bucket?> FirstOrDefaultAsync(Expression<Func<Bucket, bool>> predicate)
    {
        return await context.Buckets.FirstOrDefaultAsync(predicate);
    }

    public async Task<IEnumerable<Bucket>> AllAsync(Expression<Func<Bucket, bool>> predicate,
        params Expression<Func<Bucket, object>>[] includes)
    {
        IQueryable<Bucket> query = context.Set<Bucket>();

        foreach (var include in includes)
            query = query.Include(include);

        return await query.Where(predicate).ToListAsync();
    }

    public Task<Bucket> AddAsync(Bucket entity)
    {
        throw new NotImplementedException();
    }

    public async Task UpdateAllAsync(IEnumerable<Bucket> entities)
    {
        context.Buckets.UpdateRange(entities);
        await context.SaveChangesAsync();
    }

    public async Task<IEnumerable<Bucket>> GetAllAvailableBucketsAsync(IEnumerable<RoleType> roles)
    {
        return await context.Buckets
            .Where(f => !f.IsHidden && f.AccessRoles.Intersect(roles).Any())
            .ToListAsync();
    }
}