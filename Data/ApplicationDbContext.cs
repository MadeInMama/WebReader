using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using WebReader.Models;
using WebReader.Models.Entities;
using File = WebReader.Models.Entities.File;

namespace WebReader.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<CustomUser> Users { get; set; }
    public DbSet<Bucket> Buckets { get; set; }
    public DbSet<File> Files { get; set; }
    public DbSet<UserReading> UserReadings { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSeeding((context, _) =>
        {
            if (!context.Set<CustomUser>().Any())
                context.Set<CustomUser>().Add(new CustomUser
                {
                    Username = "test",
                    PasswordHash = HashPassword("test"),
                    Roles = [RoleType.Admin, RoleType.User]
                });

            if (!context.Set<Bucket>().Any())
                context.Set<Bucket>().Add(new Bucket
                {
                    Name = "mybucket",
                    CustomName = "Default Bucket"
                });

            context.SaveChanges();

            var bucketId = context.Set<Bucket>().First(f => f.Name.Equals("mybucket")).Id;

            if (!context.Set<File>().Any())
                context.Set<File>().AddRange(new File
                {
                    BucketId = bucketId,
                    Name = "314041.pdf",
                    CustomName = "Семья Корлеоне: Пьюзо, Фалько",
                    AccessRoles = [RoleType.User],
                    Type = FileType.Pdf,
                    IsHidden = false
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
            .HasIndex(f => f.Name)
            .IsUnique();

        modelBuilder.Entity<CustomUser>()
            .HasOne(f => f.Bucket)
            .WithOne(f => f.User)
            .HasForeignKey<Bucket>(f => f.UserId);

        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges()
    {
        SetCreateAndUpdateTime();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = new())
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

    private static string HashPassword(string password)
    {
        var hashedBytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }
}
