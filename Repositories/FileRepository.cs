using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models;
using File = WebReader.Models.Entities.File;

namespace WebReader.Repositories;

public class FileRepository(ApplicationDbContext context) : IRepository<File>
{
    public async Task<File?> FirstOrDefaultAsync(Expression<Func<File, bool>> predicate,
        params Expression<Func<File, object>>[] includes)
    {
        IQueryable<File> query = context.Set<File>();

        foreach (var include in includes)
            query = query.Include(include);

        return await query.FirstOrDefaultAsync(predicate);
    }

    public async Task<IEnumerable<File>> AllAsync(Expression<Func<File, bool>> predicate,
        params Expression<Func<File, object>>[] includes)
    {
        IQueryable<File> query = context.Set<File>();

        foreach (var include in includes)
            query = query.Include(include);

        return await query.Where(predicate).ToListAsync();
    }

    public async Task<File> AddAsync(File entity)
    {
        var res = await context.Files.AddAsync(entity);
        return res.Entity;
    }

    public async Task<int> SaveChangesAsync()
    {
        return await context.SaveChangesAsync();
    }

    public void UpdateAll(IEnumerable<File> entities)
    {
        context.Files.UpdateRange(entities);
    }

    public async Task<IEnumerable<File>> GetAllAvailableObjectsInBucketAsync(string bucketName,
        IEnumerable<RoleType> roles)
    {
        var rolesList = roles.ToList();

        return await context.Files
            .Where(f => f.Bucket!.Name == bucketName && !f.Bucket.IsHidden &&
                        f.Bucket.AccessRoles.Intersect(rolesList).Any() &&
                        !f.IsHidden && f.AccessRoles.Intersect(rolesList).Any())
            .ToListAsync();
    }

    public async Task<IEnumerable<File>> GetAllAvailableObjectsInBucketTopLevelAsync(string bucketName,
        IEnumerable<RoleType> roles)
    {
        var rolesList = roles.ToList();

        //TODO: temporary.
        // var referencedIds = await context.Files
        //     .Where(f => f.Bucket!.Name == bucketName && !f.Bucket.IsHidden &&
        //                 f.Bucket.AccessRoles.Intersect(rolesList).Any() &&
        //                 !f.IsHidden && f.AccessRoles.Intersect(rolesList).Any() &&
        //                 f.NextPartId != null)
        //     .Select(f => f.NextPartId!.Value)
        //     .ToHashSetAsync();

        HashSet<Guid> referencedIds = [];

        return await context.Files
            .Where(f => f.Bucket!.Name == bucketName && !f.Bucket.IsHidden &&
                        f.Bucket.AccessRoles.Intersect(rolesList).Any() &&
                        !f.IsHidden && f.AccessRoles.Intersect(rolesList).Any() &&
                        !referencedIds.Contains(f.Id))
            .ToListAsync();
    }
}
