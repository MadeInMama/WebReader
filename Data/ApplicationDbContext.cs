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

            if (!context.Set<ScheduledTaskConfig>()
                    .Any(f => f.Type == TaskType.RemoveBucketsThatNotExistsInDb))
                context.Set<ScheduledTaskConfig>().Add(new ScheduledTaskConfig
                {
                    Type = TaskType.RemoveBucketsThatNotExistsInDb,
                    DefaultPriority = sbyte.MaxValue,
                    Cron = TaskConfigCron.EveryHour,
                    IsActive = true
                });

            if (!context.Set<ScheduledTaskConfig>()
                    .Any(f => f.Type == TaskType.MakeUnavailableBucketsThatNotExistsInS3))
                context.Set<ScheduledTaskConfig>().Add(new ScheduledTaskConfig
                {
                    Type = TaskType.MakeUnavailableBucketsThatNotExistsInS3,
                    DefaultPriority = sbyte.MaxValue - 1,
                    Cron = TaskConfigCron.EveryHour,
                    IsActive = true
                });

            if (!context.Set<ScheduledTaskConfig>()
                    .Any(f => f.Type == TaskType.RemoveFilesThatNotExistsInDb))
                context.Set<ScheduledTaskConfig>().Add(new ScheduledTaskConfig
                {
                    Type = TaskType.RemoveFilesThatNotExistsInDb,
                    DefaultPriority = sbyte.MaxValue - 2,
                    Cron = TaskConfigCron.EveryHour,
                    IsActive = true
                });

            if (!context.Set<ScheduledTaskConfig>()
                    .Any(f => f.Type == TaskType.UpdateBucketData))
                context.Set<ScheduledTaskConfig>().Add(new ScheduledTaskConfig
                {
                    Type = TaskType.UpdateBucketData,
                    DefaultPriority = sbyte.MaxValue - 3,
                    Cron = TaskConfigCron.EveryHour,
                    IsActive = true
                });

            if (!context.Set<ScheduledTaskConfig>()
                    .Any(f => f.Type == TaskType.UpdateFilesData))
                context.Set<ScheduledTaskConfig>().Add(new ScheduledTaskConfig
                {
                    Type = TaskType.UpdateFilesData,
                    DefaultPriority = sbyte.MaxValue - 4,
                    Cron = TaskConfigCron.EveryHour,
                    IsActive = true
                });

            if (!context.Set<ScheduledTaskConfig>()
                    .Any(f => f.Type == TaskType.AutoDownloadNewPartsOmniscientReader))
                context.Set<ScheduledTaskConfig>().Add(new ScheduledTaskConfig
                {
                    Type = TaskType.AutoDownloadNewPartsOmniscientReader,
                    DefaultPriority = 100,
                    Cron = TaskConfigCron.EveryDay,
                    Settings = JsonDocument.Parse("{\"max_size\": 1000}"),
                    IsActive = true
                });

            if (!context.Set<ScheduledTaskConfig>()
                    .Any(f => f.Type == TaskType.AutoDownloadNewPartsSoloLeveling))
                context.Set<ScheduledTaskConfig>().Add(new ScheduledTaskConfig
                {
                    Type = TaskType.AutoDownloadNewPartsSoloLeveling,
                    DefaultPriority = 90,
                    Cron = TaskConfigCron.EveryWeek,
                    Settings = JsonDocument.Parse("{\"max_size\": 1000}"),
                    IsActive = true
                });

            if (!context.Set<ScheduledTaskConfig>()
                    .Any(f => f.Type == TaskType.AutoDownloadNewPartsWorldAfterDestruction))
                context.Set<ScheduledTaskConfig>().Add(new ScheduledTaskConfig
                {
                    Type = TaskType.AutoDownloadNewPartsWorldAfterDestruction,
                    DefaultPriority = 90,
                    Cron = TaskConfigCron.EveryWeek,
                    Settings = JsonDocument.Parse("{\"max_size\": 1000}"),
                    IsActive = true
                });

            if (!context.Set<ScheduledTaskConfig>()
                    .Any(f => f.Type == TaskType.DeleteOldCompletedTasks))
                context.Set<ScheduledTaskConfig>().Add(new ScheduledTaskConfig
                {
                    Type = TaskType.DeleteOldCompletedTasks,
                    DefaultPriority = 0,
                    Cron = TaskConfigCron.EveryWeek,
                    Settings = JsonDocument.Parse("{\"older_then_in_days\": 7}"),
                    IsActive = true
                });

            if (!context.Set<ScheduledTaskConfig>()
                    .Any(f => f.Type == TaskType.DeleteOldErroredTasks))
                context.Set<ScheduledTaskConfig>().Add(new ScheduledTaskConfig
                {
                    Type = TaskType.DeleteOldErroredTasks,
                    DefaultPriority = 0,
                    Cron = TaskConfigCron.EveryMonth,
                    Settings = JsonDocument.Parse("{\"older_then_in_days\": 30}"),
                    IsActive = true
                });

            if (!context.Set<ScheduledTaskConfig>()
                    .Any(f => f.Type == TaskType.DeleteOldInProgressTasks))
                context.Set<ScheduledTaskConfig>().Add(new ScheduledTaskConfig
                {
                    Type = TaskType.DeleteOldInProgressTasks,
                    DefaultPriority = 0,
                    Cron = TaskConfigCron.EveryHour,
                    Settings = JsonDocument.Parse("{\"older_then_in_days\": 1}"),
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
