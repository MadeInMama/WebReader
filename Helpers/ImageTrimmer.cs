using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace WebReader.Helpers;

public static class ImageTrimmer
{
    public static byte[] Trim(Image<Rgb24> image, int minX, int minY, int maxX, int maxY, Color[] emptyColors,
        int offset)
    {
        var top = FindTopRow(image, minY, maxY, emptyColors) - offset;
        if (top < 0) top = 0;
        var bottom = FindBottomRow(image, minY, maxY, emptyColors) + offset;
        if (bottom > image.Height - 1) bottom = image.Height - 1;
        var left = FindLeftCol(image, minX, maxX, top, bottom, emptyColors) + 1;
        if (left < 0) left = 0;
        var right = FindRightCol(image, minX, maxX, top, bottom, emptyColors) - 1;
        if (right > image.Width - 1) right = image.Width - 1;

        var trimHeight = bottom - top + 1;
        var trimWidth = right - left + 1;

        if (trimWidth < 0 || trimHeight < 0)
            return [];

        return StaticFunctions.GetCropped(image, left, top, trimWidth, trimHeight);
    }

    private static int FindTopRow(Image<Rgb24> image, int minY, int maxY, Color[] emptyColors)
    {
        for (var y = minY; y < maxY; y++)
            if (!ImageEmptyChecker.IsRowEmpty(image, y, emptyColors))
                return y;

        return minY;
    }

    private static int FindBottomRow(Image<Rgb24> image, int minY, int maxY, Color[] emptyColors)
    {
        for (var y = maxY; y >= minY; y--)
            if (!ImageEmptyChecker.IsRowEmpty(image, y, emptyColors))
                return y;

        return maxY;
    }

    private static int FindLeftCol(Image<Rgb24> image, int minX, int maxX, int minY, int maxY, Color[] emptyColors)
    {
        for (var x = minX; x < maxX; x++)
            if (!ImageEmptyChecker.IsColumnEmpty(image, x, minY, maxY, emptyColors))
                return x;

        return maxX;
    }

    private static int FindRightCol(Image<Rgb24> image, int minX, int maxX, int minY, int maxY, Color[] emptyColors)
    {
        for (var x = maxX; x >= minX; x--)
            if (!ImageEmptyChecker.IsColumnEmpty(image, x, minY, maxY, emptyColors))
                return x;

        return maxX;
    }
}
