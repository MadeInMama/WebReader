using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models;
using File = WebReader.Models.Entities.File;

namespace WebReader.Repositories;

public class FileRepository(ApplicationDbContext context) : IRepository<File>
{
    public async Task<File?> FirstOrDefaultAsync(Expression<Func<File, bool>> predicate,
        ApplicationDbContext? ctx,
        params Expression<Func<File, object>>[] includes)
    {
        IQueryable<File> query = (ctx ?? context).Set<File>();

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

    public File Update(File entity)
    {
        var res = context.Files.Update(entity);
        return res.Entity;
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

    public async Task<IEnumerable<File>> GetAllAvailableObjectsWithPartsAsync(Guid fileId)
    {
        const string sql = """
                           WITH RECURSIVE file_chain AS (
                               SELECT "Id", "NextPartId", 0 AS Level
                               FROM "Files"
                               WHERE "Id" = {0}

                               UNION ALL

                               SELECT f."Id", f."NextPartId", fc.Level + 1
                               FROM "Files" f
                                   INNER JOIN file_chain fc ON f."Id" = fc."NextPartId"
                           )
                           SELECT f.*
                           FROM "Files" f
                                    INNER JOIN file_chain fc ON f."Id" = fc."Id"
                           ORDER BY fc.Level
                           """;

        return await context.Files.FromSqlRaw(sql, fileId).ToListAsync();
    }

    public async Task<File> GetHeadedPartedObjectAsync(Guid fileId)
    {
        const string sql = """
                           WITH RECURSIVE find_head AS (
                               SELECT "Id","BucketId","Name","CustomName","Type","Size",
                                      "AccessRoles","IsHidden","IsAvailable","CreatedDate",
                                      "UpdatedDate","NextPartId","CurrentPartName",1 AS depth
                               FROM "Files"
                               WHERE "Id" = {0}

                               UNION ALL

                               SELECT f."Id",f."BucketId",f."Name",f."CustomName",f."Type",f."Size",
                                      f."AccessRoles",f."IsHidden",f."IsAvailable",f."CreatedDate",
                                      f."UpdatedDate",f."NextPartId",f."CurrentPartName", fh.depth + 1
                               FROM "Files" f
                                        INNER JOIN find_head fh ON f."NextPartId" = fh."Id"
                               WHERE fh.depth < 100000
                           )
                           SELECT *
                           FROM find_head
                           ORDER BY depth DESC
                           LIMIT 1
                           """;

        return await context.Files.FromSqlRaw(sql, fileId).SingleAsync();
    }

    public async Task<IEnumerable<File>> GetAllAvailableObjectsInBucketAsync(Guid bucketId,
        IEnumerable<RoleType> roles)
    {
        var rolesList = roles.ToList();

        return await context.Files
            .Where(f => f.BucketId == bucketId && !f.Bucket.IsHidden &&
                        f.Bucket.AccessRoles.Intersect(rolesList).Any() &&
                        !f.IsHidden && f.AccessRoles.Intersect(rolesList).Any())
            .ToListAsync();
    }

    public async Task<IEnumerable<File>> GetAllAvailableObjectsInBucketTopLevelAsync(string bucketName,
        IEnumerable<RoleType> roles)
    {
        var rolesList = roles.ToList();

        var referencedIds = await context.Files
            .Where(f => f.Bucket!.Name == bucketName && !f.Bucket.IsHidden &&
                        f.Bucket.AccessRoles.Intersect(rolesList).Any() &&
                        !f.IsHidden && f.AccessRoles.Intersect(rolesList).Any() &&
                        f.NextPartId != null)
            .Select(f => f.NextPartId!.Value)
            .ToHashSetAsync();

        return await context.Files
            .Where(f => f.Bucket!.Name == bucketName && !f.Bucket.IsHidden &&
                        f.Bucket.AccessRoles.Intersect(rolesList).Any() &&
                        !f.IsHidden && f.AccessRoles.Intersect(rolesList).Any() &&
                        !referencedIds.Contains(f.Id))
            .ToListAsync();
    }
}
