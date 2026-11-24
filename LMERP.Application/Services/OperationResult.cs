namespace LMERP.Application.Services;

public enum OperationStatus { Success, NotFound, Conflict, BadRequest }

public sealed record OperationResult<T>(OperationStatus Status, T? Value, string Message)
{
    public static OperationResult<T> Ok(T value, string message = "Request completed.") => new(OperationStatus.Success, value, message);
    public static OperationResult<T> NotFound(string message) => new(OperationStatus.NotFound, default, message);
    public static OperationResult<T> Conflict(string message) => new(OperationStatus.Conflict, default, message);
    public static OperationResult<T> BadRequest(string message) => new(OperationStatus.BadRequest, default, message);
}
