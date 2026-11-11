namespace WebReader.Services.Http;

public class ReadMangaClubClient(HttpClient httpClient)
{
    public async Task<string> GetPageHtmlAsync(string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentNullException(nameof(url));

        var request = new HttpRequestMessage(HttpMethod.Get, url);

        var targetUri = new Uri(url);
        request.Headers.Referrer = new Uri($"{targetUri.Scheme}://{targetUri.Host}/");

        var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<string> GetChapterHtmlAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentNullException(nameof(url));

        // 1. Добавляем параметр mtr=true, если его еще нет, чтобы обойти заглушку 18+
        var uriBuilder = new UriBuilder(url);
        if (!uriBuilder.Query.Contains("mtr="))
            uriBuilder.Query = string.IsNullOrEmpty(uriBuilder.Query)
                ? "mtr=true"
                : $"{uriBuilder.Query.TrimStart('?')}&mtr=true";

        var targetUri = uriBuilder.Uri;
        var request = new HttpRequestMessage(HttpMethod.Get, targetUri);

        // 2. Формируем правильный Referer (страница самой манги, а не корень сайта)
        // Из ".../podniatie_urovnia_v_odinochku__A5ea4/vol1/32" получаем ".../podniatie_urovnia_v_odinochku__A5ea4"
        var segments = targetUri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var refererPath = segments.Length > 0 ? $"/{segments[0]}" : "/";
        request.Headers.Add("Referer", $"{targetUri.Scheme}://{targetUri.Host}{refererPath}");

        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<byte[]> GetImageByteArrayAsync(string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentNullException(nameof(url));

        var request = new HttpRequestMessage(HttpMethod.Get, url);

        var targetUri = new Uri(url);
        request.Headers.Add("Referer", $"{targetUri.Scheme}://{targetUri.Host}/");

        using var response =
            await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }
}
