namespace AcxiomCRM.Web.DTOs;

/// <summary>
/// Standard error envelope returned by every API endpoint:
/// { "error": { "code": "...", "message": "...", "details": { ... } } }
/// </summary>
public sealed class ApiErrorDto
{
    public ApiErrorBody Error { get; set; } = new();

    public static ApiErrorDto Create(string code, string message, object? details = null)
        => new() { Error = new ApiErrorBody { Code = code, Message = message, Details = details } };
}

public sealed class ApiErrorBody
{
    public string Code { get; set; } = "error";
    public string Message { get; set; } = string.Empty;
    public object? Details { get; set; }
}
