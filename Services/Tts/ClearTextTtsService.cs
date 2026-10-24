using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using WebReader.Helpers;

namespace WebReader.Services.Tts;

public partial class ClearTextTtsService(TtsClientFactory ttsClientFactory, ILogger<ClearTextTtsService> logger)
{
    public async Task<string> ClearTextAsync(string text, string speaker, CancellationToken cancellationToken)
    {
        var preparedText = TtsTextHelper.PrepareTextForTts(text);

        if (text.Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries).Length < int.MaxValue)
            return preparedText;

        var response = await ttsClientFactory.GetClient(TtsProvider.ClearText)
            .ProceedAsync(preparedText, speaker, cancellationToken);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken);
        var fullAiResponse = jsonResponse?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();

        ArgumentNullException.ThrowIfNull(fullAiResponse);

        var match = MyRegex().Match(fullAiResponse);

        string? res;

        if (match.Success)
        {
            res = match.Groups[1].Value.Trim();
        }
        else
        {
            var altMatch = MyRegex1().Match(fullAiResponse);
            res = altMatch.Success ? altMatch.Groups[1].Value.Trim() : fullAiResponse.Trim();
        }

        ArgumentNullException.ThrowIfNull(res);

        return TtsTextHelper.PrepareTextForTts(res);
    }

    // Находим текст между тегами [START_TEXT] и [END_TEXT] с помощью регулярного выражения
    [GeneratedRegex(
        @"(?:\[(?:START_TEXT|СТАРТ_ТЕКСТ|STARTTEXT|СТАРТТЕКСТ)\](.*?)\[(?:END_TEXT|СТОП_ТЕКСТ|ENDTEXT|СТОПТЕКСТ)\]|\((?:START_TEXT|СТАРТ_ТЕКСТ|STARTTEXT|СТАРТТЕКСТ)\)(.*?)\((?:END_TEXT|СТОП_ТЕКСТ|ENDTEXT|СТОПТЕКСТ)\)|\{(?:START_TEXT|СТАРТ_ТЕКСТ|STARTTEXT|СТАРТТЕКСТ)\}(.*?)\{(?:END_TEXT|СТОП_ТЕКСТ|ENDTEXT|СТОПТЕКСТ)\})",
        RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex MyRegex();


    // Резервный вариант, если модель забыла про теги, но выдала кавычки < >
    [GeneratedRegex(@"<(.*?)>", RegexOptions.Singleline)]
    private static partial Regex MyRegex1();
}
