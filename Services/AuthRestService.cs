using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentResults;
using Microsoft.IdentityModel.Tokens;
using WebReader.Configuration;
using WebReader.Models.Dtos.Rest;
using WebReader.Models.Entities;
using WebReader.Repositories;

namespace WebReader.Services;

public class AuthRestService
{
    private readonly JwtConfig _jwtConfig = new();
    private readonly CustomUserRepository _userRepository;
    private readonly UserService _userService;

    public AuthRestService(UserService userService, IConfiguration configuration, CustomUserRepository userRepository)
    {
        _userService = userService;
        _userRepository = userRepository;

        configuration.GetRequiredSection(nameof(JwtConfig)).Bind(_jwtConfig);
    }

    public async Task<Result<BaseResponseDto<int?>>> SignIn(LoginRequestDto request)
    {
        var user = await _userService.AuthenticateAsync(request.Username, request.Password);

        if (user == null) return Result.Fail("Invalid credentials");

        return BuildResponseUser(user);
    }

    public BaseResponseDto<int?> BuildResponseUser(CustomUser user)
    {
        var accessToken = GenerateAccessToken(user);

        return new BaseResponseDto<int?>
        {
            Data = null,
            AccessToken = accessToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtConfig.ExpiryMinutes - 1),
            UserId = user.Id,
            Username = user.Username
        };
    }

    private string GenerateAccessToken(CustomUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtConfig.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };
        claims.AddRange(user.Roles.Select(f => new Claim(ClaimTypes.Role, f.ToString())));

        var token = new JwtSecurityToken(
            _jwtConfig.Issuer,
            _jwtConfig.Audience,
            claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtConfig.ExpiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
