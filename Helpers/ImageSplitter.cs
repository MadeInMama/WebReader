using System.Collections.Immutable;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace WebReader.Helpers;

public static class ImageSplitter
{
    public static ImmutableList<byte[]> Split(byte[] imageBytes, Color[]? emptyColors = null)
    {
        emptyColors ??= [Color.Black, Color.White];

        using var image = Image.Load<Rgb24>(imageBytes);

        var result = new List<byte[]>();

        for (var y = 0; y < image.Height; y++)
        {
            var currEndY = FindNextEmptyY(image, y + 1, emptyColors) - 1;
            currEndY = Math.Max(0, currEndY);

            if (y == currEndY) continue;

            result.Add(ImageTrimmer.Trim(GetCropped(image, y, currEndY)));

            y = FindNextFilledY(image, currEndY + 1, emptyColors) - 1;
        }

        return result.Where(f => !ImageEmptyChecker.IsEmpty(f))
            .ToImmutableList();
    }

    private static int FindNextFilledY(Image<Rgb24> image, int startY, Color[] emptyColors)
    {
        for (var y = startY; y < image.Height; y++)
            if (!IsRowEmpty(image, y, emptyColors))
                return y;

        return image.Height;
    }

    private static int FindNextEmptyY(Image<Rgb24> image, int startY, Color[] emptyColors)
    {
        for (var y = startY; y < image.Height; y++)
            if (IsRowEmpty(image, y, emptyColors))
                return y;

        return image.Height;
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

    private static byte[] GetCropped(Image<Rgb24> image, int startY, int endY)
    {
        using var cropped = image.Clone(ctx =>
        {
            ctx.Crop(new Rectangle(0, startY, image.Width, endY - startY))
                .Resize(new Size(image.Width, endY - startY));
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
}
