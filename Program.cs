using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Minio;
using WebReader.Configuration;
using WebReader.Data;
using WebReader.Repositories;
using WebReader.Services;
using WebReader.Services.Impl;
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

var minioConfig = new MinioConfig();
builder.Configuration.GetRequiredSection(nameof(MinioConfig)).Bind(minioConfig);
builder.Services.AddSingleton<IMinioClient>(_ => new MinioClient()
    .WithEndpoint(minioConfig.Endpoint)
    .WithCredentials(minioConfig.AccessKey, minioConfig.SecretKey)
    .WithSSL(false)
    .Build());

builder.Services.AddScoped<IMinioService, MinioService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<CustomUserRepository>();
builder.Services.AddScoped<FileRepository>();
builder.Services.AddScoped<UserReadingRepository>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
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