using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebReader.Helpers;
using WebReader.Models;
using WebReader.Models.Entities;
using File = WebReader.Models.Entities.File;

namespace WebReader.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<CustomUser> Users { get; set; }
    public DbSet<Bucket> Buckets { get; set; }
    public DbSet<File> Files { get; set; }
    public DbSet<RequestAttemptsLog> RequestAttemptsLogs { get; set; }
    public DbSet<UserReading> UserReadings { get; set; }
    public DbSet<SubscriberTg> SubscriberTgs { get; set; }
    public DbSet<ScheduledTask> ScheduledTasks { get; set; }
    public DbSet<ScheduledTaskConfig> ScheduledTaskConfigs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSeeding((context, _) =>
        {
            if (!context.Set<CustomUser>().Any())
            {
                var user = context.Set<CustomUser>().Add(new CustomUser
                {
                    Username = "test",
                    PasswordHash = StaticFunctions.HashPassword("test"),
                    Roles = [RoleType.Admin, RoleType.User]
                });

                context.SaveChanges();

                context.Set<Bucket>().Add(new Bucket
                {
                    Name = $"personal-{user.Entity.Id}",
                    CustomName = "Personal",
                    UserId = user.Entity.Id
                });

                context.SaveChanges();
            }

            if (!context.Set<Bucket>().Any(f => f.Name.Equals("mybucket")))
            {
                context.Set<Bucket>().Add(new Bucket
                {
                    Name = "mybucket",
                    CustomName = "Default"
                });

                context.SaveChanges();
            }

            if (!context.Set<Bucket>().Any(f => f.Name.Equals(StaticNames.CoversBucketName)))
            {
                context.Set<Bucket>().Add(new Bucket
                {
                    Name = StaticNames.CoversBucketName,
                    IsSystem = true,
                    IsHidden = true
                });

                context.SaveChanges();
            }

            if (!context.Set<Bucket>().Any(f => f.Name.Equals(StaticNames.TtsBucketName)))
            {
                context.Set<Bucket>().Add(new Bucket
                {
                    Name = StaticNames.TtsBucketName,
                    IsSystem = true,
                    IsHidden = true
                });

                context.SaveChanges();
            }

            if (!context.Set<ScheduledTaskConfig>()
                    .Any(f => f.Type == TaskType.RemoveBucketsThatNotExistsInDb))
                context.Set<ScheduledTaskConfig>().Add(new ScheduledTaskConfig
                {
                    Type = TaskType.RemoveBucketsThatNotExistsInDb,
                    DefaultPriority = sbyte.MinValue + 24,
                    Cron = TaskCron.EveryHour,
                    IsActive = true
                });

            if (!context.Set<ScheduledTaskConfig>()
                    .Any(f => f.Type == TaskType.MakeUnavailableBucketsThatNotExistsInS3))
                context.Set<ScheduledTaskConfig>().Add(new ScheduledTaskConfig
                {
                    Type = TaskType.MakeUnavailableBucketsThatNotExistsInS3,
                    DefaultPriority = sbyte.MinValue + 22,
                    Cron = TaskCron.EveryHour,
                    IsActive = true
                });

            if (!context.Set<ScheduledTaskConfig>()
                    .Any(f => f.Type == TaskType.RemoveFilesThatNotExistsInDb))
                context.Set<ScheduledTaskConfig>().Add(new ScheduledTaskConfig
                {
                    Type = TaskType.RemoveFilesThatNotExistsInDb,
                    DefaultPriority = sbyte.MinValue + 20,
                    Cron = TaskCron.EveryHour,
                    IsActive = true
                });

            if (!context.Set<ScheduledTaskConfig>()
                    .Any(f => f.Type == TaskType.RemoveTtsForFilesThatNotExistsInDb))
                context.Set<ScheduledTaskConfig>().Add(new ScheduledTaskConfig
                {
                    Type = TaskType.RemoveTtsForFilesThatNotExistsInDb,
                    DefaultPriority = sbyte.MinValue + 18,
                    Cron = TaskCron.EveryHour,
                    IsActive = true
                });

            if (!context.Set<ScheduledTaskConfig>()
                    .Any(f => f.Type == TaskType.UpdateBucketData))
                context.Set<ScheduledTaskConfig>().Add(new ScheduledTaskConfig
                {
                    Type = TaskType.UpdateBucketData,
                    DefaultPriority = sbyte.MinValue + 16,
                    Cron = TaskCron.EveryHour,
                    IsActive = true
                });

            if (!context.Set<ScheduledTaskConfig>()
                    .Any(f => f.Type == TaskType.UpdateFilesData))
                context.Set<ScheduledTaskConfig>().Add(new ScheduledTaskConfig
                {
                    Type = TaskType.UpdateFilesData,
                    DefaultPriority = sbyte.MinValue + 14,
                    Cron = TaskCron.EveryHour,
                    IsActive = true
                });

            if (!context.Set<ScheduledTaskConfig>()
                    .Any(f => f.Type == TaskType.AutoDownloadNewPartsOmniscientReader))
                context.Set<ScheduledTaskConfig>().Add(new ScheduledTaskConfig
                {
                    Type = TaskType.AutoDownloadNewPartsOmniscientReader,
                    DefaultPriority = sbyte.MinValue + 12,
                    Cron = TaskCron.EveryDay,
                    DefaultSettings = JsonDocument.Parse("{\"max_size\": 500}"),
                    IsActive = true
                });

            if (!context.Set<ScheduledTaskConfig>()
                    .Any(f => f.Type == TaskType.AutoDownloadNewPartsSoloLeveling))
                context.Set<ScheduledTaskConfig>().Add(new ScheduledTaskConfig
                {
                    Type = TaskType.AutoDownloadNewPartsSoloLeveling,
                    DefaultPriority = sbyte.MinValue + 10,
                    Cron = TaskCron.EveryWeek,
                    DefaultSettings = JsonDocument.Parse("{\"max_size\": 500}"),
                    IsActive = true
                });

            if (!context.Set<ScheduledTaskConfig>()
                    .Any(f => f.Type == TaskType.AutoDownloadNewPartsWorldAfterDestruction))
                context.Set<ScheduledTaskConfig>().Add(new ScheduledTaskConfig
                {
                    Type = TaskType.AutoDownloadNewPartsWorldAfterDestruction,
                    DefaultPriority = sbyte.MinValue + 8,
                    Cron = TaskCron.EveryWeek,
                    DefaultSettings = JsonDocument.Parse("{\"max_size\": 500}"),
                    IsActive = true
                });

            if (!context.Set<ScheduledTaskConfig>()
                    .Any(f => f.Type == TaskType.DeleteOldCompletedTasks))
                context.Set<ScheduledTaskConfig>().Add(new ScheduledTaskConfig
                {
                    Type = TaskType.DeleteOldCompletedTasks,
                    DefaultPriority = sbyte.MinValue + 6,
                    Cron = TaskCron.EveryDay,
                    DefaultSettings = JsonDocument.Parse("{\"older_then_in_hours\": 72}"),
                    IsActive = true
                });

            if (!context.Set<ScheduledTaskConfig>()
                    .Any(f => f.Type == TaskType.DeleteOldErroredTasks))
                context.Set<ScheduledTaskConfig>().Add(new ScheduledTaskConfig
                {
                    Type = TaskType.DeleteOldErroredTasks,
                    DefaultPriority = sbyte.MinValue + 4,
                    Cron = TaskCron.EveryDay,
                    DefaultSettings = JsonDocument.Parse("{\"older_then_in_hours\": 720}"),
                    IsActive = true
                });

            if (!context.Set<ScheduledTaskConfig>()
                    .Any(f => f.Type == TaskType.DeleteOldInProgressTasks))
                context.Set<ScheduledTaskConfig>().Add(new ScheduledTaskConfig
                {
                    Type = TaskType.DeleteOldInProgressTasks,
                    DefaultPriority = sbyte.MinValue + 2,
                    Cron = TaskCron.EveryHour,
                    DefaultSettings = JsonDocument.Parse("{\"older_then_in_hours\": 24}"),
                    IsActive = true
                });

            if (!context.Set<ScheduledTaskConfig>()
                    .Any(f => f.Type == TaskType.DeleteOldRequestAttemptsLogs))
                context.Set<ScheduledTaskConfig>().Add(new ScheduledTaskConfig
                {
                    Type = TaskType.DeleteOldRequestAttemptsLogs,
                    DefaultPriority = sbyte.MinValue,
                    Cron = TaskCron.EveryDay,
                    DefaultSettings = JsonDocument.Parse("{\"older_then_in_hours\": 4320}"),
                    IsActive = true
                });

            if (!context.Set<ScheduledTaskConfig>()
                    .Any(f => f.Type == TaskType.CalcAllBooksSizes))
                context.Set<ScheduledTaskConfig>().Add(new ScheduledTaskConfig
                {
                    Type = TaskType.CalcAllBooksSizes,
                    DefaultPriority = sbyte.MinValue,
                    Cron = TaskCron.Manually,
                    IsActive = true
                });

            context.SaveChanges();
        });
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserReading>()
            .Property(f => f.Page)
            .HasDefaultValue(1);

        modelBuilder.Entity<UserReading>()
            .Property(f => f.Scale)
            .HasDefaultValue(1);

        modelBuilder.Entity<File>()
            .HasIndex(f => new { f.Name, f.BucketId })
            .IsUnique();
        modelBuilder.Entity<File>()
            .Property(f => f.Settings)
            .HasColumnType("jsonb");

        modelBuilder.Entity<CustomUser>()
            .HasOne(f => f.Bucket)
            .WithOne(f => f.User)
            .HasForeignKey<Bucket>(f => f.UserId);

        modelBuilder.Entity<ScheduledTask>()
            .HasIndex(f => f.Priority);
        modelBuilder.Entity<ScheduledTask>()
            .HasOne(f => f.ScheduledTaskConfig);

        modelBuilder.Entity<RequestAttemptsLog>()
            .HasKey(f => new { f.Ip, f.CreatedDate });
        modelBuilder.Entity<RequestAttemptsLog>()
            .HasIndex(f => f.Ip);

        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges()
    {
        SetCreateAndUpdateTime();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetCreateAndUpdateTime();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void SetCreateAndUpdateTime()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e is { Entity: BaseEntity, State: EntityState.Added or EntityState.Modified });

        foreach (var entityEntry in entries)
        {
            ((BaseEntity)entityEntry.Entity).UpdatedDate = DateTimeOffset.UtcNow;

            if (entityEntry.State == EntityState.Added)
                ((BaseEntity)entityEntry.Entity).CreatedDate = DateTimeOffset.UtcNow;
        }
    }
}
