using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models.Entities;

namespace WebReader.Repositories;

public class SubscriberTgRepository(ApplicationDbContext context) : IRepository<SubscriberTg>
{
    public Task<SubscriberTg?> FirstOrDefaultAsync(Expression<Func<SubscriberTg, bool>> predicate,
        ApplicationDbContext? ctx,
        CancellationToken cancellationToken,
        bool asNoTracking = false,
        params Expression<Func<SubscriberTg, object>>[] includes)
    {
        IQueryable<SubscriberTg> query = (ctx ?? context).Set<SubscriberTg>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

        return query.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public Task<List<SubscriberTg>> AllAsync(
        Expression<Func<SubscriberTg, bool>> predicate,
        CancellationToken cancellationToken,
        bool asNoTracking = false,
        ApplicationDbContext? ctx = null,
        int take = int.MaxValue,
        int skip = 0,
        params Expression<Func<SubscriberTg, object>>[] includes)
    {
        IQueryable<SubscriberTg> query = (ctx ?? context).Set<SubscriberTg>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

        return query.Where(predicate).OrderBy(f => f.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        Expression<Func<SubscriberTg, bool>> predicate,
        CancellationToken cancellationToken,
        ApplicationDbContext? ctx = null,
        int take = int.MaxValue,
        int skip = 0,
        params Expression<Func<SubscriberTg, object>>[] includes)
    {
        IQueryable<SubscriberTg> query = (ctx ?? context).Set<SubscriberTg>();

        foreach (var include in includes)
            query = query.Include(include);

        query = query.AsNoTracking();

        return query.Where(predicate).OrderBy(f => f.Id).CountAsync(cancellationToken);
    }

    public async Task<SubscriberTg> AddAsync(SubscriberTg entity, CancellationToken cancellationToken)
    {
        var res = await context.SubscriberTgs.AddAsync(entity, cancellationToken);
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

        return context.SubscriberTgs
            .Where(f => idsArray.Contains(f.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<int> DeleteAllByUserIdAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (id == null) return Task.FromResult(0);

        return context.SubscriberTgs
            .Where(f => f.UserId == id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<int> DeleteByChatIdAsync(long? id, CancellationToken cancellationToken)
    {
        if (id == null) return Task.FromResult(0);

        return context.SubscriberTgs
            .Where(f => f.ChatId == id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<int> DeleteAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (id == null) return Task.FromResult(0);

        return DeleteAllAsync([id.Value], cancellationToken);
    }
}
