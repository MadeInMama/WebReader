using System.Text.Json.Serialization;
using System.Web;

namespace WebReader.Services.Http;

public class MangaLibClient(HttpClient httpClient)
{
    public async Task<List<MangaLibChapterDto>> GetChaptersAsync(string mangaSlug,
        CancellationToken cancellationToken)
    {
        var url = $"https://api.cdnrulib.link/api/manga/{mangaSlug}/chapters";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Referer", "https://mangalib.org");
        request.Headers.Add("Origin", "https://mangalib.org");
        request.Headers.Add("Site-Id", "1");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var apiResponse = await response.Content.ReadFromJsonAsync<MangaLibChaptersResponse>(cancellationToken);

        if (apiResponse?.Data == null)
            return [];

        return apiResponse.Data.OrderBy(f => f.Index).Select(ch =>
        {
            // Берем первую доступную ветку перевода (если их несколько)
            var branchId = ch.Branches.FirstOrDefault()?.BranchId;
            var branchQuery = branchId.HasValue ? $"?bid={branchId.Value}" : "";

            // Формируем полную ссылку на главу
            var chapterUrl = $"https://mangalib.orgru/{mangaSlug}/read/v{ch.Volume}/c{ch.Number}{branchQuery}";

            return new MangaLibChapterDto(
                ch.Volume,
                ch.Number,
                ch.Index,
                ch.Name,
                chapterUrl
            );
        }).ToList();
    }

    public async Task<List<MangaLibCoverDto>> GetAllCoversAsync(string mangaSlug, CancellationToken cancellationToken)
    {
        var url = $"https://api.cdnrulib.link/api/manga/{mangaSlug}/covers";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Referer", "https://mangalib.org");
        request.Headers.Add("Origin", "https://mangalib.org");
        request.Headers.Add("Site-Id", "1");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var apiResponse = await response.Content.ReadFromJsonAsync<MangaLibCoversResponse>(cancellationToken);

        if (apiResponse?.Data == null)
            return new List<MangaLibCoverDto>();

        return apiResponse.Data
            .Where(item => !string.IsNullOrWhiteSpace(item.Cover?.Default))
            .Select(item => new MangaLibCoverDto(
                item.Id,
                item.Info,
                item.Order,
                EnsureAbsoluteUrl(item.Cover!.Default!),
                !string.IsNullOrWhiteSpace(item.Cover.Thumbnail)
                    ? EnsureAbsoluteUrl(item.Cover.Thumbnail)
                    : null
            ))
            .ToList();
    }

    private static string EnsureAbsoluteUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out _))
            return url;

        return $"https://imglib.info{url}";
    }

    public async Task<List<byte[]>> GetAllImagesBytesAsync(string readerUrl, int maxConcurrency,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(readerUrl, UriKind.Absolute, out var uri))
            throw new ArgumentException($"Invalid URL: {readerUrl}", nameof(readerUrl));

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var readIndex = Array.IndexOf(segments, "read");

        if (readIndex < 1 || readIndex + 2 >= segments.Length)
            throw new ArgumentException($"Invalid MangaLib reader URL format: {readerUrl}", nameof(readerUrl));

        var slug = segments[readIndex - 1].Trim('/');
        var volume = segments[readIndex + 1].TrimStart('v', 'V');
        var number = segments[readIndex + 2].TrimStart('c', 'C');

        var query = HttpUtility.ParseQueryString(uri.Query);
        var branchId = query["bid"];

        // 1. Безопасная сборка URL через string.Join (гарантирует наличие /api/manga/)
        var apiUrl = string.Join("/", "https://cdnrulib.link", "api", "manga", slug,
            $"chapter?number={number}&volume={volume}");
        if (!string.IsNullOrEmpty(branchId))
            apiUrl += $"&branch_id={branchId}";

        using var apiRequest = CreateApiRequest(apiUrl);
        using var apiResponse = await httpClient.SendAsync(apiRequest, cancellationToken);
        apiResponse.EnsureSuccessStatusCode();

        var chapterData = await apiResponse.Content.ReadFromJsonAsync<MangaLibChapterDetailResponse>(cancellationToken);
        var pages = chapterData?.Data?.Pages;

        if (pages == null || pages.Count == 0)
            return new List<byte[]>();

        // 2. Получаем актуальный CDN-сервер картинок (по умолчанию "https://cdnrulib.link")
        var imageHost = await GetImageServerHostAsync(cancellationToken);

        // 3. Скачиваем все страницы параллельно с ограничением потоков
        using var semaphore = new SemaphoreSlim(maxConcurrency);
        var downloadTasks = pages
            .OrderBy(p => p.PageNumber)
            .Select(async page =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var fullImageUrl = EnsureAbsoluteImageUrl(page.Url, imageHost);
                    return await DownloadImageBytesAsync(fullImageUrl, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });

        var imagesBytes = await Task.WhenAll(downloadTasks);
        return imagesBytes.ToList();
    }

    private async Task<string> GetImageServerHostAsync(CancellationToken cancellationToken)
    {
        try
        {
            var constantsUrl = string.Join("/", "https://cdnrulib.link", "api", "constants?fields[]=imageServers");
            using var request = CreateApiRequest(constantsUrl);
            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var constants = await response.Content.ReadFromJsonAsync<MangaLibConstantsResponse>(cancellationToken);

                var mainServer = constants?.Data?.ImageServers?
                    .Where(s => s.SiteIds.Contains(1) && !string.IsNullOrWhiteSpace(s.Url))
                    .FirstOrDefault(s => s.Id == "main" && !s.Url.Contains("api.", StringComparison.OrdinalIgnoreCase));

                if (mainServer != null)
                    return mainServer.Url;
            }
        }
        catch
        {
            // В случае ошибки используем проверенный CDN-домен
        }

        return "https://cdnrulib.link";
    }

    public async Task<byte[]> DownloadImageBytesAsync(string imageUrl, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, imageUrl);
        request.Headers.Add("Referer", "https://mangalib.org");

        using var response =
            await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private static HttpRequestMessage CreateApiRequest(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Referer", "https://mangalib.org");
        request.Headers.Add("Origin", "https://mangalib.org");
        request.Headers.Add("Site-Id", "1");
        return request;
    }

    private static string EnsureAbsoluteImageUrl(string rawPath, string defaultHost)
    {
        if (rawPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            rawPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return rawPath;

        var cleanHost = defaultHost.StartsWith("//")
            ? $"https:{defaultHost}"
            : defaultHost;

        cleanHost = cleanHost.TrimEnd('/');
        var cleanPath = rawPath.TrimStart('/');

        return string.Join("/", cleanHost, cleanPath);
    }
}

public class MangaLibChapterDetailResponse
{
    [JsonPropertyName("data")] public MangaLibChapterDetailData? Data { get; set; }
}

public class MangaLibChapterDetailData
{
    [JsonPropertyName("pages")] public List<MangaLibPageItem> Pages { get; set; } = new();
}

public class MangaLibPageItem
{
    [JsonPropertyName("p")] public int PageNumber { get; set; }

    [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;
}

public class MangaLibConstantsResponse
{
    [JsonPropertyName("data")] public MangaLibConstantsData? Data { get; set; }
}

public class MangaLibConstantsData
{
    [JsonPropertyName("imageServers")] public List<MangaLibImageServer>? ImageServers { get; set; }
}

public class MangaLibImageServer
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;

    [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;

    [JsonPropertyName("site_ids")] public List<int> SiteIds { get; set; } = new();
}

public record MangaLibChapterDto(
    string Volume,
    string Number,
    int Index,
    string? Name,
    string Url
);

public class MangaLibChaptersResponse
{
    [JsonPropertyName("data")] public List<MangaLibChapterItem> Data { get; set; } = new();
}

public class MangaLibChapterItem
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("volume")] public string Volume { get; set; } = string.Empty;

    [JsonPropertyName("number")] public string Number { get; set; } = string.Empty;

    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("index")] public int Index { get; set; }

    [JsonPropertyName("branches")] public List<MangaLibBranchItem> Branches { get; set; } = new();
}

public class MangaLibBranchItem
{
    [JsonPropertyName("branch_id")] public int? BranchId { get; set; }
}

public record MangaLibCoverDto(
    int Id,
    string Info,
    int? Order,
    string FullImageUrl,
    string? ThumbnailUrl
);

public class MangaLibCoversResponse
{
    [JsonPropertyName("data")] public List<MangaLibCoverItem> Data { get; set; } = new();
}

public class MangaLibCoverItem
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("info")] public string Info { get; set; } = string.Empty;

    [JsonPropertyName("order")] public int Order { get; set; }

    [JsonPropertyName("cover")] public MangaLibCoverUrls? Cover { get; set; }
}

public class MangaLibCoverUrls
{
    [JsonPropertyName("default")] public string? Default { get; set; }

    [JsonPropertyName("thumbnail")] public string? Thumbnail { get; set; }
}
