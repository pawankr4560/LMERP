using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace LMERP.Server.ExceptionHandling;

/// <summary>Creates RFC 7807 errors while retaining the message/errors fields expected by the Angular client.</summary>
public static class ApiProblemDetails
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static ProblemDetails Create(HttpContext context, int statusCode, string title, string message, IEnumerable<string>? errors = null)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = message,
            Instance = context.Request.Path
        };
        problem.Extensions["success"] = false;
        problem.Extensions["message"] = message;
        problem.Extensions["data"] = null;
        problem.Extensions["errors"] = errors?.ToArray() ?? Array.Empty<string>();
        problem.Extensions["traceId"] = traceId;
        return problem;
    }

    public static async Task WriteAsync(HttpContext context, int statusCode, string title, string message, CancellationToken cancellationToken = default)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
        context.Response.Headers["X-Correlation-ID"] = traceId;
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        var problem = Create(context, statusCode, title, message);
        await JsonSerializer.SerializeAsync(context.Response.Body, problem, JsonOptions, cancellationToken);
    }
}
