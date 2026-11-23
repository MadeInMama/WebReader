using System.Text.Json;
using System.Text.RegularExpressions;

namespace WebReader.Services.Http;

public class MangaLibClient(HttpClient httpClient)
{
    private const string ApiBaseUrl = "https://api.cdnlibs.org/api";
    private const string ImageHost = "https://img3.cdnlibs.org";

    /// <summary>
    ///     2. Получает список всех глав (с метаданными, необходимыми для загрузки картинок).
    /// </summary>
    public async Task<List<ChapterInfo>> GetChaptersAsync(string mangaUrl, CancellationToken cancellationToken)
    {
        var slug = ExtractSlug(mangaUrl);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var response = await httpClient.GetFromJsonAsync<ChaptersApiResponse>(
            $"{ApiBaseUrl}/manga/{slug}/chapters?site_id[]=2", options, cancellationToken);

        if (response?.data == null) return [];

        return response.data
            .OrderBy(c => c.index ?? 0)
            .Select(c => new ChapterInfo(
                slug,
                c.volume ?? "0",
                c.number ?? "0",
                c.name ?? c.number ?? "Без названия",
                (c.branches?.FirstOrDefault()?.branch_id ?? c.branches?.FirstOrDefault()?.id)!.Value,
                c.index!.Value
            ))
            .ToList();
    }

    /// <summary>
    ///     3. Получает список прямых ссылок на изображения для конкретной главы.
    /// </summary>
    public async Task<List<string>> GetChapterImagesAsync(ChapterInfo chapter, CancellationToken cancellationToken)
    {
        var queryParams = new List<string>
        {
            $"number={chapter.Number}",
            $"volume={chapter.Volume}",
            "site_id[]=2"
        };

        queryParams.Add($"branch_id={chapter.BranchId}");

        var url = $"{ApiBaseUrl}/manga/{chapter.Slug}/chapter?{string.Join("&", queryParams)}";
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var response = await httpClient.GetFromJsonAsync<ChapterDetailApiResponse>(url, options, cancellationToken);

        if (response?.data?.pages == null) return new List<string>();

        return response.data.pages
            .Where(p => !string.IsNullOrEmpty(p.url))
            .Select(p => GetFullImageUrl(p.url))
            .ToList();
    }

    /// <summary>
    ///     4. Скачивает одно изображение по URL и возвращает его в виде массива байтов.
    /// </summary>
    public async Task<byte[]> DownloadImageAsync(string imageUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(imageUrl))
            throw new ArgumentException("URL изображения не может быть пустым.", nameof(imageUrl));

        // HttpClient уже содержит нужные заголовки (User-Agent, Referer),
        // которые необходимы MangaLib для отдачи картинок (защита от hotlinking).
        try
        {
            return await httpClient.GetByteArrayAsync(imageUrl, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new HttpRequestException(
                $"Не удалось загрузить изображение по адресу {imageUrl}. Возможно, требуется авторизация или изменились правила доступа.",
                ex);
        }
    }

    /// <summary>
    ///     5. Скачивает все изображения указанной главы и возвращает их в виде массивов байтов.
    /// </summary>
    /// <returns>Список кортежей, содержащих номер страницы, URL и байтовый массив изображения.</returns>
    public async Task<List<ImageData>> GetChapterImagesBytesAsync(ChapterInfo chapter,
        CancellationToken cancellationToken)
    {
        // 1. Сначала получаем список URL-адресов
        var imageUrls = await GetChapterImagesAsync(chapter, cancellationToken);

        if (!imageUrls.Any())
            return new List<ImageData>();

        var result = new List<ImageData>();

        // 2. Последовательно скачиваем каждое изображение
        // Примечание: Для очень больших глав лучше использовать асинхронный поток (Stream),
        // чтобы избежать исключения OutOfMemoryException при хранении всех картинок в оперативной памяти.
        for (var i = 0; i < imageUrls.Count; i++)
        {
            var url = imageUrls[i];
            var imageBytes = await DownloadImageAsync(url, cancellationToken);

            result.Add(new ImageData(
                i + 1,
                url,
                imageBytes
            ));
        }

        return result;
    }

    public record ChapterInfo(string Slug, string Volume, string Number, string Name, int BranchId, int Index);

    public record ImageData(int PageIndex, string Url, byte[] Bytes);

    #region Private Helpers

    private string ExtractSlug(string url)
    {
        var match = Regex.Match(url, @"/(?:ru/)?(?:manga/)?(\d+--[\w\-]+)");
        return match.Success
            ? match.Groups[1].Value
            : throw new ArgumentException("Не удалось извлечь slug из URL.", nameof(url));
    }

    private string GetFullImageUrl(string rawUrl)
    {
        if (string.IsNullOrEmpty(rawUrl)) return string.Empty;

        // API может возвращать URL вида "//img..." или "/uploads/..."
        var path = rawUrl.StartsWith("//") ? rawUrl.Substring(1) : rawUrl;
        if (!path.StartsWith("/")) path = "/" + path;

        return ImageHost + path;
    }

    #endregion

    #region Internal API Models (для десериализации)

    private class ChaptersApiResponse
    {
        public List<ApiChapter>? data { get; set; }
    }

    private class ApiChapter
    {
        public string? volume { get; set; }
        public string? number { get; set; }
        public string? name { get; set; }
        public int? index { get; set; }
        public List<ApiBranch>? branches { get; set; }
    }

    private class ApiBranch
    {
        public int? branch_id { get; set; }
        public int? id { get; set; }
    }

    private class ChapterDetailApiResponse
    {
        public ChapterDetailData? data { get; set; }
    }

    private class ChapterDetailData
    {
        public List<ApiPage>? pages { get; set; }
    }

    private class ApiPage
    {
        public string? url { get; set; }
    }

    #endregion
}
