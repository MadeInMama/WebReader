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
}
