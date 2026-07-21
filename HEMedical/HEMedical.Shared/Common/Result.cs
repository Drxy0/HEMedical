namespace HEMedical.Shared.Common;

public enum ErrorKind
{
    None = 0,
    Failure = 1,
    InvalidInput = 2,
    NotFound = 3,
    /// <summary>
    /// A required external credential (currently the LOINC terminology account) is
    /// missing or was rejected, so the caller should prompt for it and retry.
    /// </summary>
    LoincCredentialsRequired = 4,
    /// <summary>
    /// A dependency needed to answer the request is temporarily unavailable — e.g. no
    /// approved hospital data sources are registered yet, or none responded. A valid,
    /// transient state the caller can retry, not a server fault (maps to 503, not 500).
    /// </summary>
    ServiceUnavailable = 5,
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
