using System.Text.Json;
using LMERP.Application.Exceptions;
using LMERP.Server.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace LMERP.Server.Middleware;

public sealed class ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception exception) when (context.Request.Path.StartsWithSegments("/api"))
        {
            var (status, message) = exception switch
            {
                DomainValidationException => (StatusCodes.Status400BadRequest, exception.Message),
                NotFoundException => (StatusCodes.Status404NotFound, exception.Message),
                ConflictException => (StatusCodes.Status409Conflict, exception.Message),
                DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "The record was modified by someone else. Reload and try again."),
                DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } => (StatusCodes.Status409Conflict, "A record with the same unique value already exists."),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
            };
            if (status == StatusCodes.Status500InternalServerError) logger.LogError(exception, "Unhandled API exception for {Path}", context.Request.Path);
            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            await JsonSerializer.SerializeAsync(context.Response.Body, BaseResponse<object>.Failed(message), JsonOptions, context.RequestAborted);
        }
    }
}
