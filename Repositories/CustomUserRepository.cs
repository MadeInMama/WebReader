using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models.Entities;

namespace WebReader.Repositories;

public class CustomUserRepository(ApplicationDbContext context) : IRepository<CustomUser>
{
    public async Task<CustomUser?> FirstOrDefaultAsync(Expression<Func<CustomUser, bool>> predicate,
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

        return await query.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public Task<IEnumerable<CustomUser>> AllAsync(Expression<Func<CustomUser, bool>> predicate,
        CancellationToken cancellationToken,
        params Expression<Func<CustomUser, object>>[] includes)
    {
        throw new NotImplementedException();
    }

    public async Task<CustomUser> AddAsync(CustomUser entity, CancellationToken cancellationToken)
    {
        var res = await context.Users.AddAsync(entity, cancellationToken);
        return res.Entity;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await context.SaveChangesAsync(cancellationToken);
    }

    public void UpdateAsync(CustomUser entity)
    {
        context.Users.Update(entity);
    }

    public async Task DeleteAllAsync(IEnumerable<Guid>? ids, CancellationToken cancellationToken)
    {
        var idsArray = ids?.ToArray() ?? [];

        if (idsArray.Length == 0) return;

        await context.Users
            .Where(r => idsArray.Contains(r.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (id == null) return;

        await DeleteAllAsync([id.Value], cancellationToken);
    }
}
