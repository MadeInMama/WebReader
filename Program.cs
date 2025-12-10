using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Minio;
using WebReader.Configuration;
using WebReader.Data;
using WebReader.Repositories;
using WebReader.Services;
using MinioConfig = WebReader.Configuration.MinioConfig;

var builder = WebApplication.CreateBuilder(args);

var dbConfig = new DbConfig();
builder.Configuration.GetRequiredSection(nameof(DbConfig)).Bind(dbConfig);
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        dbConfig.ConnectionString!,
        npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(
            5,
            TimeSpan.FromSeconds(30),
            null
        )
    )
);
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
        options.UseNpgsql(
            dbConfig.ConnectionString!,
            npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(
                5,
                TimeSpan.FromSeconds(30),
                null
            )
        ),
    ServiceLifetime.Scoped
);

var minioConfig = new MinioConfig();
builder.Configuration.GetRequiredSection(nameof(MinioConfig)).Bind(minioConfig);
builder.Services.AddSingleton<IMinioClient>(_ => new MinioClient()
    .WithEndpoint(minioConfig.Endpoint)
    .WithCredentials(minioConfig.AccessKey, minioConfig.SecretKey)
    .WithSSL(false)
    .Build());

builder.Services.AddScoped<BucketService>();
builder.Services.AddScoped<MinioService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<BucketRepository>();
builder.Services.AddScoped<CustomUserRepository>();
builder.Services.AddScoped<FileRepository>();
builder.Services.AddScoped<UserReadingRepository>();

// builder.Services.AddHostedService<UpdateFilesFromS3>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/SignIn";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
        options.ReturnUrlParameter = "returnUrl";
    });

builder.Services.AddControllersWithViews();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    Console.WriteLine("Database migration started");
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.Migrate();
    Console.WriteLine("Database migration finished");
}

if (!app.Environment.IsDevelopment()) app.UseHsts();

app.UseHttpsRedirection();
app.UseRouting();

app.MapStaticAssets();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Map("/", () => Results.Redirect("/Home/Index"));
app.Map("/Home", () => Results.Redirect("/Home/Index"));

app.Run();
