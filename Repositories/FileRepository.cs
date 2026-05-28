using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using WebReader.Data;
using WebReader.Models;
using WebReader.Models.Entities;
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
        CancellationToken cancellationToken, bool asNoTracking = false,
        params Expression<Func<File, object>>[] includes)
    {
        IQueryable<File> query = context.Set<File>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

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

    public void Attach(File entity)
    {
        context.Files.Attach(entity);
    }

    public void AttachAll(IEnumerable<File> entities)
    {
        foreach (var entity in entities) Attach(entity);
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
        var fileEntity = context.Model.FindEntityType(typeof(File))!;

        var fileIdProperty = fileEntity.FindProperty(nameof(File.Id))!;
        var fileNextPartIdProperty = fileEntity.FindProperty(nameof(File.NextPartId))!;

        var sql =
            $"""
             WITH RECURSIVE file_chain AS (
                 SELECT "{fileIdProperty.GetColumnName()}","{fileNextPartIdProperty.GetColumnName()}", 0 AS Level
                 FROM "{fileEntity.GetTableName()}"
                 WHERE "{fileIdProperty.GetColumnName()}" = '{fileId}'

                 UNION ALL

                 SELECT f."{fileIdProperty.GetColumnName()}", f."{fileNextPartIdProperty.GetColumnName()}", fc.Level + 1
                 FROM "{fileEntity.GetTableName()}" f
                     INNER JOIN file_chain fc ON f."{fileIdProperty.GetColumnName()}" = fc."{fileNextPartIdProperty.GetColumnName()}"
             )
             SELECT f.*
             FROM "{fileEntity.GetTableName()}" f
                      INNER JOIN file_chain AS fc ON f."{fileIdProperty.GetColumnName()}" = fc."{fileIdProperty.GetColumnName()}"
             ORDER BY fc.Level
             """;

        return await context.Files.FromSqlRaw(sql).ToListAsync(cancellationToken);
    }

    public async Task<File> GetHeadedPartedObjectAsync(Guid fileId, CancellationToken cancellationToken)
    {
        var fileEntity = context.Model.FindEntityType(typeof(File))!;

        var fileIdProperty = fileEntity.FindProperty(nameof(File.Id))!;
        var fileNextPartIdProperty = fileEntity.FindProperty(nameof(File.NextPartId))!;

        var sql = $"""
                   WITH RECURSIVE find_head AS (
                       SELECT "{fileIdProperty.GetColumnName()}","{fileNextPartIdProperty.GetColumnName()}",1 AS depth
                       FROM "{fileEntity.GetTableName()}"
                       WHERE "{fileIdProperty.GetColumnName()}" = '{fileId}'

                       UNION ALL

                       SELECT f."{fileIdProperty.GetColumnName()}",f."{fileNextPartIdProperty.GetColumnName()}", fh.depth + 1
                       FROM "{fileEntity.GetTableName()}" f
                                INNER JOIN find_head fh ON f."{fileNextPartIdProperty.GetColumnName()}" = fh."Id"
                       WHERE fh.depth < 100000
                   )
                   SELECT f.*
                   FROM "{fileEntity.GetTableName()}" f
                            INNER JOIN find_head AS fh ON f."{fileIdProperty.GetColumnName()}" = fh."{fileIdProperty.GetColumnName()}"
                   ORDER BY fh.depth DESC
                   LIMIT 1
                   """;

        return await context.Files.FromSqlRaw(sql).SingleAsync(cancellationToken);
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
        var joinedRoles = string.Join(",", roles.Select(f => (int)f));

        var bucketEntity = context.Model.FindEntityType(typeof(Bucket))!;
        var fileEntity = context.Model.FindEntityType(typeof(File))!;

        var bucketIdProperty = bucketEntity.FindProperty(nameof(Bucket.Id))!;
        var bucketAccessRolesProperty = bucketEntity.FindProperty(nameof(Bucket.AccessRoles))!;

        var fileIdProperty = fileEntity.FindProperty(nameof(File.Id))!;
        var fileBucketIdProperty = fileEntity.FindProperty(nameof(File.BucketId))!;
        var fileAccessRolesProperty = fileEntity.FindProperty(nameof(File.AccessRoles))!;
        var fileNextPartIdProperty = fileEntity.FindProperty(nameof(File.NextPartId))!;
        var fileSizeProperty = fileEntity.FindProperty(nameof(File.Size))!;

        var sql = $"""
                   WITH RECURSIVE file_chain AS (
                       SELECT
                           f."{fileIdProperty.GetColumnName()}" AS root_id,
                           f."{fileIdProperty.GetColumnName()}",
                           f."{fileNextPartIdProperty.GetColumnName()}",
                           f."{fileSizeProperty.GetColumnName()}"
                       FROM "{fileEntity.GetTableName()}" f
                           JOIN "{bucketEntity.GetTableName()}" AS b
                           ON b."{bucketIdProperty.GetColumnName()}" = f."{fileBucketIdProperty.GetColumnName()}"
                       WHERE b."{bucketIdProperty.GetColumnName()}" = '{bucketId}' AND
                             b."{bucketAccessRolesProperty.GetColumnName()}" && ARRAY[{joinedRoles}] AND
                             f."{fileAccessRolesProperty.GetColumnName()}" && ARRAY[{joinedRoles}] AND
                           NOT EXISTS (
                           SELECT 1 FROM "{fileEntity.GetTableName()}" AS child
                           WHERE child."{fileNextPartIdProperty.GetColumnName()}" = f."{fileIdProperty.GetColumnName()}"
                       )

                       UNION ALL

                       SELECT
                           current_file.root_id,
                           next_file."{fileIdProperty.GetColumnName()}",
                           next_file."{fileNextPartIdProperty.GetColumnName()}",
                           next_file."{fileSizeProperty.GetColumnName()}"
                       FROM "{fileEntity.GetTableName()}" AS next_file
                                JOIN file_chain AS current_file
                                ON next_file."{fileIdProperty.GetColumnName()}" = current_file."{fileNextPartIdProperty.GetColumnName()}"
                   )
                   SELECT
                       f.*,
                       stats.total_count AS "TotalCount",
                       stats.total_size AS "TotalSize"
                   FROM (
                            SELECT
                                root_id,
                                COUNT(*) AS total_count,
                                SUM(COALESCE("{fileSizeProperty.GetColumnName()}", 0)) AS total_size
                            FROM file_chain
                            GROUP BY root_id
                        ) stats
                            JOIN "{fileEntity.GetTableName()}" f ON f."{fileIdProperty.GetColumnName()}" = stats.root_id;
                   """;

        return await context.Database.SqlQueryRaw<ExtendedFile>(sql).ToListAsync(cancellationToken);
    }

    public async Task DeleteAllAsync(IEnumerable<Guid>? ids, CancellationToken cancellationToken)
    {
        var idsArray = ids?.ToArray() ?? [];

        if (idsArray.Length == 0) return;

        var joinedIds = string.Join(",", idsArray.Select(f => $"'{f.ToString()}'"));

        var fileEntity = context.Model.FindEntityType(typeof(File))!;
        var readingEntity = context.Model.FindEntityType(typeof(UserReading))!;

        var fileIdProperty = fileEntity.FindProperty(nameof(File.Id))!;

        var readingFileIdProperty = readingEntity.FindProperty(nameof(UserReading.FileId))!;

        var sql = $"""
                   DELETE FROM "{fileEntity.GetTableName()}"
                   WHERE "{fileIdProperty.GetColumnName()}" IN ({joinedIds});

                   DELETE FROM "{readingEntity.GetTableName()}"
                   WHERE "{readingFileIdProperty.GetColumnName()}" IN ({joinedIds});
                   """;

        await context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    public async Task DeleteAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (id == null) return;

        await DeleteAllAsync([id.Value], cancellationToken);
    }
}
