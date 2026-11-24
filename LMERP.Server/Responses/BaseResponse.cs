namespace LMERP.Server.Responses;

public abstract record BaseResponse(bool Success, string Message, IReadOnlyList<string> Errors);

public sealed record BaseResponse<T>(bool Success, string Message, T? Data, IReadOnlyList<string> Errors) : BaseResponse(Success, Message, Errors)
{
    public static BaseResponse<T> Succeeded(T data, string message = "Request completed.") => new(true, message, data, Array.Empty<string>());
    public static BaseResponse<T> Failed(string message, IEnumerable<string>? errors = null) => new(false, message, default, errors?.ToArray() ?? Array.Empty<string>());
}
