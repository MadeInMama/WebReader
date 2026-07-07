using System.Collections.Immutable;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace WebReader.Helpers;

public static class ImageSplitter
{
    public static ImmutableList<byte[]> SplitImage(byte[] imageBytes, int gap = 600, int tolerance = 5)
    {
        using var image = Image.Load<Rgb24>(ImageTrimmer.TrimImage(imageBytes));

        var result = new List<byte[]>();

        for (var y = 0; y < image.Height - gap; y++)
        {
            var currEndY = FindNextEmptyY(image, y + gap, tolerance) - 1;

            result.Add(ImageTrimmer.TrimImage(GetCropped(image, y, currEndY)));

            y = FindNextFilledY(image, currEndY + 1, tolerance);
        }

        return result.Where(f => !ImageEmptyChecker.IsEmpty(f))
            .ToImmutableList();
    }

    private static int FindNextFilledY(Image<Rgb24> image, int startY, int tolerance)
    {
        for (var y = startY; y < image.Height; y++)
            if (!IsRowEmpty(image, y, tolerance))
                return y;

        return image.Height;
    }

    private static int FindNextEmptyY(Image<Rgb24> image, int startY, int tolerance)
    {
        for (var y = startY; y < image.Height; y++)
            if (IsRowEmpty(image, y, tolerance))
                return y;

        return image.Height;
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

    private static byte[] GetCropped(Image<Rgb24> image, int startY, int endY)
    {
        using var cropped = image.Clone(ctx =>
        {
            ctx.Crop(new Rectangle(0, startY, image.Width, endY - startY))
                .Resize(new Size(image.Width, endY - startY));
        });

        using var outputMs = new MemoryStream();
        cropped.Save(outputMs, new JpegEncoder());

        var res = outputMs.ToArray();

        outputMs.Close();

        return res;
    }
}
