using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models;
using WebReader.Models.Extended;
using File = WebReader.Models.Entities.File;

namespace WebReader.Repositories;

public class FileRepository(ApplicationDbContext context) : IRepository<File>
{
    public async Task<File?> FirstOrDefaultAsync(Expression<Func<File, bool>> predicate,
        ApplicationDbContext? ctx,
        CancellationToken cancellationToken,
        bool asNoTracking,
        params Expression<Func<File, object>>[] includes)
    {
        IQueryable<File> query = (ctx ?? context).Set<File>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

        return await query.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public async Task<IEnumerable<File>> AllAsync(Expression<Func<File, bool>> predicate,
        CancellationToken cancellationToken,
        params Expression<Func<File, object>>[] includes)
    {
        IQueryable<File> query = context.Set<File>();

        foreach (var include in includes)
            query = query.Include(include);

        return await query.Where(predicate).ToListAsync(cancellationToken);
    }

    public async Task<File> AddAsync(File entity, CancellationToken cancellationToken)
    {
        var res = await context.Files.AddAsync(entity, cancellationToken);
        return res.Entity;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await context.SaveChangesAsync(cancellationToken);
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
        IEnumerable<RoleType> roles, CancellationToken cancellationToken)
    {
        var rolesList = roles.ToList();

        return await context.Files
            .Where(f => f.Bucket!.Name == bucketName && !f.Bucket.IsHidden &&
                        f.Bucket.AccessRoles.Intersect(rolesList).Any() &&
                        !f.IsHidden && f.AccessRoles.Intersect(rolesList).Any())
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<File>> GetAllAvailableObjectsWithPartsAsync(Guid fileId,
        CancellationToken cancellationToken)
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
                                    INNER JOIN file_chain AS fc ON f."Id" = fc."Id"
                           ORDER BY fc.Level
                           """;

        return await context.Files.FromSqlRaw(sql, fileId).ToListAsync(cancellationToken);
    }

    public async Task<File> GetHeadedPartedObjectAsync(Guid fileId, CancellationToken cancellationToken)
    {
        const string sql = """
                           WITH RECURSIVE find_head AS (
                               SELECT "Id","NextPartId",1 AS depth
                               FROM "Files"
                               WHERE "Id" = {0}

                               UNION ALL

                               SELECT f."Id",f."NextPartId", fh.depth + 1
                               FROM "Files" f
                                        INNER JOIN find_head fh ON f."NextPartId" = fh."Id"
                               WHERE fh.depth < 100000
                           )
                           SELECT f.*
                           FROM "Files" f
                                    INNER JOIN find_head AS fh ON f."Id" = fh."Id"
                           ORDER BY fh.depth DESC
                           LIMIT 1
                           """;

        return await context.Files.FromSqlRaw(sql, fileId).SingleAsync(cancellationToken);
    }

    public async Task<IEnumerable<File>> GetAllAvailableObjectsInBucketAsync(Guid bucketId,
        IEnumerable<RoleType> roles, CancellationToken cancellationToken)
    {
        var rolesList = roles.ToList();

        return await context.Files
            .Where(f => f.BucketId == bucketId && !f.Bucket.IsHidden &&
                        f.Bucket.AccessRoles.Intersect(rolesList).Any() &&
                        !f.IsHidden && f.AccessRoles.Intersect(rolesList).Any())
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<ExtendedFile>> GetAllAvailableObjectsInBucketTopLevelAsync(Guid bucketId,
        IEnumerable<RoleType> roles, CancellationToken cancellationToken)
    {
        return await context.Database.SqlQuery<ExtendedFile>($"""
                                                              WITH RECURSIVE file_chain AS (
                                                                  SELECT
                                                                      f."Id" AS root_id,
                                                                      f."Id",
                                                                      f."NextPartId",
                                                                      f."Size"
                                                                  FROM "Files" f
                                                                      JOIN "Buckets" AS b ON b."Id" = f."BucketId"
                                                                  WHERE b."Id" = {bucketId} AND
                                                                        f."AccessRoles" && {roles} AND
                                                                      NOT EXISTS (
                                                                      SELECT 1 FROM "Files" AS child WHERE child."NextPartId" = f."Id"
                                                                  )

                                                                  UNION ALL

                                                                  SELECT
                                                                      current_file.root_id,
                                                                      next_file."Id",
                                                                      next_file."NextPartId",
                                                                      next_file."Size"
                                                                  FROM "Files" AS next_file
                                                                           JOIN file_chain AS current_file ON next_file."Id" = current_file."NextPartId"
                                                              )
                                                              SELECT
                                                                  f.*,
                                                                  stats.total_count AS "TotalCount",
                                                                  stats.total_size AS "TotalSize"
                                                              FROM (
                                                                       SELECT
                                                                           root_id,
                                                                           COUNT(*) AS total_count,
                                                                           SUM(COALESCE("Size", 0)) AS total_size
                                                                       FROM file_chain
                                                                       GROUP BY root_id
                                                                   ) stats
                                                                       JOIN "Files" f ON f."Id" = stats.root_id;
                                                              """).ToListAsync(cancellationToken);
    }

    public async Task DeleteAllAsync(IEnumerable<Guid>? ids, CancellationToken cancellationToken)
    {
        var idsArray = ids?.ToArray() ?? [];

        if (idsArray.Length == 0) return;

        await context.Files
            .Where(r => idsArray.Contains(r.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (id == null) return;

        await DeleteAllAsync([id.Value], cancellationToken);
    }
}
