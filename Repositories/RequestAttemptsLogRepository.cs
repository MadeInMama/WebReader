using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models.Entities;

namespace WebReader.Repositories;

public class RequestAttemptsLogRepository(ApplicationDbContext context) : IRepository<RequestAttemptsLog>
{
    public Task<RequestAttemptsLog?> FirstOrDefaultAsync(Expression<Func<RequestAttemptsLog, bool>> predicate,
        ApplicationDbContext? ctx,
        CancellationToken cancellationToken,
        bool asNoTracking,
        params Expression<Func<RequestAttemptsLog, object>>[] includes)
    {
        IQueryable<RequestAttemptsLog> query = (ctx ?? context).Set<RequestAttemptsLog>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

        return query.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public Task<List<RequestAttemptsLog>> AllAsync(
        Expression<Func<RequestAttemptsLog, bool>> predicate,
        CancellationToken cancellationToken,
        bool asNoTracking = false,
        ApplicationDbContext? ctx = null,
        int take = int.MaxValue,
        int skip = 0,
        params Expression<Func<RequestAttemptsLog, object>>[] includes)
    {
        IQueryable<RequestAttemptsLog> query = (ctx ?? context).Set<RequestAttemptsLog>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

        return query.Where(predicate).OrderBy(f => f.Ip).Skip(skip).Take(take).ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(Expression<Func<RequestAttemptsLog, bool>> predicate,
        CancellationToken cancellationToken,
        ApplicationDbContext? ctx = null,
        int take = int.MaxValue,
        int skip = 0,
        params Expression<Func<RequestAttemptsLog, object>>[] includes)
    {
        IQueryable<RequestAttemptsLog> query = (ctx ?? context).Set<RequestAttemptsLog>();

        foreach (var include in includes)
            query = query.Include(include);

        query = query.AsNoTracking();

        return query.Where(predicate).OrderBy(f => f.Ip).CountAsync(cancellationToken);
    }

    public async Task<RequestAttemptsLog> AddAsync(RequestAttemptsLog entity, CancellationToken cancellationToken)
    {
        var res = await context.RequestAttemptsLogs.AddAsync(entity, cancellationToken);
        return res.Entity;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsAllowed(string ip, CancellationToken cancellationToken)
    {
        const int maxAttemptsCountLastHour = 10;

        var attemptsCountLastHour = await context.RequestAttemptsLogs
            .Where(f => f.Ip == ip && !f.IsAuthenticated && f.CreatedDate > DateTimeOffset.UtcNow.AddHours(-1))
            .CountAsync(cancellationToken);

        return attemptsCountLastHour <= maxAttemptsCountLastHour;
    }

    public Task<int> DeleteAllOlderThenAsync(DateTimeOffset createdDate, CancellationToken cancellationToken)
    {
        return context.RequestAttemptsLogs
            .Where(f => f.CreatedDate < createdDate)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
