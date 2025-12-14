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
            {
                var user = context.Set<CustomUser>().Add(new CustomUser
                {
                    Username = "test",
                    PasswordHash = HashPassword("test"),
                    Roles = [RoleType.Admin, RoleType.User]
                });

                context.SaveChanges();

                context.Set<Bucket>().Add(new Bucket
                {
                    Name = $"personal-{user.Entity.Id}",
                    CustomName = "Personal Bucket",
                    UserId = user.Entity.Id
                });

                context.SaveChanges();
            }

            if (!context.Set<Bucket>().Any(f => f.Name.Equals("mybucket")))
            {
                context.Set<Bucket>().Add(new Bucket
                {
                    Name = "mybucket",
                    CustomName = "Default Bucket"
                });

                context.SaveChanges();
            }

            var bucketId = context.Set<Bucket>().First(f => f.Name.Equals("mybucket")).Id;

            if (!context.Set<File>().Any())
                context.Set<File>().AddRange(new File
                {
                    BucketId = bucketId,
                    Name = "314041.pdf",
                    CustomName = "Семья Корлеоне: Пьюзо, Фалько",
                    Type = FileType.Pdf,
                    IsHidden = false
                }, new File
                {
                    BucketId = bucketId,
                    Name = "Краткие ответы на большие вопросы [2019] Хокинг.fb2",
                    CustomName = "Краткие ответы на большие вопросы: Хокинг",
                    Type = FileType.Fb2,
                    IsHidden = false
                }, new File
                {
                    BucketId = bucketId,
                    Name = "Mario_Pyuzo_-_Omerta.pdf",
                    CustomName = "Омерта: Пьюзо",
                    Type = FileType.Pdf,
                    IsHidden = false
                }, new File
                {
                    BucketId = bucketId,
                    Name = "Mario_Pyuzo_-_Sitsiliets.pdf",
                    CustomName = "Сицилиец: Пьюзо",
                    Type = FileType.Pdf,
                    IsHidden = false
                }, new File
                {
                    BucketId = bucketId,
                    Name = "pyuzo_krestnyy-otec_1_krestnyy-oteckw4btg178419.pdf",
                    CustomName = "Крестный отец: Пьюзо",
                    Type = FileType.Pdf,
                    IsHidden = false
                }, new File
                {
                    BucketId = bucketId,
                    Name = "Vayngartner Mark Mest Krestnogo otca - BooksCafeNet.pdf",
                    CustomName = "Месть Крестного отца: Винегарднер",
                    Type = FileType.Pdf,
                    IsHidden = false
                }, new File
                {
                    BucketId = bucketId,
                    Name = "Vayngartner Mark Vozvraschenie Krestnogo otca - BooksCafeNet.pdf",
                    CustomName = "Возвращение Крестного отца: Винегарднер",
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
