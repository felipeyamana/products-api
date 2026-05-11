namespace Application.Common;

public class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;

    public T? Value { get; }
    public string? Error { get; }

    private Result(bool isSuccess, T? value, string? error)
    {
        if (isSuccess && error is not null)
            throw new ArgumentException("Cannot have error on success.");

        if (!isSuccess && string.IsNullOrWhiteSpace(error))
            throw new ArgumentException("Failure must contain error.");

        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Ok(T value)
        => new(true, value, null);

    public static Result<T> Fail(string error)
        => new(false, default, error);
}
