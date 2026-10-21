namespace WebApp.Domain.Common;

public class Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }

    protected Result(bool success, string? error = null)
    {
        IsSuccess = success;
        Error     = error;
    }

    public static Result Ok()           => new(true);
    public static Result Fail(string e) => new(false, e);

    public static Result<T> Ok<T>(T v)        => new(true, v);
    public static Result<T> Fail<T>(string e) => new(false, default, e);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    /// <summary>
    /// The success value. Throws when the result is a failure — check
    /// <see cref="Result.IsSuccess"/> first rather than suppressing with `!`.
    /// </summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException(
            $"Cannot read {nameof(Value)} of a failed result. Error: {Error}");

    internal Result(bool success, T? value = default, string? error = null)
        : base(success, error) => _value = value;
}
