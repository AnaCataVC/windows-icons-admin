namespace WindowsIconsAdmin.Core.Safety;

public sealed record SafetyValidationResult(
    bool IsValid,
    SafetyViolationKind ViolationKind,
    string? Reason)
{
    public static SafetyValidationResult Success() =>
        new(true, SafetyViolationKind.None, null);

    public static SafetyValidationResult Violation(SafetyViolationKind kind, string reason) =>
        new(false, kind, reason);
}
