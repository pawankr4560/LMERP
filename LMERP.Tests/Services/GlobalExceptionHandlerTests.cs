using System.Text.Json;
using LMERP.Application.Exceptions;
using LMERP.Server.ExceptionHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LMERP.Tests.Services;

public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task NotFoundException_Returns404ProblemDetailsAndLegacyMessage()
    {
        var response = await HandleAsync(new NotFoundException("Loan was not found."));

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.ContentType);
        Assert.Equal("Loan was not found.", response.Body.RootElement.GetProperty("message").GetString());
        Assert.False(response.Body.RootElement.GetProperty("success").GetBoolean());
        Assert.NotEmpty(response.Body.RootElement.GetProperty("traceId").GetString()!);
    }

    [Fact]
    public async Task DomainValidationException_Returns400()
    {
        var response = await HandleAsync(new DomainValidationException("Enter a valid loan amount."));

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal("Enter a valid loan amount.", response.Body.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task ConflictException_Returns409()
    {
        var response = await HandleAsync(new ConflictException("Loan cannot be changed after disbursement."));

        Assert.Equal(StatusCodes.Status409Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UnexpectedException_IsRedactedFromClientAndLoggedWithExceptionDetails()
    {
        var exception = new InvalidOperationException("Sensitive database connection string");
        var logger = new CapturingLogger<GlobalExceptionHandler>();
        var response = await HandleAsync(exception, logger);

        Assert.Equal(StatusCodes.Status500InternalServerError, response.StatusCode);
        Assert.Equal("An unexpected error occurred. Please try again later.", response.Body.RootElement.GetProperty("detail").GetString());
        Assert.DoesNotContain("Sensitive database connection string", response.RawBody, StringComparison.Ordinal);
        Assert.Same(exception, logger.Exception);
        Assert.Contains("Unhandled exception", logger.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(StatusCodes.Status401Unauthorized, "Unauthorized")]
    [InlineData(StatusCodes.Status403Forbidden, "Forbidden")]
    public async Task AuthenticationErrors_UseProblemDetailsAndCompatibilityFields(int statusCode, string title)
    {
        var context = NewContext();
        await ApiProblemDetails.WriteAsync(context, statusCode, title, "Access denied.");

        Assert.Equal(statusCode, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("Access denied.", document.RootElement.GetProperty("message").GetString());
        Assert.True(document.RootElement.TryGetProperty("errors", out _));
    }

    [Fact]
    public void InvalidModelStateProblemDetails_PreservesAngularErrorsArray()
    {
        var context = NewContext();
        var problem = ApiProblemDetails.Create(context, StatusCodes.Status400BadRequest, "Invalid request", "Request validation failed.", ["Amount is required."]);

        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("Request validation failed.", problem.Extensions["message"]);
        Assert.Equal(new[] { "Amount is required." }, Assert.IsType<string[]>(problem.Extensions["errors"]));
    }

    private static async Task<(int StatusCode, string? ContentType, JsonDocument Body, string RawBody)> HandleAsync(
        Exception exception,
        CapturingLogger<GlobalExceptionHandler>? logger = null)
    {
        var context = NewContext();
        var handler = new GlobalExceptionHandler(logger ?? new CapturingLogger<GlobalExceptionHandler>());
        Assert.True(await handler.TryHandleAsync(context, exception, CancellationToken.None));
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();
        var document = JsonDocument.Parse(rawBody);
        return (context.Response.StatusCode, context.Response.ContentType, document, rawBody);
    }

    private static DefaultHttpContext NewContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/loans/99";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public Exception? Exception { get; private set; }
        public string Message { get; private set; } = string.Empty;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Exception = exception;
            Message = formatter(state, exception);
        }
    }
}
