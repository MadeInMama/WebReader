using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models.Entities;

namespace WebReader.Repositories;

public class SubscriberTgRepository(ApplicationDbContext context) : IRepository<SubscriberTg>
{
    public async Task<SubscriberTg?> FirstOrDefaultAsync(Expression<Func<SubscriberTg, bool>> predicate,
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

        return await query.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public async Task<IEnumerable<SubscriberTg>> AllAsync(Expression<Func<SubscriberTg, bool>> predicate,
        CancellationToken cancellationToken, bool asNoTracking = false, ApplicationDbContext? ctx = null,
        params Expression<Func<SubscriberTg, object>>[] includes)
    {
        IQueryable<SubscriberTg> query = (ctx ?? context).Set<SubscriberTg>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

        return await query.Where(predicate).ToListAsync(cancellationToken);
    }

    public async Task<SubscriberTg> AddAsync(SubscriberTg entity, CancellationToken cancellationToken)
    {
        var res = await context.SubscriberTgs.AddAsync(entity, cancellationToken);
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

        return await context.SubscriberTgs
            .Where(f => idsArray.Contains(f.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> DeleteAllByUserIdAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (id == null) return 0;

        return await context.SubscriberTgs
            .Where(f => f.UserId == id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> DeleteByChatIdAsync(long? id, CancellationToken cancellationToken)
    {
        if (id == null) return 0;

        return await context.SubscriberTgs
            .Where(f => f.ChatId == id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> DeleteAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (id == null) return 0;

        return await DeleteAllAsync([id.Value], cancellationToken);
    }
}
