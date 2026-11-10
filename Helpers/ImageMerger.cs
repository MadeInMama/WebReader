using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace WebReader.Helpers;

public static class ImageMerger
{
    public static byte[] Merge(List<byte[]> imagesBytes)
    {
        if (imagesBytes.Count == 0)
            return [];

        var loadedImages = imagesBytes.Select(imageBytes => Image.Load<Rgb24>(imageBytes)).ToList();

        var maxWidth = loadedImages.Max(img => img.Width);
        var totalHeight = loadedImages.Sum(img => img.Height);

        using var result = new Image<Rgb24>(maxWidth, totalHeight, Color.White);

        var currentY = 0;

        foreach (var image in loadedImages)
        {
            for (var y = 0; y < image.Height; y++)
            for (var x = 0; x < image.Width; x++)
                result[x, currentY + y] = image[x, y];

            currentY += image.Height;
        }

        using var outputMs = new MemoryStream();
        result.Save(outputMs, new PngEncoder());

        var res = outputMs.ToArray();

        outputMs.Close();

        return res;
    }
}
