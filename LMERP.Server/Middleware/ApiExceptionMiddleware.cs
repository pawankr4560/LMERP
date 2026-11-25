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
            if (exception is ArgumentException or KeyNotFoundException)
            {
                context.Response.Clear();
                context.Response.StatusCode = exception is KeyNotFoundException ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest;
                context.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(context.Response.Body, BaseResponse<object>.Failed(exception.Message), JsonOptions, context.RequestAborted);
                return;
            }
            logger.LogError(exception, "Unhandled API exception for {Path}", context.Request.Path);
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await JsonSerializer.SerializeAsync(context.Response.Body, BaseResponse<object>.Failed("An unexpected error occurred."), JsonOptions, context.RequestAborted);
        }
    }
}
