using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.IdentityModel.Tokens;
using Minio;
using Polly;
using StackExchange.Redis;
using Telegram.Bot;
using WebReader.Background;
using WebReader.Background.AutoDownloadNewParts;
using WebReader.Background.Delete;
using WebReader.Background.Notification;
using WebReader.Background.Stats;
using WebReader.Background.SyncDbWithS3;
using WebReader.Configuration;
using WebReader.Data;
using WebReader.Helpers;
using WebReader.Models;
using WebReader.Models.Signal;
using WebReader.Repositories;
using WebReader.Services;
using WebReader.Services.Http;
using WebReader.Services.Notification;
using WebReader.Services.Tts;
using Yarp.ReverseProxy.Model;
using MinioConfig = WebReader.Configuration.MinioConfig;

var builder = WebApplication.CreateBuilder(args);

// var full = File.ReadAllBytes(@"C:\Users\Пользователь\Downloads\test.jpeg");
//
// var fullImage = Image.Load<Rgb24>(full);
//
// var splitted = ImageSplitter.Split(fullImage);
//
// foreach (var split in splitted.Index())
//     File.WriteAllBytes(@$"C:\Users\Пользователь\Downloads\test_{split.Index}.jpeg", split.Item);

//FOR LOCAL TTS
// builder.Services.AddCors(options =>
// {
//     options.AddPolicy("DevCorsPolicy", policy =>
//     {
//         policy.SetIsOriginAllowed(origin => origin == "null" || origin.StartsWith("http://localhost"))
//             .AllowAnyMethod()
//             .AllowAnyHeader()
//             .AllowCredentials()
//             .WithExposedHeaders("Content-Disposition", "Content-Length", "Accept-Ranges");
//     });
// });

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddHttpContextAccessor();

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

builder.Services.AddScoped<BucketRepository>();
builder.Services.AddScoped<CustomUserRepository>();
builder.Services.AddScoped<FileRepository>();
builder.Services.AddScoped<NotificationRepository>();
builder.Services.AddScoped<RequestAttemptsLogRepository>();
builder.Services.AddScoped<ScheduledTaskConfigRepository>();
builder.Services.AddScoped<ScheduledTaskRepository>();
builder.Services.AddScoped<SubscriberPwaRepository>();
builder.Services.AddScoped<SubscriberTgRepository>();
builder.Services.AddScoped<UserReadingRepository>();
builder.Services.AddScoped<UserTgLinkRepository>();

builder.Services.AddScoped<AuthRestService>();
builder.Services.AddScoped<BucketService>();
builder.Services.AddScoped<FileControllerService>();
builder.Services.AddScoped<FileService>();
builder.Services.AddScoped<FileUploadService>();
builder.Services.AddScoped<LinkGenerationService>();
builder.Services.AddScoped<MinioService>();
builder.Services.AddScoped<UserService>();

builder.Services.AddScoped<LogRequestAttribute>();

builder.Services.AddKeyedTransient<IBackgroundTasked, RemoveBucketsThatNotExistsInDb>(
    TaskType.RemoveBucketsThatNotExistsInDb);
builder.Services.AddKeyedTransient<IBackgroundTasked, MakeUnavailableBucketsThatNotExistsInS3>(
    TaskType.MakeUnavailableBucketsThatNotExistsInS3);
builder.Services.AddKeyedTransient<IBackgroundTasked, RemoveFilesThatNotExistsInDb>(
    TaskType.RemoveFilesThatNotExistsInDb);
builder.Services.AddKeyedTransient<IBackgroundTasked, RemoveTtsForFilesThatNotExistsInDb>(
    TaskType.RemoveTtsForFilesThatNotExistsInDb);
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

builder.Services.AddKeyedTransient<IBackgroundTasked, ClearUserTgLinks>(
    TaskType.ClearUserTgLinks);
builder.Services.AddKeyedTransient<IBackgroundTasked, DeleteOldCompletedTasks>(
    TaskType.DeleteOldCompletedTasks);
builder.Services.AddKeyedTransient<IBackgroundTasked, DeleteOldErroredTasks>(
    TaskType.DeleteOldErroredTasks);
builder.Services.AddKeyedTransient<IBackgroundTasked, DeleteOldInProgressTasks>(
    TaskType.DeleteOldInProgressTasks);
builder.Services.AddKeyedTransient<IBackgroundTasked, DeleteOldRequestAttemptsLogs>(
    TaskType.DeleteOldRequestAttemptsLogs);
builder.Services.AddKeyedTransient<IBackgroundTasked, DeleteSentNotifications>(
    TaskType.DeleteSentNotifications);

builder.Services.AddKeyedTransient<IBackgroundTasked, CalcAllBooksSizes>(
    TaskType.CalcAllBooksSizes);

builder.Services.AddKeyedTransient<IBackgroundTasked, SendNotifications>(
    TaskType.SendNotifications);

builder.Services.AddTransient<IUserNotify, TgUserNotify>();
builder.Services.AddTransient<IUserNotify, PwaUserNotify>();

builder.Services.Configure<BackgroundTaskConfig>(builder.Configuration.GetSection("BackgroundTasks"));

builder.Services.Configure<TelegramBotClientConfig>(builder.Configuration.GetSection("Telegram"));
var telegramConfig = new TelegramBotClientConfig();
builder.Configuration.GetRequiredSection("Telegram").Bind(telegramConfig);
builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(telegramConfig.BotToken!));

builder.Services.Configure<AdditionalValuesConfig>(builder.Configuration.GetSection("AdditionalValues"));

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

var redisConfig = new RedisConfig();
builder.Configuration.GetRequiredSection(nameof(RedisConfig)).Bind(redisConfig);

builder.Services.AddDataProtection()
    .PersistKeysToStackExchangeRedis(
        ConnectionMultiplexer.Connect(redisConfig.ConnectionString),
        "DataProtection-Keys");

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(redisConfig.ConnectionString));

builder.Services.AddSignalR().AddStackExchangeRedis(redisConfig.ConnectionString);

builder.Services.AddStackExchangeRedisCache(options => { options.Configuration = redisConfig.ConnectionString; });

builder.Services.AddHostedService<BackgroundTaskManager>();

builder.Services.Configure<TtsConfig>(builder.Configuration.GetSection(nameof(TtsConfig)));
var ttsConfig = new TtsConfig();
builder.Configuration.GetRequiredSection(nameof(TtsConfig)).Bind(ttsConfig);

builder.Services.AddHttpClient();
builder.Services.AddHttpClient<ReadMangaClubClient>(client =>
    {
        client.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        client.DefaultRequestHeaders.Add("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
        client.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
        UseCookies = true,
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5
    })
    .AddResilienceHandler("custom-pipeline", (resilienceBuilder, context) =>
    {
        var logger = context.ServiceProvider.GetRequiredService<ILogger<ReadMangaClubClient>>();

        resilienceBuilder.AddRetry(new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(2),
            BackoffType = DelayBackoffType.Exponential,
            OnRetry = args =>
            {
                logger.LogWarning(
                    "Request to {url} failed. Retry attempt #{attempt}. Waiting {delay}s before next try. Reason: {reason}",
                    args.Outcome.Result?.RequestMessage?.RequestUri?.ToString() ?? "Unknown URL",
                    args.AttemptNumber + 1,
                    args.RetryDelay.TotalSeconds,
                    args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString() ?? "Unknown error");

                return ValueTask.CompletedTask;
            }
        });
    });
builder.Services.AddHttpClient<XttsTtsClient>(client =>
{
    client.BaseAddress = new Uri(ttsConfig.XttsUrl!);
    client.Timeout = TimeSpan.FromMinutes(2);
});
builder.Services.AddHttpClient<ClearTextTtsClient>(client =>
{
    client.BaseAddress = new Uri(ttsConfig.ClearTextUrl!);
    client.Timeout = TimeSpan.FromMinutes(2);
});
builder.Services.AddTransient<TtsClientFactory>();
builder.Services.AddTransient<ClearTextTtsService>();
builder.Services.AddTransient<TtsService>();

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

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = long.MaxValue;
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

builder.Services.AddControllers()
    .AddJsonOptions(options => { options.JsonSerializerOptions.PropertyNamingPolicy = null; });

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
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

builder.Services.Configure<VapidKeysConfig>(builder.Configuration.GetSection("VapidKeys"));

var app = builder.Build();

app.UseForwardedHeaders();

app.UseExceptionHandler("/Account/CustomNotFound");

app.UseHsts();

// HTTPS redirection handled by reverse proxy (nginx)
// app.UseHttpsRedirection();

app.UseWebSockets();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // await context.ScheduledTasks.ExecuteDeleteAsync();
    // await context.ScheduledTaskConfigs.ExecuteDeleteAsync();
    // await context.Notifications.ExecuteDeleteAsync();

    context.Database.Migrate();

    // var files = await context.Files.AsNoTracking()
    // .Where(f => f.CustomName == "Мир после падения" || f.CustomName == "Поднятие уровня в одиночку" ||
    // f.CurrentPartNumber > 309).ToListAsync();

    // await scope.ServiceProvider.GetRequiredService<FileService>()
    // .DeleteFilesAsync([.. files.Select(f => f.Id)], CancellationToken.None);

    var botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
    await botClient.SetWebhook(telegramConfig.WebhookUrl!, secretToken: telegramConfig.SecretToken!);

    // var startTime = Stopwatch.GetTimestamp();
    // var ttsService = scope.ServiceProvider.GetRequiredService<TtsService>();
    // var minioService = scope.ServiceProvider.GetRequiredService<MinioService>();
    //
    // var paths = new List<string>
    // {
    //     // @"C:\Users\Пользователь\Downloads\the_egyptian_myths_a_guide_to_the_ancient_gods_and_legends.fb2",
    //     @"C:\Users\Пользователь\Downloads\edith_hamilton_mythology_timeless_tales_of_gods_and_heroes.fb2"
    // };
    //
    // foreach (var path in paths)
    // {
    //     var doc = XDocument.Load(path);
    //     var ns = doc.Root!.GetDefaultNamespace();
    //
    //     // var counter = 0;
    //
    //     foreach (var el in doc.Root.Element(ns + "body")!.Descendants())
    //     {
    //         if (!el.Attributes().Select(f => f.Name).Select(f => f.LocalName).Contains("tts-id")) continue;
    //
    //         var ttsId = el.Attributes().First(f => f.Name.LocalName == "tts-id").Value;
    //         var ttsType = el.Attributes().First(f => f.Name.LocalName == "tts-type").Value;
    //
    //         // var text = string.Concat(
    //         //     el.Nodes()
    //         //         .OfType<XText>()
    //         //         .Select(node => node.Value)
    //         // ).Trim().Replace("\n", " ");
    //
    //         // if (++counter >= 255) break;
    //
    //         if (await minioService.ObjectExistsAsync(StaticNames.TtsBucketName, $"{ttsId}.ogg", CancellationToken.None))
    //         {
    //             Console.WriteLine();
    //             Console.WriteLine(
    //                 $"TTS {ttsId} skipped | TIME: {FormatPrettyTime(Stopwatch.GetElapsedTime(startTime))}");
    //             continue;
    //         }
    //
    //         Console.WriteLine();
    //         Console.WriteLine($"TTS attempt {ttsId}");
    //
    //         var res = await ttsService.GenerateSpeechAsync(el.Value, ttsType, ttsId, CancellationToken.None);
    //
    //         Console.WriteLine(
    //             $"TTS: {ttsId} {(res ? "done" : "error")} | TIME: {FormatPrettyTime(Stopwatch.GetElapsedTime(startTime))}");
    //     }
    // }
    //
    // static string FormatPrettyTime(TimeSpan ts)
    // {
    //     if (ts.TotalDays >= 1) return $"{ts.TotalDays:F1} days";
    //     if (ts.TotalHours >= 1) return $"{ts.TotalHours:F1} hours";
    //     if (ts.TotalMinutes >= 1) return $"{ts.TotalMinutes:F1} minutes";
    //     if (ts.TotalSeconds >= 1) return $"{ts.TotalSeconds:F2}s";
    //     if (ts.TotalMilliseconds >= 1) return $"{ts.TotalMilliseconds:F0}ms";
    //
    //     // Fallback for sub-millisecond precision
    //     return $"{ts.TotalMilliseconds * 1000:F0} µs";
    // }
}

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

//FOR LOCAL TTS
// app.UseCors("DevCorsPolicy");

app.Use(async (context, next) =>
{
    var randomNumber = new byte[32];
    using (var rng = RandomNumberGenerator.Create())
    {
        rng.GetBytes(randomNumber);
    }

    var nonce = Convert.ToBase64String(randomNumber).Replace("+", "").Replace("/", "").Replace("=", "");

    context.Items["ScriptNonce"] = nonce;

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
        context.Response.Headers.Remove("Reporting-Endpoints");

        var reverseProxyFeature = context.Features.Get<IReverseProxyFeature>();
        var isVpnRequest = context.Request.Path.StartsWithSegments("/vpn-admin") ||
                           reverseProxyFeature?.Route.Config.RouteId == "vpn-route";

        if (isVpnRequest)
        {
            context.Response.Headers.Append("Content-Security-Policy",
                "default-src 'self'; " +
                "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data: blob:; " +
                "connect-src 'self' wss: ws: https://githubusercontent.com; " +
                "frame-ancestors 'self';");

            context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
            context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
            context.Response.Headers.Append("Strict-Transport-Security",
                "max-age=31536000; includeSubDomains; preload");
        }
        else
        {
            context.Response.Headers.Append("Reporting-Endpoints", "csp-endpoint=\"/api/Csp/Report\"");

            context.Response.Headers.Append("Content-Security-Policy",
                // "default-src 'self'; " +
                // $"script-src 'self' 'nonce-{nonce}' 'strict-dynamic' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://unpkg.com; " +
                "style-src 'self' 'unsafe-inline'; " +
                "img-src 'self' data: blob:; " +
                "worker-src 'self' blob:; " +
                "font-src 'self' data:; " +
                "connect-src 'self' wss: ws:; " +
                "media-src 'self' blob:; " +
                "object-src 'none'; " +
                "base-uri 'self'; " +
                "form-action 'self'; " +
                "frame-ancestors 'self'; " +
                "report-uri /api/Csp/Report;" +
                "report-to csp-endpoint;");

            context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Append("X-Frame-Options", "DENY");
            context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
            context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

            context.Response.Headers.Append("Strict-Transport-Security",
                "max-age=31536000; includeSubDomains; preload");

            context.Response.Headers.Append("Cross-Origin-Embedder-Policy", "require-corp");
            //FOR LOCAL TTS
            // context.Response.Headers.Append("Cross-Origin-Embedder-Policy", "credentialless");
            context.Response.Headers.Append("Cross-Origin-Opener-Policy", "same-origin");

            context.Response.Headers.Append("Permissions-Policy",
                "camera=(), microphone=(), geolocation=(), fullscreen=(), payment=(), usb=(), accelerometer=(), gyroscope=()");
        }

        return Task.CompletedTask;
    });

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Map("/", () => Results.Redirect("/Home/Index"));
app.Map("/Home", () => Results.Redirect("/Home/Index"));

#region FOR_CVE_ONLINE_CHECK

app.MapGet("/.well-known/blacksight-domain-association", async context =>
{
    context.Response.ContentType = "text/plain; charset=utf-8";
    await context.Response.WriteAsync("blacksight-verification-code=094477df-9492-4fdf-aef9-da976b358344");
});
app.MapGet("/9e528635-b058-493e-abca-925c6068c1f1.txt", async context =>
{
    context.Response.ContentType = "text/plain; charset=utf-8";
    await context.Response.WriteAsync("Probely");
});

#endregion

app.MapHub<ScheduledTaskHub>("/ScheduledTaskHub");
app.MapHub<UserTgLinkHub>("/UserTgLinkHub");

app.Use((context, next) =>
{
    context.Request.EnableBuffering();
    return next();
});

app.MapReverseProxy();

app.Run();
