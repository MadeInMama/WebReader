using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace WebReader.Helpers;

public static class ImageTrimmer
{
    public static byte[] TrimImage(byte[] imageBytes, int tolerance = 20)
    {
        using var image = Image.Load<Rgb24>(imageBytes);

        var width = image.Width;
        var height = image.Height;

        var top = FindTopRow(image, tolerance);
        var bottom = FindBottomRow(image, tolerance);
        var left = FindLeftCol(image, image[0, 0], tolerance);
        var right = FindRightCol(image, image[width - 1, 0], tolerance);

        var trimHeight = bottom - top + 1;
        var trimWidth = right - left + 1;

        if (trimHeight <= 0 || trimHeight > height || trimWidth <= 0 || trimWidth > width) return imageBytes;

        using var cropped = image.Clone(ctx =>
        {
            ctx.Crop(new Rectangle(left, top, trimWidth, trimHeight)).Resize(new Size(trimWidth, trimHeight));
        });

        using var outputMs = new MemoryStream();
        cropped.Save(outputMs, new JpegEncoder());

        var res = outputMs.ToArray();

        outputMs.Close();

        return res;
    }

    private static int FindTopRow(Image<Rgb24> image, int tolerance)
    {
        for (var y = 0; y < image.Height; y++)
            if (!IsRowEmpty(image, y, tolerance))
                return y;

        return 0;
    }

    private static int FindBottomRow(Image<Rgb24> image, int tolerance)
    {
        for (var y = image.Height - 1; y >= 0; y--)
            if (!IsRowEmpty(image, y, tolerance))
                return y;

        return image.Height - 1;
    }

    private static int FindLeftCol(Image<Rgb24> image, Rgb24 fillColor, int tolerance)
    {
        for (var x = 0; x < image.Width; x++)
            if (!IsColumnEmpty(image, x, fillColor, tolerance))
                return x;

        return 0;
    }


    private static int FindRightCol(Image<Rgb24> image, Rgb24 fillColor, int tolerance)
    {
        for (var x = image.Width - 1; x >= 0; x--)
            if (!IsColumnEmpty(image, x, fillColor, tolerance))
                return x;

        return image.Width - 1;
    }

    private static bool IsRowEmpty(Image<Rgb24> image, int y, int tolerance)
    {
        var fillColor = image[image.Width / 2, y];

        for (var x = 0; x < image.Width; x++)
        {
            var pixel = image[x, y];

            if (!StaticFunctions.IsColorMatch(Color.FromRgb(pixel.R, pixel.G, pixel.B), fillColor, tolerance))
                return false;
        }

        return true;
    }

    private static bool IsColumnEmpty(Image<Rgb24> image, int x, Rgb24 fillColor, int tolerance)
    {
        for (var y = 0; y < image.Height; y++)
        {
            var pixel = image[x, y];

            if (!StaticFunctions.IsColorMatch(Color.FromRgb(pixel.R, pixel.G, pixel.B), fillColor, tolerance))
                return false;
        }

        return true;
    }
}
