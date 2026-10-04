namespace WindowsIconsAdmin.Core.Rules;

public enum RuleMatchTarget
{
    FolderName = 0,
    FullPath = 1
}

public sealed record FolderRule(
    string Id,
    string Name,
    bool Enabled,
    RuleCondition Condition,
    string Pattern,
    bool CaseSensitive,
    string IconPath,
    int Priority,
    RuleMatchTarget MatchTarget = RuleMatchTarget.FolderName);
