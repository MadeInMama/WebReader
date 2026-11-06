using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models.Entities;

namespace WebReader.Repositories;

public class NotificationRepository(ApplicationDbContext context) : IRepository<Notification>
{
    public Task<Notification?> FirstOrDefaultAsync(
        Expression<Func<Notification, bool>> predicate,
        ApplicationDbContext? ctx,
        CancellationToken cancellationToken,
        bool asNoTracking = false,
        params Expression<Func<Notification, object>>[] includes)
    {
        IQueryable<Notification> query = (ctx ?? context).Set<Notification>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

        return query.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public Task<List<Notification>> AllAsync(
        Expression<Func<Notification, bool>> predicate,
        CancellationToken cancellationToken,
        bool asNoTracking = false,
        ApplicationDbContext? ctx = null,
        int take = int.MaxValue,
        int skip = 0,
        params Expression<Func<Notification, object>>[] includes)
    {
        IQueryable<Notification> query = (ctx ?? context).Set<Notification>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

        return query.Where(predicate).OrderBy(f => f.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        Expression<Func<Notification, bool>> predicate,
        CancellationToken cancellationToken,
        ApplicationDbContext? ctx = null,
        int take = int.MaxValue,
        int skip = 0,
        params Expression<Func<Notification, object>>[] includes)
    {
        IQueryable<Notification> query = (ctx ?? context).Set<Notification>();

        foreach (var include in includes)
            query = query.Include(include);

        query = query.AsNoTracking();

        return query.Where(predicate).OrderBy(f => f.Id).CountAsync(cancellationToken);
    }

    public async Task<Notification> AddAsync(Notification entity,
        CancellationToken cancellationToken)
    {
        var res = await context.Notifications.AddAsync(entity, cancellationToken);
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

        return context.Notifications
            .Where(f => idsArray.Contains(f.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<int> DeleteAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (id == null) return Task.FromResult(0);

        return DeleteAllAsync([id.Value], cancellationToken);
    }

    public Task<int> DeleteAllSentAndOlderThenAsync(DateTimeOffset createdDate,
        CancellationToken cancellationToken)
    {
        return context.Notifications
            .Where(f => f.IsSent && f.SentAt < createdDate)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
