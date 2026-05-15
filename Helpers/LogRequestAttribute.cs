using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WebReader.Helpers;

public class LogRequestAttribute(ILogger<LogRequestAttribute> logger) : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        context.HttpContext.Request.EnableBuffering();
        var requestBody = await ReadStreamAsync(context.HttpContext.Request.Body);
        context.HttpContext.Request.Body.Position = 0;

        logger.LogInformation("IP: {ip}\n      UserId: {userId}\n      Request: {method} {path}\n      Data: {data}",
            ipAddress, context.HttpContext.User.GetUserGuid(), context.HttpContext.Request.Method,
            context.HttpContext.Request.Path, requestBody);

        await base.OnActionExecutionAsync(context, next);
    }

    public override async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

        var options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        var responseBody = context.Result switch
        {
            ViewResult vResult => JsonSerializer.Serialize(vResult.Model, options),
            ObjectResult oResult => JsonSerializer.Serialize(oResult.Value, options),
            JsonResult jResult => jResult.Value?.ToString(),
            _ => ""
        };

        logger.LogInformation(
            "IP: {iP}\n      UserId: {userId}\n      Request: {method} {path}\n      Response Status: {status}\n      Data: {data}\n",
            ipAddress, context.HttpContext.User.GetUserGuid(), context.HttpContext.Response.StatusCode,
            context.HttpContext.Request.Method, context.HttpContext.Request.Path, responseBody?.LimitTo());

        await base.OnResultExecutionAsync(context, next);
    }

    private static async Task<string> ReadStreamAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }
}
