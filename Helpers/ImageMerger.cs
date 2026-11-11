using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace WebReader.Helpers;

public static class ImageMerger
{
    public static Image<Rgb24> Merge(List<byte[]> imagesBytes)
    {
        if (imagesBytes.Count == 0)
            return new Image<Rgb24>(1, 1, Color.White);

        var maxWidth = 0;
        var totalHeight = 0;

        // 1. Читаем только заголовки файлов без декодирования пикселей в память
        foreach (var info in imagesBytes.Select(bytes => Image.Identify(bytes)))
        {
            if (info.Width > maxWidth) maxWidth = info.Width;
            totalHeight += info.Height;
        }

        var result = new Image<Rgb24>(maxWidth, totalHeight, Color.White);
        var currentY = 0;

        // 2. Загружаем, отрисовываем и сразу освобождаем память (using) по одной картинке
        for (var i = 0; i < imagesBytes.Count; i++)
        {
            Console.WriteLine($"Merge {i + 1}/{imagesBytes.Count}");

            using var img = Image.Load<Rgb24>(imagesBytes[i]);

            // Быстрое копирование блока пикселей средствами ImageSharp
            result.Mutate(ctx => ctx.DrawImage(img, new Point(0, currentY), 1f));

            currentY += img.Height;
        }

        return result;
    }
}
