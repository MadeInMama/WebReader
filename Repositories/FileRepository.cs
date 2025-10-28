using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models;
using File = WebReader.Models.Entities.File;

namespace WebReader.Repositories;

public class FileRepository(ApplicationDbContext context) : IRepository<File>
{
    public async Task<File?> FirstOrDefaultAsync(Expression<Func<File, bool>> predicate)
    {
        return await context.Files.FirstOrDefaultAsync(predicate);
    }

    public Task<IEnumerable<File>> AllAsync(Expression<Func<File, bool>> predicate)
    {
        throw new NotImplementedException();
    }

    public Task<File> AddAsync(File entity)
    {
        throw new NotImplementedException();
    }

    public async Task<IEnumerable<string>> GetAllAvailableBucketsAsync(IEnumerable<RoleType> roles)
    {
        return await context.Files
            .Where(f => !f.IsHidden && f.AccessRoles.Intersect(roles).Any())
            .Select(f => f.Bucket)
            .Distinct()
            .ToListAsync();
    }

    public async Task<IEnumerable<string>> GetAllAvailableObjectsInBucketAsync(string bucketId,
        IEnumerable<RoleType> roles)
    {
        return await context.Files
            .Where(f => f.Bucket == bucketId && !f.IsHidden && f.AccessRoles.Intersect(roles).Any())
            .Select(f => f.Object)
            .Distinct()
            .ToListAsync();
    }
}