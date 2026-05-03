namespace AppointmentSystem.Bll.Common;

/// <summary>
/// Discriminated union representing either a successful value or a failure message.
/// Replaces exception-based error flow for expected business-rule failures.
/// </summary>
public sealed class Result<T>
{
    public bool    IsSuccess { get; }
    public bool    IsFailure => !IsSuccess;
    public T?      Value     { get; }
    public string? Error     { get; }

    private Result(T value)      { IsSuccess = true;  Value = value; }
    private Result(string error) { IsSuccess = false; Error = error; }

    public static Result<T> Ok(T value)      => new(value);
    public static Result<T> Fail(string err) => new(err);

    /// <summary>Railway-oriented match — execute one branch, return a unified type.</summary>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<string, TOut> onFailure)
        => IsSuccess ? onSuccess(Value!) : onFailure(Error!);

    /// <summary>Allow implicit bool check: <c>if (result)</c></summary>
    public static implicit operator bool(Result<T> result) => result.IsSuccess;

    public override string ToString()
        => IsSuccess ? $"Ok({Value})" : $"Fail({Error})";
}

/// <summary>Non-generic Result for void operations.</summary>
public sealed class Result
{
    public bool    IsSuccess { get; }
    public bool    IsFailure => !IsSuccess;
    public string? Error     { get; }

    private Result(bool success, string? error) { IsSuccess = success; Error = error; }

    public static Result Ok()              => new(true,  null);
    public static Result Fail(string err)  => new(false, err);

    public static implicit operator bool(Result result) => result.IsSuccess;
}
