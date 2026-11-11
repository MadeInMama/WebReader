using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace WebReader.Helpers;

public static class ImageTrimmer
{
    public static byte[] Trim(Image<Rgb24> image, int minX, int minY, int maxX, int maxY, int offset)
    {
        ArgumentNullException.ThrowIfNull(image);

        // Нормализуем входные границы (min включительно, max не включительно)
        minX = Math.Clamp(minX, 0, image.Width);
        maxX = Math.Clamp(maxX, 0, image.Width);
        minY = Math.Clamp(minY, 0, image.Height);
        maxY = Math.Clamp(maxY, 0, image.Height);

        if (maxX <= minX || maxY <= minY)
            return [];

        // Находим реальные границы контента (индексы пикселей включительно)
        var contentTop = FindTopRow(image, minY, maxY);
        var contentBottom = FindBottomRow(image, minY, maxY);

        // Если вся область пустая
        if (contentTop > contentBottom)
            return [];

        // Передаем contentBottom + 1, так как IsColumnEmpty работает до maxY не включительно
        var contentLeft = FindLeftCol(image, minX, maxX, contentTop, contentBottom + 1);
        var contentRight = FindRightCol(image, minX, maxX, contentTop, contentBottom + 1);

        if (contentLeft > contentRight)
            return [];

        // Применяем безопасный отступ (offset) со всех 4 сторон
        var top = Math.Max(0, contentTop - offset);
        var bottom = Math.Min(image.Height - 1, contentBottom + offset);
        var left = Math.Max(0, contentLeft);
        var right = Math.Min(image.Width - 1, contentRight);

        var trimWidth = right - left + 1;
        var trimHeight = bottom - top + 1;

        if (trimWidth <= 0 || trimHeight <= 0)
            return [];

        return StaticFunctions.GetCropped(image, left, top, trimWidth, trimHeight);
    }

    private static int FindTopRow(Image<Rgb24> image, int minY, int maxY)
    {
        for (var y = minY; y < maxY; y++)
            if (!ImageEmptyChecker.IsRowEmpty(image, y))
                return y;

        return maxY;
    }

    private static int FindBottomRow(Image<Rgb24> image, int minY, int maxY)
    {
        for (var y = maxY - 1; y >= minY; y--)
            if (!ImageEmptyChecker.IsRowEmpty(image, y))
                return y;

        return minY - 1;
    }

    private static int FindLeftCol(Image<Rgb24> image, int minX, int maxX, int minY, int maxY)
    {
        for (var x = minX; x < maxX; x++)
            if (!ImageEmptyChecker.IsColumnEmpty(image, x, minY, maxY))
                return x;

        return maxX;
    }

    private static int FindRightCol(Image<Rgb24> image, int minX, int maxX, int minY, int maxY)
    {
        for (var x = maxX - 1; x >= minX; x--)
            if (!ImageEmptyChecker.IsColumnEmpty(image, x, minY, maxY))
                return x;

        return minX - 1;
    }
}
