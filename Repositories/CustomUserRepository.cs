using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models.Entities;

namespace WebReader.Repositories;

public class CustomUserRepository(ApplicationDbContext context) : IRepository<CustomUser>
{
    public Task<CustomUser?> FirstOrDefaultAsync(Expression<Func<CustomUser, bool>> predicate,
        ApplicationDbContext? ctx,
        CancellationToken cancellationToken,
        bool asNoTracking = false,
        params Expression<Func<CustomUser, object>>[] includes)
    {
        IQueryable<CustomUser> query = (ctx ?? context).Set<CustomUser>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

        return query.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public Task<List<CustomUser>> AllAsync(
        Expression<Func<CustomUser, bool>> predicate,
        CancellationToken cancellationToken,
        bool asNoTracking = false,
        ApplicationDbContext? ctx = null,
        int take = int.MaxValue,
        int skip = 0,
        params Expression<Func<CustomUser, object>>[] includes)
    {
        IQueryable<CustomUser> query = (ctx ?? context).Set<CustomUser>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

        return query.Where(predicate).OrderBy(f => f.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        Expression<Func<CustomUser, bool>> predicate,
        CancellationToken cancellationToken,
        ApplicationDbContext? ctx = null,
        int take = int.MaxValue,
        int skip = 0,
        params Expression<Func<CustomUser, object>>[] includes)
    {
        IQueryable<CustomUser> query = (ctx ?? context).Set<CustomUser>();

        foreach (var include in includes)
            query = query.Include(include);

        query = query.AsNoTracking();

        return query.Where(predicate).OrderBy(f => f.Id).CountAsync(cancellationToken);
    }

    public async Task<CustomUser> AddAsync(CustomUser entity, CancellationToken cancellationToken)
    {
        var res = await context.Users.AddAsync(entity, cancellationToken);
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

        return context.Users
            .Where(r => idsArray.Contains(r.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<int> DeleteAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (id == null) return Task.FromResult(0);

        return DeleteAllAsync([id.Value], cancellationToken);
    }
}
