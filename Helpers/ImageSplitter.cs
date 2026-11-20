using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace WebReader.Helpers;

public static class ImageSplitter
{
    public static List<byte[]> Split<T>(List<Image<Rgb24>> images, ILogger<T> logger, int minContentHeight = 50,
        int minEmptyGap = 100, int minBytesSize = 3000)
    {
        var result = new List<byte[]>();

        foreach (var image in images)
        {
            var y = FindNextFilledY(image, 0);

            while (y < image.Height)
            {
                var searchStart = Math.Min(image.Height, y + minContentHeight);
                var currEndY = FindEmptyGapY(image, searchStart, minEmptyGap);

                if (currEndY > y)
                {
                    var trimmedBytes = ImageTrimmer.Trim(image, 0, y, image.Width, currEndY, 5);

                    if (trimmedBytes.Length >= minBytesSize)
                    {
                        logger.LogInformation("Split {y}..{currEndY} / {height} ({bytes} bytes)", y, currEndY,
                            image.Height,
                            trimmedBytes.Length);
                        result.Add(trimmedBytes);
                    }
                }

                y = FindNextFilledY(image, currEndY + minEmptyGap);
            }
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
