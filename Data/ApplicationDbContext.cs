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

            if (!context.Set<File>().Any())
                context.Set<File>().AddRange(new File
                {
                    Bucket = "mybucket",
                    Object = "file-sample_150kB.pdf",
                    CustomName = "Small File",
                    AccessRoles = [RoleType.Admin],
                    IsHidden = false,
                    Type = FileType.Pdf
                }, new File
                {
                    Bucket = "mybucket",
                    Object = "file-example_500_kB.pdf",
                    CustomName = "Medium File",
                    AccessRoles = [RoleType.User, RoleType.Admin],
                    IsHidden = false,
                    Type = FileType.Pdf
                }, new File
                {
                    Bucket = "mybucket",
                    Object = "file-example_1MB.pdf",
                    CustomName = "Large File",
                    AccessRoles = [RoleType.User, RoleType.Admin],
                    IsHidden = false,
                    Type = FileType.Pdf
                }, new File
                {
                    Bucket = "mybucket",
                    Object = "file-example_200MB.pdf",
                    CustomName = "Giant File",
                    AccessRoles = [RoleType.User, RoleType.Admin],
                    IsHidden = false,
                    Type = FileType.Pdf
                });

            context.SaveChanges();
        });
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