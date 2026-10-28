namespace LinkRadar.Api.ResultPattern;

public enum ErrorType
{
    Validation,
    NotFound,
    Unauthorized,
    Conflict,
    ExternalService,
    Unexpected
}

// IsTransient = خطای گذرا؛ step می‌تواند دوباره تلاش کند. خطای دائمی نباید retry شود
public record Error(string Code, string Message, ErrorType Type, bool IsTransient = false)
{
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error ExternalService(string code, string message, bool isTransient) =>
        new(code, message, ErrorType.ExternalService, isTransient);

    public static Error Unexpected(string code, string message) => new(code, message, ErrorType.Unexpected);
}
