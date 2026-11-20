using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace WebReader.Helpers;

public static class ImageMerger
{
    public static List<Image<Rgb24>> MergeByWidth<T>(
        List<byte[]> imagesBytes,
        ILogger<T> logger,
        double widthTolerancePercent = 1.0)
    {
        if (imagesBytes.Count == 0)
            return [];

        var result = new List<Image<Rgb24>>();

        // Вспомогательный список для текущей группы склеиваемых картинок
        var currentGroup = new List<(byte[] Bytes, ImageInfo Info)>();

        foreach (var bytes in imagesBytes)
        {
            var info = Image.Identify(bytes);

            if (currentGroup.Count == 0)
            {
                currentGroup.Add((bytes, info));
                continue;
            }

            // Сравниваем ширину текущей картинки с базовой (первой) картинкой в текущей группе
            var baseWidth = currentGroup[0].Info.Width;
            var diffPercent = Math.Abs(info.Width - baseWidth) * 100.0 / baseWidth;

            if (diffPercent <= widthTolerancePercent)
            {
                // Ширина в пределах допуска — добавляем в текущую группу
            }
            else
            {
                // Ширина отличается сильнее допуска — склеиваем накопленную группу и начинаем новую
                result.Add(MergeGroup(currentGroup, logger, result.Count + 1));
                currentGroup.Clear();
            }

            currentGroup.Add((bytes, info));
        }

        // Склеиваем последнюю оставшуюся группу
        if (currentGroup.Count > 0) result.Add(MergeGroup(currentGroup, logger, result.Count + 1));

        return result;
    }

    private static Image<Rgb24> MergeGroup<T>(
        List<(byte[] Bytes, ImageInfo Info)> group,
        ILogger<T> logger,
        int groupNumber)
    {
        var maxWidth = group.Max(item => item.Info.Width);
        var totalHeight = group.Sum(item => item.Info.Height);

        var mergedImage = new Image<Rgb24>(maxWidth, totalHeight, Color.White);
        var currentY = 0;

        for (var i = 0; i < group.Count; i++)
        {
            logger.LogInformation("Group {groupNumber}: Merging {current}/{total}", groupNumber, i + 1, group.Count);

            using var img = Image.Load<Rgb24>(group[i].Bytes);

            // Отрисовываем текущую картинку (при небольшом расхождении ширины можно центрировать или прижать влево)
            mergedImage.Mutate(ctx => ctx.DrawImage(img, new Point(0, currentY), 1f));

            currentY += img.Height;
        }

        return mergedImage;
    }

    public static Image<Rgb24> Merge<T>(List<byte[]> imagesBytes, ILogger<T> logger)
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
            logger.LogInformation("Merge {i}/{imagesBytesCount}", i + 1, imagesBytes.Count);

            using var img = Image.Load<Rgb24>(imagesBytes[i]);

            // Быстрое копирование блока пикселей средствами ImageSharp
            result.Mutate(ctx => ctx.DrawImage(img, new Point(0, currentY), 1f));

            currentY += img.Height;
        }

        return result;
    }
}
