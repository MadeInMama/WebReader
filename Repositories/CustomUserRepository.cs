using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models.Entities;

namespace WebReader.Repositories;

public class CustomUserRepository(ApplicationDbContext context) : IRepository<CustomUser>
{
    public async Task<CustomUser?> FirstOrDefaultAsync(Expression<Func<CustomUser, bool>> predicate)
    {
        return await context.Users.FirstOrDefaultAsync(predicate);
    }

    public Task<IEnumerable<CustomUser>> AllAsync(Expression<Func<CustomUser, bool>> predicate,
        params Expression<Func<CustomUser, object>>[] includes)
    {
        throw new NotImplementedException();
    }

    public async Task<CustomUser> AddAsync(CustomUser entity)
    {
        var res = await context.Users.AddAsync(entity);
        await context.SaveChangesAsync();
        return res.Entity;
    }
}