namespace HEMedical.Shared.Common;

public enum ErrorKind
{
    None = 0,
    Failure = 1,
    InvalidInput = 2,
    NotFound = 3,
}

public record Result<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }
    public ErrorKind Kind { get; init; }

    public static Result<T> Ok(T value) => new() { IsSuccess = true, Value = value };
    public static Result<T> Fail(string error, ErrorKind kind = ErrorKind.Failure) => new() { IsSuccess = false, Error = error, Kind = kind };

    public static implicit operator Result<T>(T value) => Ok(value);
}
