using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models.Entities;

namespace WebReader.Repositories;

public class SubscriberPwaRepository(ApplicationDbContext context) : IRepository<SubscriberPwa>
{
    public async Task<SubscriberPwa?> FirstOrDefaultAsync(Expression<Func<SubscriberPwa, bool>> predicate,
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

        return await query.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public async Task<IEnumerable<SubscriberPwa>> AllAsync(
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

        return await query.Where(predicate).OrderBy(f => f.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(
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

        return await query.Where(predicate).OrderBy(f => f.Id).CountAsync(cancellationToken);
    }

    public async Task<SubscriberPwa> AddAsync(SubscriberPwa entity, CancellationToken cancellationToken)
    {
        var res = await context.SubscriberPwas.AddAsync(entity, cancellationToken);
        return res.Entity;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> DeleteAllAsync(IEnumerable<Guid>? ids, CancellationToken cancellationToken)
    {
        var idsArray = ids?.ToArray() ?? [];

        if (idsArray.Length == 0) return 0;

        return await context.SubscriberPwas
            .Where(f => idsArray.Contains(f.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> DeleteAllByUserIdAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (id == null) return 0;

        return await context.SubscriberPwas
            .Where(f => f.UserId == id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> DeleteAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (id == null) return 0;

        return await DeleteAllAsync([id.Value], cancellationToken);
    }
}
