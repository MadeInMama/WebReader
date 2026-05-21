using System.Text;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WebReader.Helpers;

public class LogRequestAttribute(ILogger<LogRequestAttribute> logger) : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        context.HttpContext.Request.EnableBuffering();
        var requestBody = await ReadStreamAsync(context.HttpContext.Request.Body);
        context.HttpContext.Request.Body.Position = 0;

        // logger.LogInformation("IP: {ip}\nUserId: {userId}\nRequest: {method} {path} {query}\nData: {data}",
        //     context.HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "Unknown",
        //     context.HttpContext.User.Identity is { IsAuthenticated: true }
        //         ? context.HttpContext.User.GetUserGuid()
        //         : null,
        //     context.HttpContext.Request.Method, context.HttpContext.Request.Path,
        //     string.Join(", ", context.HttpContext.Request.Query), requestBody);

        logger.LogInformation("IP: {ip}\nUserId: {userId}",
            context.HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "Unknown",
            context.HttpContext.User.Identity is { IsAuthenticated: true }
                ? context.HttpContext.User.GetUserGuid()
                : null);

        await base.OnActionExecutionAsync(context, next);
    }

    public override async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        // var options = new JsonSerializerOptions
        // {
        //     Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        // };

        // var responseBody = context.Result switch
        // {
        //     ViewResult vResult => JsonSerializer.Serialize(vResult.Model, options),
        //     ObjectResult oResult => JsonSerializer.Serialize(oResult.Value, options),
        //     JsonResult jResult => jResult.Value?.ToString(),
        //     _ => ""
        // };

        // logger.LogInformation(
        //     "IP: {iP}\nUserId: {userId}\nRequest: {method} {path} {query}\nResponse Status: {status}\nData: {data}\n",
        //     context.HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "Unknown",
        //     context.HttpContext.User.Identity is { IsAuthenticated: true }
        //         ? context.HttpContext.User.GetUserGuid()
        //         : null,
        //     context.HttpContext.Request.Method, context.HttpContext.Request.Path,
        //     string.Join(", ", context.HttpContext.Request.Query), context.HttpContext.Response.StatusCode,
        //     responseBody?.LimitTo());

        logger.LogInformation("IP: {ip}\nUserId: {userId}",
            context.HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "Unknown",
            context.HttpContext.User.Identity is { IsAuthenticated: true }
                ? context.HttpContext.User.GetUserGuid()
                : null);

        await base.OnResultExecutionAsync(context, next);
    }

    private static async Task<string> ReadStreamAsync(Stream stream)
    {
        stream.Position = 0;
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        var res = await reader.ReadToEndAsync();
        return res;
    }
}
