using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace WebReader.Helpers;

public static class ImageEmptyChecker
{
    public static bool IsEmpty(Image<Rgb24> image, int startX, int startY, int endX, int endY, int percentEmptiness = 5)
    {
        var matchingRes = new List<bool>();

        var pixelTarget = image[image.Width / 2, image.Height / 2];

        for (var x = startX; x < endX; x++)
        for (var y = startY; y < endY; y++)
        {
            var pixel = image[x, y];

            matchingRes.Add(StaticFunctions.IsColorMatch(pixel, pixelTarget));
        }

        if (matchingRes.Count == 0) return true;

        return matchingRes.Count(f => !f) * 100 / matchingRes.Count < percentEmptiness;
    }

    public static bool IsRowEmpty(Image<Rgb24> image, int y, Color[] emptyColors, int percentEmptiness = 1)
    {
        var matchingRes = new List<bool>();

        for (var x = 0; x < image.Width; x++)
        {
            var pixel = image[x, y];

            matchingRes.Add(StaticFunctions.IsColorMatch(pixel, emptyColors));
        }

        if (matchingRes.Count == 0) return true;

        return matchingRes.Count(f => !f) * 100 / matchingRes.Count < percentEmptiness;
    }

    public static bool IsColumnEmpty(Image<Rgb24> image, int x, int minY, int maxY, Color[] emptyColors,
        int percentEmptiness = 10)
    {
        var matchingRes = new List<bool>();

        for (var y = minY; y < maxY; y++)
        {
            var pixel = image[x, y];

            matchingRes.Add(StaticFunctions.IsColorMatch(pixel, emptyColors));
        }

        if (matchingRes.Count == 0) return true;

        return matchingRes.Count(f => !f) * 100 / matchingRes.Count < percentEmptiness;
    }
}
