using LMERP.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace LMERP.Server.ExceptionHandling;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title, message) = MapException(exception);
        var traceId = System.Diagnostics.Activity.Current?.Id ?? httpContext.TraceIdentifier;

        if (statusCode >= StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception for {Method} {Path}; trace {TraceId}", httpContext.Request.Method, httpContext.Request.Path, traceId);
        else
            logger.LogWarning("Handled {ExceptionType} as {StatusCode} for {Method} {Path}; trace {TraceId}", exception.GetType().Name, statusCode, httpContext.Request.Method, httpContext.Request.Path, traceId);

        await ApiProblemDetails.WriteAsync(httpContext, statusCode, title, message, cancellationToken);
        return true;
    }

    private static (int StatusCode, string Title, string Message) MapException(Exception exception) => exception switch
    {
        NotFoundException => (StatusCodes.Status404NotFound, "Resource not found", exception.Message),
        DomainValidationException or ValidationException => (StatusCodes.Status400BadRequest, "Invalid request", exception.Message),
        ConflictException => (StatusCodes.Status409Conflict, "Conflict", exception.Message),
        DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflict", "The record was modified by someone else. Reload and try again."),
        DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } =>
            (StatusCodes.Status409Conflict, "Conflict", "A record with the same unique value already exists."),
        _ => (StatusCodes.Status500InternalServerError, "Internal server error", "An unexpected error occurred. Please try again later.")
    };
}
