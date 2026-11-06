using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models.Entities;

namespace WebReader.Repositories;

public class SubscriberPwaRepository(ApplicationDbContext context) : IRepository<SubscriberPwa>
{
    public Task<SubscriberPwa?> FirstOrDefaultAsync(Expression<Func<SubscriberPwa, bool>> predicate,
        ApplicationDbContext? ctx,
        CancellationToken cancellationToken,
        bool asNoTracking = false,
        params Expression<Func<SubscriberPwa, object>>[] includes)
    {
        IQueryable<SubscriberPwa> query = (ctx ?? context).Set<SubscriberPwa>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

        return query.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public Task<List<SubscriberPwa>> AllAsync(
        Expression<Func<SubscriberPwa, bool>> predicate,
        CancellationToken cancellationToken,
        bool asNoTracking = false,
        ApplicationDbContext? ctx = null,
        int take = int.MaxValue,
        int skip = 0,
        params Expression<Func<SubscriberPwa, object>>[] includes)
    {
        IQueryable<SubscriberPwa> query = (ctx ?? context).Set<SubscriberPwa>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

        return query.Where(predicate).OrderBy(f => f.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        Expression<Func<SubscriberPwa, bool>> predicate,
        CancellationToken cancellationToken,
        ApplicationDbContext? ctx = null,
        int take = int.MaxValue,
        int skip = 0,
        params Expression<Func<SubscriberPwa, object>>[] includes)
    {
        IQueryable<SubscriberPwa> query = (ctx ?? context).Set<SubscriberPwa>();

        foreach (var include in includes)
            query = query.Include(include);

        query = query.AsNoTracking();

        return query.Where(predicate).OrderBy(f => f.Id).CountAsync(cancellationToken);
    }

    public async Task<SubscriberPwa> AddAsync(SubscriberPwa entity, CancellationToken cancellationToken)
    {
        var res = await context.SubscriberPwas.AddAsync(entity, cancellationToken);
        return res.Entity;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return context.SaveChangesAsync(cancellationToken);
    }

    public Task<int> DeleteAllAsync(IEnumerable<Guid>? ids, CancellationToken cancellationToken)
    {
        var idsArray = ids?.ToArray() ?? [];

        if (idsArray.Length == 0) return Task.FromResult(0);

        return context.SubscriberPwas
            .Where(f => idsArray.Contains(f.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<int> DeleteAllByUserIdAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (id == null) return Task.FromResult(0);

        return context.SubscriberPwas
            .Where(f => f.UserId == id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<int> DeleteByUserIdAndEndpointAsync(Guid? id, string? endpoint,
        CancellationToken cancellationToken)
    {
        if (id == null || endpoint == null) return Task.FromResult(0);

        return context.SubscriberPwas
            .Where(f => f.UserId == id && f.Endpoint == endpoint)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<int> DeleteAsync(Guid? id, CancellationToken cancellationToken)
    {
        return id == null ? Task.FromResult(0) : DeleteAllAsync([id.Value], cancellationToken);
    }

    public Task<bool> ExistsAsync(Guid userId, string endpoint, CancellationToken cancellationToken)
    {
        return context.SubscriberPwas.AsNoTracking()
            .AnyAsync(f => f.UserId == userId && f.Endpoint == endpoint, cancellationToken);
    }
}
