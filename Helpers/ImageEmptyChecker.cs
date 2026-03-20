using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace WebReader.Helpers;

public static class ImageEmptyChecker
{
    public static bool IsEmpty(byte[] imageBytes, int tolerance = 50, int offset = 10)
    {
        using var image = Image.Load<Rgb24>(imageBytes);

        for (var x = offset; x < image.Width - offset; x++)
        for (var y = offset; y < image.Height - offset; y++)
        {
            var pixel = image[x, y];

            if (!StaticFunctions.IsColorMatch(Color.FromRgb(pixel.R, pixel.G, pixel.B),
                    image[image.Width / 2, image.Height / 2], tolerance))
                return false;
        }

        return true;
    }
}
