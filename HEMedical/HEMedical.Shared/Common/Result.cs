namespace HEMedical.Shared.Common;

/// <summary>
/// Classifies a failed <see cref="Result{T}"/> so the web layer can map it
/// to the right HTTP status instead of reporting every failure as a server error.
/// </summary>
public enum ErrorKind
{
    None = 0,
    /// <summary>Something went wrong on our side (default for failures).</summary>
    Failure = 1,
    /// <summary>The caller's input was invalid (e.g. an unrecognized LOINC code).</summary>
    InvalidInput = 2,
    /// <summary>The request was valid but matched no data.</summary>
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

    /// <summary>Lets a plain value stand in for a successful result: <c>return value;</c> instead of <c>return Result&lt;T&gt;.Ok(value);</c>.</summary>
    public static implicit operator Result<T>(T value) => Ok(value);
}
