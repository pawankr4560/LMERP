using System.Text.Json;
using LMERP.Server.Responses;

namespace LMERP.Server.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception exception) when (context.Request.Path.StartsWithSegments("/api"))
        {
            logger.LogError(exception, "Unhandled API exception for {Path}", context.Request.Path);
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await JsonSerializer.SerializeAsync(context.Response.Body, BaseResponse<object>.Failed("An unexpected error occurred."), JsonOptions, context.RequestAborted);
        }
    }
}
