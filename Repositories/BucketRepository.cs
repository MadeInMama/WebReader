using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models;
using WebReader.Models.Entities;

namespace WebReader.Repositories;

public class BucketRepository(ApplicationDbContext context) : IRepository<Bucket>
{
    public async Task<Bucket?> FirstOrDefaultAsync(Expression<Func<Bucket, bool>> predicate,
        ApplicationDbContext? ctx,
        CancellationToken cancellationToken,
        bool asNoTracking,
        params Expression<Func<Bucket, object>>[] includes)
    {
        IQueryable<Bucket> query = (ctx ?? context).Set<Bucket>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public async Task<IEnumerable<Bucket>> AllAsync(Expression<Func<Bucket, bool>> predicate,
        CancellationToken cancellationToken, bool asNoTracking = false,
        params Expression<Func<Bucket, object>>[] includes)
    {
        IQueryable<Bucket> query = context.Set<Bucket>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

        return await query.Where(predicate).ToListAsync(cancellationToken);
    }

    public async Task<Bucket> AddAsync(Bucket entity, CancellationToken cancellationToken)
    {
        var res = await context.Buckets.AddAsync(entity, cancellationToken);
        return res.Entity;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await context.SaveChangesAsync(cancellationToken);
    }

    public void AttachAll(IEnumerable<Bucket> entities)
    {
        foreach (var entity in entities) context.Buckets.Attach(entity);
    }

    public async Task<IEnumerable<Bucket>> GetAllAvailableBucketsAsync(IEnumerable<RoleType> roles, Guid userId,
        CancellationToken cancellationToken)
    {
        return await context.Buckets
            .Where(f => f.IsAvailable && !f.IsHidden && f.AccessRoles.Intersect(roles).Any() &&
                        (f.UserId == userId || f.UserId == null))
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteAllAsync(IEnumerable<Guid>? ids, CancellationToken cancellationToken)
    {
        var idsArray = ids?.ToArray() ?? [];

        if (idsArray.Length == 0) return;

        await context.Buckets
            .Where(r => idsArray.Contains(r.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (id == null) return;

        await DeleteAllAsync([id.Value], cancellationToken);
    }
}
