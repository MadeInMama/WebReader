using Microsoft.Extensions.Options;
using WebReader.Configuration;

namespace WebReader.Services;

public class LinkGenerationService(
    IOptions<AdditionalValuesConfig> additionalOptionsConfig,
    ILogger<LinkGenerationService> logger)
{
    public string? GenerateGetFileUrl(Guid bucketId, Guid fileId, bool isAbsolute = true)
    {
        try
        {
            var query = QueryString.Create(new Dictionary<string, string?>
            {
                { "bucketId", bucketId.ToString() },
                { "fileId", fileId.ToString() }
            });

            const string routePath = "File/GetFile";

            if (!isAbsolute) return $"/{routePath}{query}";

            var externalHostSetting = additionalOptionsConfig.Value.ExternalHost;

            if (string.IsNullOrWhiteSpace(externalHostSetting))
            {
                logger.LogWarning("ExternalHost configuration is empty. Cannot generate absolute URL");
                return null;
            }

            if (!externalHostSetting.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !externalHostSetting.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                externalHostSetting = $"https://{externalHostSetting}";

            var baseUri = new Uri(externalHostSetting);

            var uriBuilder = new UriBuilder(baseUri)
            {
                Path = $"{baseUri.AbsolutePath.TrimEnd('/')}/{routePath}",
                Query = query.ToString()
            };

            return uriBuilder.Uri.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating file URL for bucket {bucketId} and file {fileId}", bucketId, fileId);
            return null;
        }
    }
}
