using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models.Entities;

namespace WebReader.Repositories;

public class CustomUserRepository(ApplicationDbContext context) : IRepository<CustomUser>
{
    public async Task<CustomUser?> FirstOrDefaultAsync(Expression<Func<CustomUser, bool>> predicate,
        ApplicationDbContext? ctx,
        params Expression<Func<CustomUser, object>>[] includes)
    {
        IQueryable<CustomUser> query = (ctx ?? context).Set<CustomUser>();

        foreach (var include in includes)
            query = query.Include(include);

        return await query.FirstOrDefaultAsync(predicate);
    }

    public Task<IEnumerable<CustomUser>> AllAsync(Expression<Func<CustomUser, bool>> predicate,
        params Expression<Func<CustomUser, object>>[] includes)
    {
        throw new NotImplementedException();
    }

    public async Task<CustomUser> AddAsync(CustomUser entity)
    {
        var res = await context.Users.AddAsync(entity);
        return res.Entity;
    }

    public async Task<int> SaveChangesAsync()
    {
        return await context.SaveChangesAsync();
    }

    public void UpdateAsync(CustomUser entity)
    {
        context.Users.Update(entity);
    }

    public async Task DeleteAllAsync(IEnumerable<Guid>? ids)
    {
        var idsArray = ids?.ToArray() ?? [];

        if (idsArray.Length == 0) return;

        await context.Users
            .Where(r => idsArray.Contains(r.Id))
            .ExecuteDeleteAsync();
    }

    public async Task DeleteAsync(Guid? id)
    {
        if (id == null) return;

        await DeleteAllAsync([id.Value]);
    }
}
