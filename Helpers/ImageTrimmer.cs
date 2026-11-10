using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace WebReader.Helpers;

public static class ImageTrimmer
{
    public static byte[] Trim(byte[] imageBytes, Color[]? emptyColors = null)
    {
        emptyColors ??= [Color.White, Color.Black];

        using var image = Image.Load<Rgb24>(imageBytes);

        var width = image.Width;
        var height = image.Height;

        var top = FindTopRow(image, emptyColors);
        var bottom = FindBottomRow(image, emptyColors);
        var left = FindLeftCol(image, emptyColors);
        var right = FindRightCol(image, emptyColors);

        var trimHeight = bottom - top + 1;
        var trimWidth = right - left + 1;

        if (trimHeight <= 0 || trimHeight > height || trimWidth <= 0 || trimWidth > width) return imageBytes;

        using var cropped = image.Clone(ctx =>
        {
            ctx.Crop(new Rectangle(left, top, trimWidth, trimHeight)).Resize(new Size(trimWidth, trimHeight));
        });

        using var outputMs = new MemoryStream();
        try
        {
            cropped.Save(outputMs, new JpegEncoder());
        }
        catch (Exception)
        {
            cropped.Save(outputMs, new PngEncoder());
        }

        var res = outputMs.ToArray();

        outputMs.Close();

        return res;
    }

    private static int FindTopRow(Image<Rgb24> image, Color[] emptyColors)
    {
        for (var y = 0; y < image.Height; y++)
            if (!IsRowEmpty(image, y, emptyColors))
                return y;

        return 0;
    }

    private static int FindBottomRow(Image<Rgb24> image, Color[] emptyColors)
    {
        for (var y = image.Height - 1; y >= 0; y--)
            if (!IsRowEmpty(image, y, emptyColors))
                return y;

        return image.Height - 1;
    }

    private static int FindLeftCol(Image<Rgb24> image, Color[] emptyColors)
    {
        for (var x = 0; x < image.Width; x++)
            if (!IsColumnEmpty(image, x, emptyColors))
                return x;

        return 0;
    }

    private static int FindRightCol(Image<Rgb24> image, Color[] emptyColors)
    {
        for (var x = image.Width - 1; x >= 0; x--)
            if (!IsColumnEmpty(image, x, emptyColors))
                return x;

        return image.Width - 1;
    }

    private static bool IsRowEmpty(Image<Rgb24> image, int y, Color[] emptyColors)
    {
        for (var x = 0; x < image.Width; x++)
        {
            var pixel = image[x, y];

            if (!StaticFunctions.IsColorMatch(pixel, emptyColors))
                return false;
        }

        return true;
    }

    private static bool IsColumnEmpty(Image<Rgb24> image, int x, Color[] emptyColors)
    {
        for (var y = 0; y < image.Height; y++)
        {
            var pixel = image[x, y];

            if (!StaticFunctions.IsColorMatch(pixel, emptyColors))
                return false;
        }

        return true;
    }
}
