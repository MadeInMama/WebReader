using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace WebReader.Controllers;

public class CspReportRequest
{
    [JsonPropertyName("csp-report")] public CspReportDetails CspReport { get; set; } = new();
}

public class CspReportDetails
{
    [JsonPropertyName("document-uri")] public string DocumentUri { get; set; } = string.Empty;

    [JsonPropertyName("referrer")] public string Referrer { get; set; } = string.Empty;

    [JsonPropertyName("blocked-uri")] public string BlockedUri { get; set; } = string.Empty;

    [JsonPropertyName("violated-directive")]
    public string ViolatedDirective { get; set; } = string.Empty;

    [JsonPropertyName("effective-directive")]
    public string EffectiveDirective { get; set; } = string.Empty;

    [JsonPropertyName("original-policy")] public string OriginalPolicy { get; set; } = string.Empty;

    [JsonPropertyName("status-code")] public int StatusCode { get; set; }

    [JsonPropertyName("script-sample")] public string ScriptSample { get; set; } = string.Empty;
}

[ApiController]
[Route("api/[controller]/[action]")]
public class CspController(ILogger<CspController> logger) : ControllerBase
{
    [HttpPost]
    [Consumes("application/csp-report", "application/json")]
    public async Task<IActionResult> Report()
    {
        // Читаем сырое тело запроса напрямую из HTTP-контекста
        using var reader = new StreamReader(Request.Body);
        var rawJson = await reader.ReadToEndAsync();

        if (string.IsNullOrEmpty(rawJson))
        {
            logger.LogWarning("Получен пустой CSP-отчет");
            return BadRequest("Report body cannot be empty.");
        }

        try
        {
            // Десериализуем строку вручную
            var request = JsonSerializer.Deserialize<CspReportRequest>(rawJson);
            var report = request?.CspReport;

            if (report != null)
                logger.LogWarning(
                    "CSP Violation: Document: {documentUri} | Blocked: {blockedUri} | Directive: {violatedDirective}",
                    report.DocumentUri, report.BlockedUri, report.ViolatedDirective);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Ошибка парсинга CSP JSON отчета. Сырые данные: {RawJson}", rawJson);
        }

        return Ok();
    }
}
