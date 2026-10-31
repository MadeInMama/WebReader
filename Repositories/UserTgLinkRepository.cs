using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models.Entities;

namespace WebReader.Repositories;

public class UserTgLinkRepository(ApplicationDbContext context) : IRepository<UserTgLink>
{
    public async Task<UserTgLink?> FirstOrDefaultAsync(Expression<Func<UserTgLink, bool>> predicate,
        ApplicationDbContext? ctx,
        CancellationToken cancellationToken,
        bool asNoTracking = false,
        params Expression<Func<UserTgLink, object>>[] includes)
    {
        IQueryable<UserTgLink> query = (ctx ?? context).Set<UserTgLink>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public async Task<IEnumerable<UserTgLink>> AllAsync(
        Expression<Func<UserTgLink, bool>> predicate,
        CancellationToken cancellationToken,
        bool asNoTracking = false,
        ApplicationDbContext? ctx = null,
        int take = int.MaxValue,
        int skip = 0,
        params Expression<Func<UserTgLink, object>>[] includes)
    {
        IQueryable<UserTgLink> query = (ctx ?? context).Set<UserTgLink>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

        return await query.Where(predicate).OrderBy(f => f.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
    }

    public async Task<int> CountAsync(
        Expression<Func<UserTgLink, bool>> predicate,
        CancellationToken cancellationToken,
        bool asNoTracking = false,
        ApplicationDbContext? ctx = null,
        int take = int.MaxValue,
        int skip = 0,
        params Expression<Func<UserTgLink, object>>[] includes)
    {
        IQueryable<UserTgLink> query = (ctx ?? context).Set<UserTgLink>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

        return await query.Where(predicate).OrderBy(f => f.Id).CountAsync(cancellationToken);
    }

    public async Task<UserTgLink> AddAsync(UserTgLink entity, CancellationToken cancellationToken)
    {
        var res = await context.UserTgLinks.AddAsync(entity, cancellationToken);
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

        return await context.UserTgLinks
            .Where(f => idsArray.Contains(f.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> DeleteAllUsedOrExpiredAsync(CancellationToken cancellationToken)
    {
        return await context.UserTgLinks
            .Where(f => f.IsUsed || f.ExpiresAt >= DateTimeOffset.UtcNow)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> DeleteAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (id == null) return 0;

        return await DeleteAllAsync([id.Value], cancellationToken);
    }
}
