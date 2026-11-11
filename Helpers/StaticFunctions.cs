using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using NAudio.Wave;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using WebReader.Models;

namespace WebReader.Helpers;

public static class StaticFunctions
{
    private static readonly JpegEncoder DefaultJpegEncoder = new() { Quality = 90 };
    private static readonly PngEncoder DefaultPngEncoder = new();

    public static List<RoleType> GetUserRoles(this ClaimsPrincipal user)
    {
        return
        [
            .. user.Claims
                .Where(f => f.Type == ClaimTypes.Role)
                .Select(f => f.Value)
                .Select(Enum.Parse<RoleType>)
        ];
    }

    public static Guid GetUserGuid(this ClaimsPrincipal user)
    {
        return Guid.Parse(user.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value);
    }

    public static void AddOrAppend<T1, T2>(this Dictionary<T1, List<T2>> dict, T1 key, T2 value)
        where T1 : notnull
    {
        if (!dict.TryGetValue(key, out var list))
        {
            list = [];
            dict[key] = list;
        }

        list.Add(value);
    }

    public static void AddOrAppend<T1, T2>(this Dictionary<T1, List<T2>> dict, T1 key, List<T2> value)
        where T1 : notnull
    {
        if (!dict.TryGetValue(key, out var list))
        {
            list = [];
            dict[key] = list;
        }

        list.AddRange(value);
    }

    public static bool IsSuccessStatusCode(this HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return code is >= 200 and < 300;
    }

    public static byte[] ConvertByteArrayToJpeg(byte[] sourceImageBytes)
    {
        using var image = Image.Load(sourceImageBytes);
        using var outputStream = new MemoryStream();
        image.Save(outputStream, new JpegEncoder());

        var res = outputStream.ToArray();

        outputStream.Close();

        return res;
    }

    public static bool IsColorMatch(Rgb24 c1, Rgb24 c2, int tolerance = 10)
    {
        return Math.Abs(c1.R - c2.R) <= tolerance &&
               Math.Abs(c1.G - c2.G) <= tolerance &&
               Math.Abs(c1.B - c2.B) <= tolerance;
    }

    public static bool IsColorMatch(Rgb24 c1, Color[] c2, int tolerance = 10)
    {
        return c2.Any(f =>
        {
            var pixel = f.ToPixel<Rgb24>();

            return Math.Abs(c1.R - pixel.R) <= tolerance &&
                   Math.Abs(c1.G - pixel.G) <= tolerance &&
                   Math.Abs(c1.B - pixel.B) <= tolerance;
        });
    }

    public static bool IsNullOrEmptyOrWhitespace(this string? value)
    {
        return string.IsNullOrEmpty(value) || string.IsNullOrWhiteSpace(value);
    }

    public static string? LimitToNullable(this string? src, int limit = 2000)
    {
        return string.IsNullOrEmpty(src) || src.Length <= limit ? src : src[..(limit - 3)] + "...";
    }

    public static string LimitTo(this string src, int limit = 2000)
    {
        return string.IsNullOrEmpty(src) || src.Length <= limit ? src : src[..(limit - 3)] + "...";
    }

    public static string HashPassword(string password)
    {
        var passwordHasher = new PasswordHasher<string>();
        return passwordHasher.HashPassword(null, password);
    }

    public static bool VerifyPassword(string password, string hash)
    {
        var passwordHasher = new PasswordHasher<string>();
        var result = passwordHasher.VerifyHashedPassword(null, hash, password);
        return result != PasswordVerificationResult.Failed;
    }

    public static bool TryParseNullable<T>(string? srcStr, out T? res) where T : struct, Enum
    {
        if (Enum.TryParse<T>(srcStr, out var temp))
        {
            res = temp;
            return true;
        }

        res = null;
        return false;
    }

    public static T? PickRandom<T>(this IList<T> source, T? defaultValue = default)
    {
        if (source.Count == 0)
            return defaultValue;

        var index = Random.Shared.Next(source.Count);
        return source[index];
    }

    public static void MergeWavStreams(List<MemoryStream> sourceStreams, MemoryStream targetStream)
    {
        if (sourceStreams == null || sourceStreams.Count == 0)
            throw new ArgumentException("Empty stream source list.", nameof(sourceStreams));

        if (targetStream == null)
            throw new ArgumentNullException(nameof(targetStream), "Target stream can't be null.");

        WaveFileWriter? writer = null;

        foreach (var stream in sourceStreams)
        {
            stream.Position = 0;

            using var reader = new WaveFileReader(stream);
            if (writer == null)
                writer = new WaveFileWriter(targetStream, reader.WaveFormat);
            else if (!reader.WaveFormat.Equals(writer.WaveFormat))
                throw new InvalidOperationException("Different file formats.");

            var buffer = new byte[reader.WaveFormat.AverageBytesPerSecond];
            int read;

            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0) writer.Write(buffer, 0, read);
        }

        writer?.Flush();

        targetStream.Position = 0;
    }

    public static byte[] GetCropped(Image<Rgb24> image, int left, int top, int trimWidth, int trimHeight)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (trimWidth <= 0 || trimHeight <= 0)
            return [];

        // Убрали лишний .Resize() — оставляем только быстрый .Crop()
        using var cropped = image.Clone(ctx => ctx.Crop(new Rectangle(left, top, trimWidth, trimHeight)));
        using var outputMs = new MemoryStream();

        try
        {
            cropped.Save(outputMs, DefaultJpegEncoder);
        }
        catch (Exception)
        {
            // Очищаем поток от недописанных байтов JPEG перед сохранением в PNG
            outputMs.SetLength(0);
            cropped.Save(outputMs, DefaultPngEncoder);
        }

        return outputMs.ToArray();
    }
}
