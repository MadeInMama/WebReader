using System.Net;
using System.Security.Claims;
using PuppeteerSharp;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using WebReader.Models;

namespace WebReader.Helpers;

public static class StaticFunctions
{
    public static List<RoleType> GetUserRoles(this ClaimsPrincipal user)
    {
        return user.Claims
            .Where(x => x.Type == ClaimTypes.Role)
            .Select(x => x.Value)
            .Select(Enum.Parse<RoleType>)
            .ToList();
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

    public static bool IsSuccessStatusCode(this HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return code is >= 200 and < 300;
    }

    public static void CustomUninstall(this BrowserFetcher bf, SupportedBrowser sb, Platform p, string buildId)
    {
        foreach (var el in Enum.GetValues<SupportedBrowser>())
            BrowserProcessKiller.PrepareCleanBrowserEnvironment(el);

        var dir = new DirectoryInfo(GetInstallationDir());
        if (dir.Exists) dir.Delete(true);

        return;

        string GetInstallationDir()
        {
            return Path.Combine(GetBrowserRoot(), $"{p}-{buildId}");
        }

        string GetBrowserRoot()
        {
            return Path.Combine(bf.CacheDir, sb.ToString());
        }
    }

    public static byte[] ConvertByteArrayToGif(byte[] sourceImageBytes)
    {
        using var image = Image.Load(sourceImageBytes);
        using var outputStream = new MemoryStream();
        image.Save(outputStream, new JpegEncoder());
        return outputStream.ToArray();
    }
}
