using System.Text;
using System.Text.RegularExpressions;

namespace WindowsIconsAdmin.Core.Rules;

public static class RuleEngine
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    public static IReadOnlyList<RuleMatch> Evaluate(
        IEnumerable<string> folderPaths,
        IEnumerable<FolderRule> rules)
    {
        ArgumentNullException.ThrowIfNull(folderPaths);
        ArgumentNullException.ThrowIfNull(rules);

        var paths = folderPaths.ToList();
        if (paths.Any(p => p is null))
        {
            throw new ArgumentException("Folder paths cannot contain null elements.", nameof(folderPaths));
        }

        var ruleList = rules.ToList();
        if (ruleList.Any(r => r is null))
        {
            throw new ArgumentException("Rules cannot contain null elements.", nameof(rules));
        }

        var ordered = ruleList
            .Where(r => r.Enabled)
            .Select((rule, index) => (rule, index))
            .OrderBy(x => x.rule.Priority)
            .ThenBy(x => x.index)
            .Select(x => Compile(x.rule))
            .ToList();

        var results = new List<RuleMatch>(paths.Count);
        foreach (var path in paths)
        {
            var name = ExtractName(path);
            FolderRule? matched = null;
            if (name.Length > 0)
            {
                var normalizedPath = path.TrimEnd('\\', '/');
                foreach (var candidate in ordered)
                {
                    var targetInput = candidate.Rule.MatchTarget == RuleMatchTarget.FullPath
                        ? normalizedPath
                        : name;

                    if (candidate.IsMatch(targetInput))
                    {
                        matched = candidate.Rule;
                        break;
                    }
                }
            }

            results.Add(new RuleMatch(path, matched));
        }

        return results;
    }

    public static bool TryValidatePattern(
        RuleCondition condition,
        string? pattern,
        bool caseSensitive,
        out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            errorMessage = "El patrón no puede estar vacío.";
            return false;
        }

        if (condition == RuleCondition.Regex)
        {
            var options = RegexOptions.CultureInvariant;
            if (!caseSensitive)
            {
                options |= RegexOptions.IgnoreCase;
            }

            try
            {
                _ = new Regex(pattern, options, RegexTimeout);
            }
            catch (ArgumentException ex)
            {
                errorMessage = $"Expresión regular inválida: {ex.Message}";
                return false;
            }
        }

        errorMessage = null;
        return true;
    }

    public static string ExtractFolderName(string path) => ExtractName(path);

    private static string ExtractName(string path)
    {
        var trimmed = path.TrimEnd('\\', '/');
        var index = trimmed.LastIndexOfAny(['\\', '/']);
        var name = index >= 0 ? trimmed[(index + 1)..] : trimmed;
        return name.Length == 2 && name[1] == ':' && index < 0 ? string.Empty : name;
    }

    private static CompiledRule Compile(FolderRule rule)
    {
        var options = RegexOptions.CultureInvariant;
        if (!rule.CaseSensitive)
        {
            options |= RegexOptions.IgnoreCase;
        }

        if (rule.Condition == RuleCondition.Regex)
        {
            try
            {
                return new CompiledRule(rule, new Regex(rule.Pattern ?? string.Empty, options, RegexTimeout));
            }
            catch (ArgumentException ex)
            {
                throw new InvalidRuleException($"Rule '{rule.Name}' has an invalid regular expression.", ex);
            }
        }

        if (rule.Condition == RuleCondition.Wildcard)
        {
            if (string.IsNullOrEmpty(rule.Pattern))
            {
                return new CompiledRule(rule, null);
            }

            var wildcardRegex = ConvertWildcardToRegex(rule.Pattern);
            return new CompiledRule(rule, new Regex(wildcardRegex, options, RegexTimeout));
        }

        return new CompiledRule(rule, null);
    }

    private static string ConvertWildcardToRegex(string pattern)
    {
        var sb = new StringBuilder("^");
        foreach (var ch in pattern)
        {
            switch (ch)
            {
                case '*':
                    sb.Append(".*");
                    break;
                case '?':
                    sb.Append('.');
                    break;
                default:
                    sb.Append(Regex.Escape(ch.ToString()));
                    break;
            }
        }
        sb.Append('$');
        return sb.ToString();
    }

    private sealed record CompiledRule(FolderRule Rule, Regex? Regex)
    {
        public bool IsMatch(string input)
        {
            if (Regex is not null)
            {
                try
                {
                    return Regex.IsMatch(input);
                }
                catch (RegexMatchTimeoutException)
                {
                    return false;
                }
            }

            var pattern = Rule.Pattern;
            if (string.IsNullOrEmpty(pattern))
            {
                return false;
            }

            var comparison = Rule.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            return Rule.Condition switch
            {
                RuleCondition.Contains => input.Contains(pattern, comparison),
                RuleCondition.StartsWith => input.StartsWith(pattern, comparison),
                RuleCondition.EndsWith => input.EndsWith(pattern, comparison),
                RuleCondition.Equals => input.Equals(pattern, comparison),
                _ => false
            };
        }
    }
}
