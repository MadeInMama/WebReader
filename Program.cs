using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.IdentityModel.Tokens;
using Minio;
using Telegram.Bot;
using WebReader.Background;
using WebReader.Background.AutoDownloadNewParts;
using WebReader.Background.SyncDbWithS3;
using WebReader.Configuration;
using WebReader.Data;
using WebReader.Helpers;
using WebReader.Models;
using WebReader.Repositories;
using WebReader.Services;
using MinioConfig = WebReader.Configuration.MinioConfig;

var builder = WebApplication.CreateBuilder(args);

var dbConfig = new DbConfig();
builder.Configuration.GetRequiredSection(nameof(DbConfig)).Bind(dbConfig);
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        dbConfig.ConnectionString!,
        npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null)
    )
);
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
        options.UseNpgsql(
            dbConfig.ConnectionString!,
            npgsqlOptions => npgsqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null)
        ),
    ServiceLifetime.Scoped
);

builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(30)
    };
});

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
});

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
builder.Services.AddScoped<FileUploadService>();
builder.Services.AddScoped<FileService>();
builder.Services.AddScoped<BucketRepository>();
builder.Services.AddScoped<CustomUserRepository>();
builder.Services.AddScoped<FileRepository>();
builder.Services.AddScoped<UserReadingRepository>();
builder.Services.AddScoped<FileControllerService>();
builder.Services.AddScoped<AuthRestService>();
builder.Services.AddScoped<ScheduledTaskConfigRepository>();
builder.Services.AddScoped<ScheduledTaskRepository>();

builder.Services.AddScoped<LogRequestAttribute>();

builder.Services.AddKeyedTransient<IBackgroundTasked, RemoveBucketsThatNotExistsInDb>(
    TaskType.RemoveBucketsThatNotExistsInDb);
builder.Services.AddKeyedTransient<IBackgroundTasked, MakeUnavailableBucketsThatNotExistsInS3>(
    TaskType.MakeUnavailableBucketsThatNotExistsInS3);
builder.Services.AddKeyedTransient<IBackgroundTasked, RemoveFilesThatNotExistsInDb>(
    TaskType.RemoveFilesThatNotExistsInDb);
builder.Services.AddKeyedTransient<IBackgroundTasked, UpdateBucketData>(
    TaskType.UpdateBucketData);
builder.Services.AddKeyedTransient<IBackgroundTasked, UpdateFilesData>(
    TaskType.UpdateFilesData);

builder.Services.AddKeyedTransient<IBackgroundTasked, AutoDownloadNewPartsOmniscientReader>(
    TaskType.AutoDownloadNewPartsOmniscientReader);
builder.Services.AddKeyedTransient<IBackgroundTasked, AutoDownloadNewPartsSoloLeveling>(
    TaskType.AutoDownloadNewPartsSoloLeveling);
builder.Services.AddKeyedTransient<IBackgroundTasked, AutoDownloadNewPartsWorldAfterDestruction>(
    TaskType.AutoDownloadNewPartsWorldAfterDestruction);

builder.Services.AddHostedService<BackgroundTaskManager>();

builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(builder.Configuration["Telegram:BotToken"]!));

var jwtConfig = new JwtConfig();
builder.Configuration.GetRequiredSection(nameof(JwtConfig)).Bind(jwtConfig);
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
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
    })
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtConfig.Issuer,
            ValidAudience = jwtConfig.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtConfig.Key))
        };
    }).AddPolicyScheme("CookiesOrJwt", "Cookies or JWT", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();

            if (!string.IsNullOrEmpty(authHeader) &&
                authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return JwtBearerDefaults.AuthenticationScheme;

            return CookieAuthenticationDefaults.AuthenticationScheme;
        };
    });

builder.Services.AddAuthorizationBuilder()
    .SetDefaultPolicy(new AuthorizationPolicyBuilder("CookiesOrJwt")
        .RequireAuthenticatedUser()
        .Build());

builder.Services.AddControllersWithViews();

builder.Services.AddHttpClient();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = long.MaxValue;
    options.ValueLengthLimit = int.MaxValue;
    options.MemoryBufferThreshold = int.MaxValue;
});

builder.Services.Configure<KestrelServerOptions>(options => { options.Limits.MaxRequestBodySize = long.MaxValue; });

builder.Services.Configure<IISServerOptions>(options => { options.MaxRequestBodySize = long.MaxValue; });

builder.Services.AddControllers()
    .AddJsonOptions(options => { options.JsonSerializerOptions.PropertyNamingPolicy = null; });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.Migrate();

    var botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
    await botClient.SetWebhook(builder.Configuration["Telegram:WebhookUrl"]!);
}

app.UseResponseCompression();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=31536000, immutable");
    }
});

app.UseExceptionHandler("/Account/CustomNotFound");

app.UseHttpMethodOverride(new HttpMethodOverrideOptions
{
    FormFieldName = "_method"
});

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseHsts();

app.UseRouting();

//TODO: don't understand. is needed? (new UseStaticFiles)
app.MapStaticAssets();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Map("/", () => Results.Redirect("/Home/Index"));
app.Map("/Home", () => Results.Redirect("/Home/Index"));

app.Use((context, next) =>
{
    context.Request.EnableBuffering();
    return next();
});

app.Run();
