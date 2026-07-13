using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.IdentityModel.Tokens;
using Minio;
using Telegram.Bot;
using WebReader.Background;
using WebReader.Background.AutoDownloadNewParts;
using WebReader.Background.Delete;
using WebReader.Background.SyncDbWithS3;
using WebReader.Configuration;
using WebReader.Data;
using WebReader.Helpers;
using WebReader.Models;
using WebReader.Models.Signal;
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
        LocalCacheExpiration = TimeSpan.FromSeconds(1),
        Expiration = TimeSpan.FromSeconds(1)
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

builder.Services.AddKeyedTransient<IBackgroundTasked, DeleteOldCompletedTasks>(
    TaskType.DeleteOldCompletedTasks);
builder.Services.AddKeyedTransient<IBackgroundTasked, DeleteOldErroredTasks>(
    TaskType.DeleteOldErroredTasks);
builder.Services.AddKeyedTransient<IBackgroundTasked, DeleteOldInProgressTasks>(
    TaskType.DeleteOldInProgressTasks);

builder.Services.Configure<BackgroundTaskConfig>(builder.Configuration.GetSection("BackgroundTasks"));
builder.Services.AddHostedService<BackgroundTaskManager>();

builder.Services.Configure<TelegramBotClientConfig>(builder.Configuration.GetSection("Telegram"));
var telegramConfig = new TelegramBotClientConfig();
builder.Configuration.GetRequiredSection("Telegram").Bind(telegramConfig);
builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(telegramConfig.BotToken!));

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
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
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
                Encoding.UTF8.GetBytes(jwtConfig.Key)),
            ClockSkew = TimeSpan.Zero
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

builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    options.Preload = true;
});

builder.Services.AddSignalR();

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

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.SuppressXFrameOptionsHeader = false;
});

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("LoginPolicy", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(2);
        opt.QueueLimit = 0;
    });
});

var app = builder.Build();

app.UseForwardedHeaders();

app.UseExceptionHandler("/Account/CustomNotFound");

app.UseHsts();

app.UseHttpsRedirection();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    context.Database.Migrate();

    // await context.ScheduledTasks.ExecuteDeleteAsync();
    // var files = await context.Files.Where(f => f.CurrentPartNumber > 303).ToListAsync();
    // await scope.ServiceProvider.GetRequiredService<FileService>()
    // .DeleteFileAsync(files.Select(f => f.Id).ToList(), CancellationToken.None);

    var botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
    await botClient.SetWebhook(telegramConfig.WebhookUrl!);
}

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers.Remove("Content-Security-Policy");
        context.Response.Headers.Remove("X-Content-Type-Options");
        context.Response.Headers.Remove("X-Frame-Options");
        context.Response.Headers.Remove("X-XSS-Protection");
        context.Response.Headers.Remove("Referrer-Policy");
        context.Response.Headers.Remove("Strict-Transport-Security");
        context.Response.Headers.Remove("Cross-Origin-Embedder-Policy");
        context.Response.Headers.Remove("Cross-Origin-Opener-Policy");
        context.Response.Headers.Remove("Permissions-Policy");

        context.Response.Headers.Append("Content-Security-Policy",
            "default-src 'self'; " +
            "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://unpkg.com; " +
            "style-src 'self' 'unsafe-inline'; " +
            "img-src 'self' data: blob:; " +
            "worker-src 'self' blob:; " +
            "font-src 'self' data:; " +
            "connect-src 'self'; " +
            "media-src 'self'; " +
            "object-src 'none'; " +
            "base-uri 'self'; " +
            "form-action 'self'; " +
            "frame-ancestors 'self'; " +
            "report-uri /api/Csp/Report;");

        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

        context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains; preload");

        context.Response.Headers.Append("Cross-Origin-Embedder-Policy", "require-corp");
        context.Response.Headers.Append("Cross-Origin-Opener-Policy", "same-origin");

        context.Response.Headers.Append("Permissions-Policy",
            "camera=(), microphone=(), geolocation=(), fullscreen=(), payment=(), usb=(), accelerometer=(), gyroscope=()");

        return Task.CompletedTask;
    });

    await next();
});

app.UseResponseCompression();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        if (ctx.File.Name.Equals("sw.js", StringComparison.OrdinalIgnoreCase) ||
            ctx.File.Name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
            ctx.Context.Response.Headers.Append("Pragma", "no-cache");
            ctx.Context.Response.Headers.Append("Expires", "0");
        }
        else
        {
            ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=31536000, immutable");
        }
    }
});

app.UseHttpMethodOverride(new HttpMethodOverrideOptions
{
    FormFieldName = "_method"
});

app.UseRateLimiter();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Map("/", () => Results.Redirect("/Home/Index"));
app.Map("/Home", () => Results.Redirect("/Home/Index"));
app.MapGet("/.well-known/blacksight-domain-association", async context =>
{
    context.Response.ContentType = "text/plain; charset=utf-8";
    await context.Response.WriteAsync("blacksight-verification-code=094477df-9492-4fdf-aef9-da976b358344");
});
app.MapHub<ScheduledTaskHub>("/ScheduledTaskHub");

app.Use((context, next) =>
{
    context.Request.EnableBuffering();
    return next();
});

app.Run();
