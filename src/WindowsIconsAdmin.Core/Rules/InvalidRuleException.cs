namespace WindowsIconsAdmin.Core.Rules;

public sealed class InvalidRuleException : Exception
{
    public InvalidRuleException(string message) : base(message)
    {
    }

    public InvalidRuleException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
