using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace WebReader.Helpers;

public static class ImageEmptyChecker
{
    public static bool IsEmpty(Image<Rgb24> image, int startX, int startY, int endX, int endY,
        double percentEmptiness = 1)
    {
        ArgumentNullException.ThrowIfNull(image);

        startX = Math.Clamp(startX, 0, image.Width);
        endX = Math.Clamp(endX, 0, image.Width);
        startY = Math.Clamp(startY, 0, image.Height);
        endY = Math.Clamp(endY, 0, image.Height);

        var totalCount = (long)(endX - startX) * (endY - startY);
        if (totalCount <= 0) return true;

        var averageColor = GetAverageAreaColor(image, startX, startY, endX, endY);
        long nonMatchCount = 0;

        image.ProcessPixelRows(accessor =>
        {
            for (var y = startY; y < endY; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = startX; x < endX; x++)
                    if (!StaticFunctions.IsColorMatch(row[x], averageColor, 50))
                        nonMatchCount++;
            }
        });

        return (double)nonMatchCount * 100 / totalCount < percentEmptiness;
    }

    public static bool IsRowEmpty(Image<Rgb24> image, int y, double percentEmptiness = 1)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (image.Width == 0 || y < 0 || y >= image.Height)
            return true;

        var averageColor = GetAverageRowColor(image, y);
        long nonMatchCount = 0;

        image.ProcessPixelRows(accessor =>
        {
            var row = accessor.GetRowSpan(y);
            foreach (var r in row)
                if (!StaticFunctions.IsColorMatch(r, averageColor))
                    nonMatchCount++;
        });

        return (double)nonMatchCount * 100 / image.Width < percentEmptiness;
    }

    public static bool IsColumnEmpty(Image<Rgb24> image, int x, int minY, int maxY, double percentEmptiness = 5)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (x < 0 || x >= image.Width)
            return true;

        minY = Math.Clamp(minY, 0, image.Height);
        maxY = Math.Clamp(maxY, 0, image.Height);

        var totalCount = maxY - minY;
        if (totalCount <= 0) return true;

        var averageColor = GetAverageColumnColor(image, x, minY, maxY);
        long nonMatchCount = 0;

        image.ProcessPixelRows(accessor =>
        {
            for (var y = minY; y < maxY; y++)
            {
                var pixel = accessor.GetRowSpan(y)[x];
                if (!StaticFunctions.IsColorMatch(pixel, averageColor)) nonMatchCount++;
            }
        });

        return (double)nonMatchCount * 100 / totalCount < percentEmptiness;
    }

    public static Rgb24 GetAverageAreaColor(Image<Rgb24> image, int startX, int startY, int endX, int endY)
    {
        ArgumentNullException.ThrowIfNull(image);

        startX = Math.Clamp(startX, 0, image.Width);
        endX = Math.Clamp(endX, 0, image.Width);
        startY = Math.Clamp(startY, 0, image.Height);
        endY = Math.Clamp(endY, 0, image.Height);

        var count = (long)(endX - startX) * (endY - startY);
        if (count <= 0) return default;

        long totalR = 0, totalG = 0, totalB = 0;

        image.ProcessPixelRows(accessor =>
        {
            for (var y = startY; y < endY; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = startX; x < endX; x++)
                {
                    ref readonly var pixel = ref row[x];
                    totalR += pixel.R;
                    totalG += pixel.G;
                    totalB += pixel.B;
                }
            }
        });

        return new Rgb24(
            (byte)(totalR / count),
            (byte)(totalG / count),
            (byte)(totalB / count)
        );
    }

    public static Rgb24 GetAverageRowColor(Image<Rgb24> image, int y)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (image.Width == 0 || y < 0 || y >= image.Height)
            return default;

        long totalR = 0, totalG = 0, totalB = 0;

        image.ProcessPixelRows(accessor =>
        {
            var row = accessor.GetRowSpan(y);
            foreach (var r in row)
            {
                ref readonly var pixel = ref r;
                totalR += pixel.R;
                totalG += pixel.G;
                totalB += pixel.B;
            }
        });

        return new Rgb24(
            (byte)(totalR / image.Width),
            (byte)(totalG / image.Width),
            (byte)(totalB / image.Width)
        );
    }

    public static Rgb24 GetAverageColumnColor(Image<Rgb24> image, int x, int minY, int maxY)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (x < 0 || x >= image.Width)
            return default;

        minY = Math.Clamp(minY, 0, image.Height);
        maxY = Math.Clamp(maxY, 0, image.Height);

        var count = maxY - minY;
        if (count <= 0) return default;

        long totalR = 0, totalG = 0, totalB = 0;

        image.ProcessPixelRows(accessor =>
        {
            for (var y = minY; y < maxY; y++)
            {
                ref readonly var pixel = ref accessor.GetRowSpan(y)[x];
                totalR += pixel.R;
                totalG += pixel.G;
                totalB += pixel.B;
            }
        });

        return new Rgb24(
            (byte)(totalR / count),
            (byte)(totalG / count),
            (byte)(totalB / count)
        );
    }
}
