using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace WebReader.Helpers;

public static class ImageTrimmer
{
    public static byte[] TrimImageImageSharp(byte[] imageBytes, int tolerance = 5)
    {
        using var image = Image.Load<Rgb24>(imageBytes);

        var width = image.Width;
        var height = image.Height;

        var top = FindTopRow(image, image[0, 0], tolerance);
        var bottom = FindBottomRow(image, image[0, height - 1], tolerance);
        var left = FindLeftCol(image, image[0, 0], tolerance);
        var right = FindRightCol(image, image[width - 1, 0], tolerance);

        var trimHeight = bottom - top + 1;
        var trimWidth = right - left + 1;

        if (trimHeight <= 0 || trimHeight >= height || trimWidth <= 0 || trimWidth >= width) return imageBytes;

        using var cropped = image.Clone(ctx =>
        {
            ctx.Crop(new Rectangle(left, top, trimWidth, trimHeight)).Resize(new Size(trimWidth, trimHeight));
        });

        using var outputMs = new MemoryStream();
        cropped.Save(outputMs, new JpegEncoder());
        return outputMs.ToArray();
    }

    private static int FindTopRow(Image<Rgb24> image, Rgb24 fillColor, int tolerance)
    {
        for (var y = 0; y < image.Height; y++)
            if (!IsRowFilled(image, y, fillColor, tolerance))
                return y;

        return 0;
    }

    private static int FindBottomRow(Image<Rgb24> image, Rgb24 fillColor, int tolerance)
    {
        for (var y = image.Height - 1; y >= 0; y--)
            if (!IsRowFilled(image, y, fillColor, tolerance))
                return y;

        return image.Height - 1;
    }

    private static int FindLeftCol(Image<Rgb24> image, Rgb24 fillColor, int tolerance)
    {
        for (var x = 0; x < image.Width; x++)
            if (!IsColumnFilled(image, x, fillColor, tolerance))
                return x;

        return 0;
    }


    private static int FindRightCol(Image<Rgb24> image, Rgb24 fillColor, int tolerance)
    {
        for (var x = image.Width - 1; x >= 0; x--)
            if (!IsColumnFilled(image, x, fillColor, tolerance))
                return x;

        return image.Width - 1;
    }

    private static bool IsRowFilled(Image<Rgb24> image, int y, Rgb24 fillColor, int tolerance)
    {
        for (var x = 0; x < image.Width; x++)
        {
            var pixel = image[x, y];

            if (Math.Abs(pixel.R - fillColor.R) > tolerance ||
                Math.Abs(pixel.G - fillColor.G) > tolerance ||
                Math.Abs(pixel.B - fillColor.B) > tolerance)
                return false;
        }

        return true;
    }

    private static bool IsColumnFilled(Image<Rgb24> image, int x, Rgb24 fillColor, int tolerance)
    {
        for (var y = 0; y < image.Height; y++)
        {
            var pixel = image[x, y];

            if (!IsColorMatch(Color.FromRgb(pixel.R, pixel.G, pixel.B), fillColor, tolerance)) return false;
        }

        return true;
    }

    private static bool IsColorMatch(Rgb24 c1, Rgb24 c2, int tolerance)
    {
        return Math.Abs(c1.R - c2.R) <= tolerance &&
               Math.Abs(c1.G - c2.G) <= tolerance &&
               Math.Abs(c1.B - c2.B) <= tolerance;
    }
}
