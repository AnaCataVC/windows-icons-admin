namespace WindowsIconsAdmin.Core.Safety;

public class SystemProtectionException : Exception
{
    public SafetyViolationKind ViolationKind { get; }

    public SystemProtectionException(string message, SafetyViolationKind kind = SafetyViolationKind.SystemRoot)
        : base(message)
    {
        ViolationKind = kind;
    }
}

public class PathTraversalException : Exception
{
    public PathTraversalException(string message)
        : base(message)
    {
    }
}

public class RestrictedExtensionException : Exception
{
    public string Extension { get; }

    public RestrictedExtensionException(string message, string extension)
        : base(message)
    {
        Extension = extension;
    }
}
