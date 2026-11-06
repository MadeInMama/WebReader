using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models;
using WebReader.Models.Entities;

namespace WebReader.Repositories;

public class BucketRepository(ApplicationDbContext context) : IRepository<Bucket>
{
    public Task<Bucket?> FirstOrDefaultAsync(Expression<Func<Bucket, bool>> predicate,
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

        return query.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public Task<List<Bucket>> AllAsync(
        Expression<Func<Bucket, bool>> predicate,
        CancellationToken cancellationToken,
        bool asNoTracking = false,
        ApplicationDbContext? ctx = null,
        int take = int.MaxValue,
        int skip = 0,
        params Expression<Func<Bucket, object>>[] includes)
    {
        IQueryable<Bucket> query = (ctx ?? context).Set<Bucket>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

        return query.Where(predicate).OrderBy(f => f.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        Expression<Func<Bucket, bool>> predicate,
        CancellationToken cancellationToken,
        ApplicationDbContext? ctx = null,
        int take = int.MaxValue,
        int skip = 0,
        params Expression<Func<Bucket, object>>[] includes)
    {
        IQueryable<Bucket> query = (ctx ?? context).Set<Bucket>();

        foreach (var include in includes)
            query = query.Include(include);

        query = query.AsNoTracking();

        return query.Where(predicate).OrderBy(f => f.Id).CountAsync(cancellationToken);
    }

    public async Task<Bucket> AddAsync(Bucket entity, CancellationToken cancellationToken)
    {
        var res = await context.Buckets.AddAsync(entity, cancellationToken);
        return res.Entity;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return context.SaveChangesAsync(cancellationToken);
    }

    public void AttachAll(IEnumerable<Bucket> entities)
    {
        foreach (var entity in entities) context.Buckets.Attach(entity);
    }

    public Task<List<Bucket>> GetAllAvailableBucketsAsync(IEnumerable<RoleType> roles, Guid userId,
        CancellationToken cancellationToken, ApplicationDbContext? ctx = null, bool asNoTracking = false)
    {
        var res = (ctx ?? context).Buckets
            .Where(f => f.IsAvailable && !f.IsHidden && f.AccessRoles.Intersect(roles).Any() &&
                        (f.UserId == userId || f.UserId == null));

        if (asNoTracking) res.AsNoTracking();

        return res.ToListAsync(cancellationToken);
    }

    public Task<int> DeleteAllAsync(IEnumerable<Guid>? ids, CancellationToken cancellationToken)
    {
        var idsArray = ids?.ToArray() ?? [];

        if (idsArray.Length == 0) return Task.FromResult(0);

        return context.Buckets
            .Where(r => idsArray.Contains(r.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<int> DeleteAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (id == null) return Task.FromResult(0);

        return DeleteAllAsync([id.Value], cancellationToken);
    }
}
