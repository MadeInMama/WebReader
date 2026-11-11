using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace WebReader.Helpers;

public static class ImageSplitter
{
    public static List<byte[]> Split<T>(Image<Rgb24> image, ILogger<T> logger, int minContentHeight = 100,
        int minEmptyGap = 25)
    {
        ArgumentNullException.ThrowIfNull(image);

        var result = new List<byte[]>();

        // Находим первую строку с контентом, пропуская пустоту в самом верху
        var y = FindNextFilledY(image, 0);

        while (y < image.Height)
        {
            // Ищем начало следующей пустой зоны, отступив минимальную высоту контента
            var searchStart = Math.Min(image.Height, y + minContentHeight);
            var currEndY = FindEmptyGapY(image, searchStart, minEmptyGap);

            if (currEndY > y && !ImageEmptyChecker.IsEmpty(image, 0, y, image.Width, currEndY))
            {
                logger.LogInformation("Split {y}..{currEndY} / {height}", y, currEndY, image.Height);

                // Передаем согласованные границы (min включительно, max не включительно)
                result.Add(ImageTrimmer.Trim(image, 0, y, image.Width, currEndY, 5));
            }

            // Перепрыгиваем найденный пустой коридор и ищем следующий блок контента
            y = FindNextFilledY(image, currEndY + minEmptyGap);
        }

        return result;
    }

    private static int FindNextFilledY(Image<Rgb24> image, int startY)
    {
        for (var y = Math.Max(0, startY); y < image.Height; y++)
            if (!ImageEmptyChecker.IsRowEmpty(image, y))
                return y;

        return image.Height;
    }

    /// <summary>
    ///     Ищет Y-координату, начиная с которой идет минимум <paramref name="minGap" /> пустых строк подряд.
    /// </summary>
    private static int FindEmptyGapY(Image<Rgb24> image, int startY, int minGap)
    {
        var consecutiveEmpty = 0;

        for (var y = Math.Max(0, startY); y < image.Height; y++)
            if (ImageEmptyChecker.IsRowEmpty(image, y))
            {
                consecutiveEmpty++;
                if (consecutiveEmpty >= minGap)
                    // Возвращаем начало пустого коридора
                    return y - minGap + 1;
            }
            else
            {
                consecutiveEmpty = 0;
            }

        return image.Height;
    }
}
