using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using WebReader.Data;
using WebReader.Models;
using WebReader.Models.Entities;
using WebReader.Models.Extended;
using File = WebReader.Models.Entities.File;

namespace WebReader.Repositories;

public class FileRepository(ApplicationDbContext context) : IRepository<File>
{
    public Task<File?> FirstOrDefaultAsync(Expression<Func<File, bool>> predicate,
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

        return query.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public Task<List<File>> AllAsync(
        Expression<Func<File, bool>> predicate,
        CancellationToken cancellationToken,
        bool asNoTracking = false,
        ApplicationDbContext? ctx = null,
        int take = int.MaxValue,
        int skip = 0,
        params Expression<Func<File, object>>[] includes)
    {
        IQueryable<File> query = (ctx ?? context).Set<File>();

        foreach (var include in includes)
            query = query.Include(include);

        if (asNoTracking)
            query = query.AsNoTracking();

        return query.Where(predicate).OrderBy(f => f.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(
        Expression<Func<File, bool>> predicate,
        CancellationToken cancellationToken,
        ApplicationDbContext? ctx = null,
        int take = int.MaxValue,
        int skip = 0,
        params Expression<Func<File, object>>[] includes)
    {
        IQueryable<File> query = (ctx ?? context).Set<File>();

        foreach (var include in includes)
            query = query.Include(include);

        query = query.AsNoTracking();

        return query.Where(predicate).OrderBy(f => f.Id).CountAsync(cancellationToken);
    }

    public async Task<File> AddAsync(File entity, CancellationToken cancellationToken)
    {
        var res = await context.Files.AddAsync(entity, cancellationToken);
        return res.Entity;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return context.SaveChangesAsync(cancellationToken);
    }

    public void Attach(File entity)
    {
        context.Files.Attach(entity);
    }

    public void AttachAll(IEnumerable<File> entities)
    {
        foreach (var entity in entities) Attach(entity);
    }

    public Task<List<File>> GetAllAvailableObjectsInBucketAsync(string bucketName,
        IEnumerable<RoleType> roles, CancellationToken cancellationToken)
    {
        var rolesList = roles.ToList();

        return context.Files
            .Where(f => f.Bucket!.Name == bucketName && !f.Bucket.IsHidden &&
                        f.Bucket.AccessRoles.Intersect(rolesList).Any() &&
                        !f.IsHidden && f.AccessRoles.Intersect(rolesList).Any())
            .ToListAsync(cancellationToken);
    }

    public Task<List<File>> GetAllAvailableObjectsWithPartsAsync(Guid fileId, CancellationToken cancellationToken,
        int offset = 0, int limit = int.MaxValue)
    {
        var fileEntity = context.Model.FindEntityType(typeof(File))!;
        var tableName = fileEntity.GetTableName();
        var idCol = fileEntity.FindProperty(nameof(File.Id))!.GetColumnName();
        var nextPartIdCol = fileEntity.FindProperty(nameof(File.NextPartId))!.GetColumnName();

        var maxDepth = offset + limit;

        var sql = $"""
                   WITH RECURSIVE file_chain AS (
                       SELECT "{idCol}", "{nextPartIdCol}", 0 AS Level
                       FROM "{tableName}"
                       WHERE "{idCol}" = @fileId

                       UNION ALL

                       SELECT f."{idCol}", f."{nextPartIdCol}", fc.Level + 1
                       FROM "{tableName}" f
                       INNER JOIN file_chain fc ON f."{idCol}" = fc."{nextPartIdCol}"
                       WHERE fc.Level < @maxDepth
                   )
                   SELECT f.*
                   FROM "{tableName}" f
                   INNER JOIN file_chain fc ON f."{idCol}" = fc."{idCol}"
                   ORDER BY fc.Level
                   LIMIT @limit OFFSET @offset
                   """;

        var parameters = new object[]
        {
            new NpgsqlParameter("fileId", fileId),
            new NpgsqlParameter("maxDepth", maxDepth),
            new NpgsqlParameter("limit", limit),
            new NpgsqlParameter("offset", offset)
        };

        return context.Files
            .FromSqlRaw(sql, parameters)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<File> GetHeadedPartedObjectAsync(Guid fileId, CancellationToken cancellationToken)
    {
        var fileEntity = context.Model.FindEntityType(typeof(File))!;
        var tableName = fileEntity.GetTableName();
        var idCol = fileEntity.FindProperty(nameof(File.Id))!.GetColumnName();
        var nextPartIdCol = fileEntity.FindProperty(nameof(File.NextPartId))!.GetColumnName();

        var sql = $"""
                   WITH RECURSIVE find_head AS (
                       SELECT "{idCol}", "{nextPartIdCol}", 1 AS depth
                       FROM "{tableName}"
                       WHERE "{idCol}" = @fileId

                       UNION ALL

                       SELECT f."{idCol}", f."{nextPartIdCol}", fh.depth + 1
                       FROM "{tableName}" f
                       INNER JOIN find_head fh ON f."{nextPartIdCol}" = fh."{idCol}"
                       WHERE fh.depth < 100000
                   )
                   SELECT f.*
                   FROM "{tableName}" f
                   INNER JOIN find_head fh ON f."{idCol}" = fh."{idCol}"
                   ORDER BY fh.depth DESC
                   LIMIT 1
                   """;

        var param = new NpgsqlParameter("fileId", fileId);

        return context.Files
            .FromSqlRaw(sql, param)
            .AsNoTracking()
            .SingleAsync(cancellationToken);
    }

    public Task<List<File>> GetAllAvailableObjectsInBucketAsync(Guid bucketId,
        IEnumerable<RoleType> roles, CancellationToken cancellationToken)
    {
        var rolesList = roles.ToList();

        return context.Files
            .Where(f => f.BucketId == bucketId && !f.Bucket.IsHidden &&
                        f.Bucket.AccessRoles.Intersect(rolesList).Any() &&
                        !f.IsHidden && f.AccessRoles.Intersect(rolesList).Any())
            .ToListAsync(cancellationToken);
    }

    public Task<List<ExtendedFile>> GetAllAvailableObjectsInBucketTopLevelAsync(
        Guid bucketId,
        IEnumerable<RoleType> roles,
        CancellationToken cancellationToken)
    {
        var roleIds = roles.Select(f => (int)f).Distinct().ToArray();

        if (roleIds.Length == 0) return Task.FromResult(new List<ExtendedFile>());

        var bucketEntity = context.Model.FindEntityType(typeof(Bucket))!;
        var fileEntity = context.Model.FindEntityType(typeof(File))!;

        var bucketTable = bucketEntity.GetTableName();
        var bucketIdCol = bucketEntity.FindProperty(nameof(Bucket.Id))!.GetColumnName();
        var bucketRolesCol = bucketEntity.FindProperty(nameof(Bucket.AccessRoles))!.GetColumnName();

        var fileTable = fileEntity.GetTableName();
        var fileIdCol = fileEntity.FindProperty(nameof(File.Id))!.GetColumnName();
        var fileBucketIdCol = fileEntity.FindProperty(nameof(File.BucketId))!.GetColumnName();
        var fileRolesCol = fileEntity.FindProperty(nameof(File.AccessRoles))!.GetColumnName();
        var fileNextPartIdCol = fileEntity.FindProperty(nameof(File.NextPartId))!.GetColumnName();
        var fileSizeCol = fileEntity.FindProperty(nameof(File.Size))!.GetColumnName();

        var sql = $"""
                   WITH RECURSIVE valid_bucket AS (
                       SELECT 1
                       FROM "{bucketTable}"
                       WHERE "{bucketIdCol}" = @bucketId 
                         AND "{bucketRolesCol}" && @roles
                   ),
                   file_chain AS (
                       SELECT
                           f."{fileIdCol}" AS root_id,
                           f."{fileIdCol}",
                           f."{fileNextPartIdCol}",
                           f."{fileSizeCol}",
                           1 AS depth
                       FROM "{fileTable}" f
                       CROSS JOIN valid_bucket
                       WHERE f."{fileBucketIdCol}" = @bucketId
                         AND f."{fileRolesCol}" && @roles
                         AND NOT EXISTS (
                             SELECT 1 
                             FROM "{fileTable}" child
                             WHERE child."{fileNextPartIdCol}" = f."{fileIdCol}"
                         )

                       UNION ALL

                       SELECT
                           current_file.root_id,
                           next_file."{fileIdCol}",
                           next_file."{fileNextPartIdCol}",
                           next_file."{fileSizeCol}",
                           current_file.depth + 1
                       FROM "{fileTable}" next_file
                       INNER JOIN file_chain current_file
                           ON next_file."{fileIdCol}" = current_file."{fileNextPartIdCol}"
                       WHERE current_file.depth < 10000
                         AND next_file."{fileRolesCol}" && @roles
                   )
                   SELECT
                       f.*,
                       stats.total_count AS "TotalCount",
                       stats.total_size AS "TotalSize"
                   FROM (
                       SELECT
                           root_id,
                           COUNT(*) AS total_count,
                           SUM(COALESCE("{fileSizeCol}", 0)) AS total_size
                       FROM file_chain
                       GROUP BY root_id
                   ) stats
                   INNER JOIN "{fileTable}" f ON f."{fileIdCol}" = stats.root_id;
                   """;

        var parameters = new object[]
        {
            new NpgsqlParameter("bucketId", bucketId),
            new NpgsqlParameter("roles", roleIds)
        };

        return context.Database
            .SqlQueryRaw<ExtendedFile>(sql, parameters)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<List<ExtendedFile>> GetAllAvailableObjectsTopLevelAsync(Guid userId,
        IEnumerable<RoleType> roles, CancellationToken cancellationToken, ApplicationDbContext? ctx = null)
    {
        var joinedRoles = string.Join(",", roles.Select(f => (int)f));

        var bucketEntity = context.Model.FindEntityType(typeof(Bucket))!;
        var fileEntity = context.Model.FindEntityType(typeof(File))!;

        var bucketIdProperty = bucketEntity.FindProperty(nameof(Bucket.Id))!;
        var bucketUserIdProperty = bucketEntity.FindProperty(nameof(Bucket.UserId))!;
        var bucketAccessRolesProperty = bucketEntity.FindProperty(nameof(Bucket.AccessRoles))!;
        var bucketIsAvailableProperty = bucketEntity.FindProperty(nameof(Bucket.IsAvailable))!;
        var bucketIsHiddenProperty = bucketEntity.FindProperty(nameof(Bucket.IsHidden))!;

        var fileIdProperty = fileEntity.FindProperty(nameof(File.Id))!;
        var fileBucketIdProperty = fileEntity.FindProperty(nameof(File.BucketId))!;
        var fileAccessRolesProperty = fileEntity.FindProperty(nameof(File.AccessRoles))!;
        var fileNextPartIdProperty = fileEntity.FindProperty(nameof(File.NextPartId))!;
        var fileSizeProperty = fileEntity.FindProperty(nameof(File.Size))!;
        var fileIsAvailableProperty = bucketEntity.FindProperty(nameof(File.IsAvailable))!;
        var fileIsHiddenProperty = bucketEntity.FindProperty(nameof(File.IsHidden))!;

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
                      WHERE (b."{bucketUserIdProperty.GetColumnName()}" IS NULL OR
                                b."{bucketUserIdProperty.GetColumnName()}" = '{userId}') AND
                            b."{bucketAccessRolesProperty.GetColumnName()}" && ARRAY[{joinedRoles}] AND
                            b."{bucketIsAvailableProperty.GetColumnName()}" = true AND
                            b."{bucketIsHiddenProperty.GetColumnName()}" = false AND
                            f."{fileAccessRolesProperty.GetColumnName()}" && ARRAY[{joinedRoles}] AND
                            f."{fileIsAvailableProperty.GetColumnName()}" = true AND
                            f."{fileIsHiddenProperty.GetColumnName()}" = false AND
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

        return (ctx ?? context).Database.SqlQueryRaw<ExtendedFile>(sql).AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public Task<int> DeleteAllAsync(IEnumerable<Guid>? ids, CancellationToken cancellationToken)
    {
        var idsArray = ids?.ToArray() ?? [];

        if (idsArray.Length == 0) return Task.FromResult(0);

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

        return context.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    public Task<int> DeleteAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (id == null) return Task.FromResult(0);

        return DeleteAllAsync([id.Value], cancellationToken);
    }
}
