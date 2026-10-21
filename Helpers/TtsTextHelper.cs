using System.Text.RegularExpressions;
using System.Web;

namespace WebReader.Helpers;

public static partial class TtsTextHelper
{
    private static readonly Regex HtmlRegex = HtmlRegexAttr();
    private static readonly Regex FootnotesRegex = FootnotesRegexAttr();
    private static readonly Regex RoundFootnotesRegex = RoundFootnotesRegexAttr();
    private static readonly Regex MultiSpaceRegex = MultiSpaceRegexAttr();
    private static readonly Regex SentenceSplitRegex = SentenceSplitRegexAttr();

    public static string PrepareTextForTts(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;

        // 1. Декодируем спецсимволы (напр. &nbsp; в пробел) и удаляем HTML-теги
        var result = HttpUtility.HtmlDecode(rawText);
        result = HtmlRegex.Replace(result, " ");

        // 2. Удаляем сноски, (12), чтобы робот их не читал вслух
        result = FootnotesRegex.Replace(result, "");
        result = RoundFootnotesRegex.Replace(result, "");

        // 3. Форматируем знаки препинания для правильных пауз модели
        result = result
            .Replace("—", " — ")
            .Replace("–", " — ")
            .Replace("...", "…")
            .Replace("«", "\"")
            .Replace("»", "\"")
            .Replace("*", "")
            .Replace("_", "");

        // 4. Схлопываем лишние пробелы
        return MultiSpaceRegex.Replace(result, " ").Trim();
    }

    public static List<string> SplitLongText(string text, int maxChunkLength = 400)
    {
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return chunks;

        var sentences = SentenceSplitRegex.Split(text);
        var currentChunk = "";

        foreach (var sentence in sentences)
        {
            if (sentence.Length > maxChunkLength)
            {
                if (!string.IsNullOrEmpty(currentChunk))
                {
                    chunks.Add(currentChunk.Trim());
                    currentChunk = "";
                }

                chunks.Add(sentence.Trim());
                continue;
            }

            if ((currentChunk + " " + sentence).Length > maxChunkLength)
            {
                chunks.Add(currentChunk.Trim());
                currentChunk = sentence;
            }
            else
            {
                currentChunk = string.IsNullOrEmpty(currentChunk) ? sentence : currentChunk + " " + sentence;
            }
        }

        if (!string.IsNullOrEmpty(currentChunk)) chunks.Add(currentChunk.Trim());

        return chunks;
    }

    [GeneratedRegex("<[^>]*>", RegexOptions.Compiled)]
    private static partial Regex HtmlRegexAttr();

    [GeneratedRegex(@"\[[^\]]+\]", RegexOptions.Compiled)]
    private static partial Regex FootnotesRegexAttr();

    [GeneratedRegex(@"\(\d+\)", RegexOptions.Compiled)]
    private static partial Regex RoundFootnotesRegexAttr();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex MultiSpaceRegexAttr();

    [GeneratedRegex(@"(?<=[.!?])\s+", RegexOptions.Compiled)]
    private static partial Regex SentenceSplitRegexAttr();
}
