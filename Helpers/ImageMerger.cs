using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace WebReader.Helpers;

public static class ImageMerger
{
    public static Image<Rgb24> Merge(List<byte[]> imagesBytes)
    {
        if (imagesBytes.Count == 0)
            return new Image<Rgb24>(1, 1, Color.White);

        var loadedImages = imagesBytes.Select(imageBytes => Image.Load<Rgb24>(imageBytes)).ToList();

        var maxWidth = loadedImages.Max(img => img.Width);
        var totalHeight = loadedImages.Sum(img => img.Height);

        var result = new Image<Rgb24>(maxWidth, totalHeight, Color.White);

        var currentY = 0;

        foreach (var image in loadedImages.Index())
        {
            // Console.WriteLine($"Merge {image.Index + 1}/{loadedImages.Count}");
            for (var y = 0; y < image.Item.Height; y++)
            for (var x = 0; x < image.Item.Width; x++)
                result[x, currentY + y] = image.Item[x, y];

            currentY += image.Item.Height;
        }

        return result;
    }
}
