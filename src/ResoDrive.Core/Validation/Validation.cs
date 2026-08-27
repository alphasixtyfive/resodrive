namespace ResoDrive.Core.Validation;

public enum ValidationSeverity
{
    Error,
    Warning
}

public sealed record ValidationIssue(
    string Code,
    string Message,
    string? Field = null,
    ValidationSeverity Severity = ValidationSeverity.Error);

public sealed class ValidationResult
{
    public static ValidationResult Valid { get; } = new(Array.Empty<ValidationIssue>());

    public ValidationResult(IEnumerable<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        Issues = issues.ToArray();
    }

    public IReadOnlyList<ValidationIssue> Issues { get; }
    public bool IsValid => Issues.All(issue => issue.Severity != ValidationSeverity.Error);
}

public interface IValidator<in T>
{
    ValidationResult Validate(T value);
}
