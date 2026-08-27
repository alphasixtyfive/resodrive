namespace ResoDrive.Core.Results;

public class OperationResult
{
    internal OperationResult(bool succeeded, OperationError? error)
    {
        Succeeded = succeeded;
        Error = error;
    }

    public bool Succeeded { get; }
    public OperationError? Error { get; }

}

public sealed class OperationResult<T> : OperationResult
{
    internal OperationResult(bool succeeded, T? value, OperationError? error)
        : base(succeeded, error) => Value = value;

    public T? Value { get; }
}

public sealed record OperationError(string Code, string Message, bool IsTransient = false);

public static class Result
{
    public static OperationResult Success() => new(true, null);

    public static OperationResult Failure(string code, string message, bool isTransient = false) =>
        new(false, new OperationError(code, message, isTransient));

    public static OperationResult<T> Success<T>(T value) => new(true, value, null);

    public static OperationResult<T> Failure<T>(string code, string message, bool isTransient = false) =>
        new(false, default, new OperationError(code, message, isTransient));
}
