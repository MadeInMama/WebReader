using System.Net;
using System.Security.Claims;
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

    public static bool TryGetFileType(this string source, out FileType res)
    {
        return Enum.TryParse(Path.GetExtension(source).Remove(0, 1), true, out res);
    }

    public static bool IsSuccessStatusCode(this HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return code is >= 200 and < 300;
    }
}
