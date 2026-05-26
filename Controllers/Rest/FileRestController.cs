using FluentResults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebReader.Helpers;
using WebReader.Models.Dtos;
using WebReader.Models.Dtos.Rest;
using WebReader.Repositories;
using WebReader.Services;

namespace WebReader.Controllers.Rest;

[Authorize]
[ApiController]
[Produces("application/json")]
[Route("api/[controller]/[action]")]
public class FileRestController(
    FileControllerService fileControllerService,
    AuthRestService authRestService,
    CustomUserRepository userRepository) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllBuckets(CancellationToken cancellationToken = default)
    {
        var jwtData = await GetNewJwt(cancellationToken);

        if (jwtData.IsFailed) return Unauthorized(jwtData.ToString());

        var res = await fileControllerService.GetAllBuckets(User.GetUserGuid(), User.GetUserRoles(), cancellationToken);

        return Ok(new BaseResponseDto<AllBucketsViewModel>
        {
            Data = res.Value,
            AccessToken = jwtData.Value.AccessToken,
            ExpiresAt = jwtData.Value.ExpiresAt,
            UserId = jwtData.Value.UserId,
            Username = jwtData.Value.Username
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAllFilesInBucket(Guid bucketId, string orderBy = "FileName",
        CancellationToken cancellationToken = default)
    {
        var jwtData = await GetNewJwt(cancellationToken);

        if (jwtData.IsFailed) return Unauthorized(jwtData.ToString());

        var res = await fileControllerService.GetAllFilesInBucket(User.GetUserGuid(), User.GetUserRoles(), bucketId,
            orderBy, cancellationToken);

        if (res.IsFailed) return BadRequest(res.ToString());

        return Ok(new BaseResponseDto<AllFilesInBucketViewModel>
        {
            Data = res.Value,
            AccessToken = jwtData.Value.AccessToken,
            ExpiresAt = jwtData.Value.ExpiresAt,
            UserId = jwtData.Value.UserId,
            Username = jwtData.Value.Username
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetAllPartsInFile(Guid bucketId, Guid fileId, string orderBy = "FileName",
        CancellationToken cancellationToken = default)
    {
        var jwtData = await GetNewJwt(cancellationToken);

        if (jwtData.IsFailed) return Unauthorized(jwtData.ToString());

        var res = await fileControllerService.GetAllPartsInFile(User.GetUserGuid(), User.GetUserRoles(), bucketId,
            fileId, orderBy, cancellationToken);

        if (res.IsFailed) return BadRequest(res.ToString());

        return Ok(new BaseResponseDto<AllFilesInBucketViewModel>
        {
            Data = res.Value,
            AccessToken = jwtData.Value.AccessToken,
            ExpiresAt = jwtData.Value.ExpiresAt,
            UserId = jwtData.Value.UserId,
            Username = jwtData.Value.Username
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetReading(CancellationToken cancellationToken = default)
    {
        var jwtData = await GetNewJwt(cancellationToken);

        if (jwtData.IsFailed) return Unauthorized(jwtData.ToString());

        var res = await fileControllerService.GetReading(User.GetUserGuid(), User.GetUserRoles(), cancellationToken);

        return Ok(new BaseResponseDto<AllFilesReadingViewModel>
        {
            Data = res.Value,
            AccessToken = jwtData.Value.AccessToken,
            ExpiresAt = jwtData.Value.ExpiresAt,
            UserId = jwtData.Value.UserId,
            Username = jwtData.Value.Username
        });
    }

    public async Task<IActionResult> GetFile(Guid bucketId, Guid fileId, CancellationToken cancellationToken = default)
    {
        var jwtData = await GetNewJwt(cancellationToken);

        if (jwtData.IsFailed) return Unauthorized(jwtData.ToString());

        var res = await fileControllerService.GetFile(User.GetUserGuid(), User.GetUserRoles(), bucketId, fileId,
            cancellationToken);

        if (res.IsFailed) return BadRequest(res.ToString());

        return Ok(new BaseResponseDto<FileViewModel>
        {
            Data = res.Value,
            AccessToken = jwtData.Value.AccessToken,
            ExpiresAt = jwtData.Value.ExpiresAt,
            UserId = jwtData.Value.UserId,
            Username = jwtData.Value.Username
        });
    }

    private async Task<Result<BaseResponseDto<int?>>> GetNewJwt(CancellationToken cancellationToken = default)
    {
        var user = await userRepository.FirstOrDefaultAsync(f => f.Id.Equals(User.GetUserGuid()) && f.IsActive, null,
            cancellationToken, true);

        if (user == null) return Result.Fail("User not found");

        return authRestService.BuildResponseUser(user);
    }
}
