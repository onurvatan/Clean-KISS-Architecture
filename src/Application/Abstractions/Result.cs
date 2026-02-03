namespace Application.Abstractions;

public class Result<T>
{
    public T? Value { get; }
    public string? Error { get; }
    public int StatusCode { get; }
    public bool IsSuccess => Error is null;
    public bool IsFailure => !IsSuccess;

    private Result(T? value, string? error, int statusCode)
    {
        Value = value;
        Error = error;
        StatusCode = statusCode;
    }

    // Success
    public static Result<T> Success(T value)
        => new(value, null, 200);

    public static Result<T> Created(T value)
        => new(value, null, 201);

    // Client errors
    public static Result<T> BadRequest(string error)
        => new(default, error, 400);

    public static Result<T> Unauthorized(string error)
        => new(default, error, 401);

    public static Result<T> Forbidden(string error)
        => new(default, error, 403);

    public static Result<T> NotFound(string error)
        => new(default, error, 404);

    public static Result<T> Conflict(string error)
        => new(default, error, 409);

    // Pattern matching
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<string, TResult> onFailure)
        => IsSuccess ? onSuccess(Value!) : onFailure(Error!);
}

public class Result
{
    public string? Error { get; }
    public int StatusCode { get; }
    public bool IsSuccess => Error is null;
    public bool IsFailure => !IsSuccess;

    private Result(string? error, int statusCode)
    {
        Error = error;
        StatusCode = statusCode;
    }

    // Success
    public static Result Success() => new(null, 200);
    public static Result NoContent() => new(null, 204);

    // Client errors
    public static Result BadRequest(string error) => new(error, 400);
    public static Result Unauthorized(string error) => new(error, 401);
    public static Result Forbidden(string error) => new(error, 403);
    public static Result NotFound(string error) => new(error, 404);
    public static Result Conflict(string error) => new(error, 409);
}
