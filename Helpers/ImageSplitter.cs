using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace WebReader.Helpers;

public static class ImageSplitter
{
    public static List<byte[]> Split(Image<Rgb24> image, Color[]? emptyColors = null)
    {
        emptyColors ??= [Color.White];

        const int minHeight = 300;

        var result = new List<byte[]>();

        for (var y = 0; y < image.Height; y++)
        {
            var currEndY = FindNextEmptyY(image, y + minHeight, emptyColors);
            currEndY = Math.Max(0, currEndY);

            if (y == currEndY || ImageEmptyChecker.IsEmpty(image, 0, y, image.Width, currEndY)) continue;

            // Console.WriteLine($"Split {y}/{image.Height}");

            result.Add(ImageTrimmer.Trim(image, 0, y, image.Width - 1, currEndY, emptyColors, 5));

            y = FindNextFilledY(image, currEndY + 1, emptyColors);
        }

        image.Dispose();

        return result;
    }

    private static int FindNextFilledY(Image<Rgb24> image, int startY, Color[] emptyColors)
    {
        for (var y = startY; y < image.Height; y++)
            if (!ImageEmptyChecker.IsRowEmpty(image, y, emptyColors))
                return y;

        return image.Height - 1;
    }

    private static int FindNextEmptyY(Image<Rgb24> image, int startY, Color[] emptyColors)
    {
        for (var y = startY; y < image.Height; y++)
            if (ImageEmptyChecker.IsRowEmpty(image, y, emptyColors))
                return y;

        return image.Height - 1;
    }
}
