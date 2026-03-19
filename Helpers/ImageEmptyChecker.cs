using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace WebReader.Helpers;

public static class ImageEmptyChecker
{
    public static bool IsEmpty(byte[] imageBytes, int tolerance = 50)
    {
        using var image = Image.Load<Rgb24>(imageBytes);

        for (var x = 0; x < image.Width; x++)
        for (var y = 0; y < image.Height; y++)
        {
            var pixel = image[x, y];

            if (!StaticFunctions.IsColorMatch(Color.FromRgb(pixel.R, pixel.G, pixel.B), image[0, 0], tolerance))
                return false;
        }

        return true;
    }
}
