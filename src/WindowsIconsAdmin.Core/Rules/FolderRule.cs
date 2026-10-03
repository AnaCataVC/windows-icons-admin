namespace WindowsIconsAdmin.Core.Rules;

public sealed record FolderRule(
    string Id,
    string Name,
    bool Enabled,
    RuleCondition Condition,
    string Pattern,
    bool CaseSensitive,
    string IconPath,
    int Priority);
