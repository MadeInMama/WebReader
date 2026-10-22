using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models.Entities;

namespace WebReader.Repositories;

public class RequestAttemptsLogRepository(ApplicationDbContext context) : IRepository<RequestAttemptsLog>
{
    public async Task<RequestAttemptsLog?> FirstOrDefaultAsync(Expression<Func<RequestAttemptsLog, bool>> predicate,
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

        return await query.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public async Task<IEnumerable<RequestAttemptsLog>> AllAsync(Expression<Func<RequestAttemptsLog, bool>> predicate,
        CancellationToken cancellationToken, bool asNoTracking = false, ApplicationDbContext? ctx = null,
        params Expression<Func<RequestAttemptsLog, object>>[] includes)
    {
        IQueryable<RequestAttemptsLog> query = (ctx ?? context).Set<RequestAttemptsLog>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

        return await query.Where(predicate).ToListAsync(cancellationToken);
    }

    public async Task<RequestAttemptsLog> AddAsync(RequestAttemptsLog entity, CancellationToken cancellationToken)
    {
        var res = await context.RequestAttemptsLogs.AddAsync(entity, cancellationToken);
        return res.Entity;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsAllowed(string ip, CancellationToken cancellationToken)
    {
        const int maxAttemptsCountLastHour = 10;

        var attemptsCountLastHour = await context.RequestAttemptsLogs
            .Where(f => f.Ip == ip && !f.IsAuthenticated && f.CreatedDate > DateTimeOffset.UtcNow.AddHours(-1))
            .CountAsync(cancellationToken);

        return attemptsCountLastHour <= maxAttemptsCountLastHour;
    }

    public async Task<int> DeleteAllOlderThenAsync(DateTimeOffset createdDate, CancellationToken cancellationToken)
    {
        return await context.RequestAttemptsLogs
            .Where(f => f.CreatedDate < createdDate)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
