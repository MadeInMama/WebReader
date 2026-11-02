using Microsoft.Extensions.Options;
using WebReader.Configuration;

namespace WebReader.Services;

public class LinkGenerationService(
    IOptions<AdditionalValuesConfig> additionalOptionsConfig,
    ILogger<LinkGenerationService> logger)
{
    public string? GenerateGetFileUrl(Guid bucketId, Guid fileId)
    {
        var externalHostSetting = additionalOptionsConfig.Value.ExternalHost;

        if (string.IsNullOrWhiteSpace(externalHostSetting))
        {
            logger.LogWarning("ExternalHost configuration is empty");
            return null;
        }

        try
        {
            if (!externalHostSetting.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !externalHostSetting.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                externalHostSetting = $"https://{externalHostSetting}";

            var baseUri = new Uri(externalHostSetting);

            var query = QueryString.Create(new Dictionary<string, string?>
            {
                { "bucketId", bucketId.ToString() },
                { "fileId", fileId.ToString() }
            });

            var uriBuilder = new UriBuilder(baseUri)
            {
                Path = $"{baseUri.AbsolutePath.TrimEnd('/')}/File/GetFile",
                Query = query.ToString()
            };

            return uriBuilder.Uri.ToString();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error manually generating file URL for bucket {bucketId}", bucketId);
            return null;
        }
    }
}
