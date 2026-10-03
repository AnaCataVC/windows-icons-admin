using System.Text;

namespace WindowsIconsAdmin.Core.Shell;

/// <summary>
/// Helper to read, update and clean up desktop.ini content while preserving
/// user-defined or system sections like [ViewState].
/// </summary>
public static class IniHelper
{
    private const string ShellClassInfoSection = "[.ShellClassInfo]";

    /// <summary>
    /// Injects or updates IconResource under [.ShellClassInfo], preserving all other sections and comments.
    /// </summary>
    public static string SetIconResource(string? existingContent, string iconResource)
    {
        var lines = (existingContent ?? string.Empty)
            .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
            .ToList();

        var sectionStart = -1;
        var nextSectionStart = -1;

        for (var i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.Equals(ShellClassInfoSection, StringComparison.OrdinalIgnoreCase))
            {
                sectionStart = i;
            }
            else if (sectionStart >= 0 && trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                nextSectionStart = i;
                break;
            }
        }

        if (sectionStart < 0)
        {
            // Section doesn't exist; append it
            var sb = new StringBuilder();
            var existing = (existingContent ?? string.Empty).TrimEnd();
            if (!string.IsNullOrEmpty(existing))
            {
                sb.AppendLine(existing);
            }
            sb.AppendLine(ShellClassInfoSection);
            sb.AppendLine($"IconResource={iconResource}");
            return sb.ToString().TrimEnd() + Environment.NewLine;
        }
        else
        {
            // Section exists; replace or add IconResource
            var sectionEnd = nextSectionStart >= 0 ? nextSectionStart : lines.Count;
            var updatedSectionLines = new List<string>();
            var iconResourceUpdated = false;

            for (var i = sectionStart + 1; i < sectionEnd; i++)
            {
                var trimmed = lines[i].Trim();
                if (trimmed.StartsWith("IconResource=", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("IconIndex=", StringComparison.OrdinalIgnoreCase))
                {
                    if (!iconResourceUpdated)
                    {
                        updatedSectionLines.Add($"IconResource={iconResource}");
                        iconResourceUpdated = true;
                    }
                }
                else
                {
                    updatedSectionLines.Add(lines[i]);
                }
            }

            if (!iconResourceUpdated)
            {
                updatedSectionLines.Insert(0, $"IconResource={iconResource}");
            }

            var result = new List<string>();
            for (var i = 0; i <= sectionStart; i++) result.Add(lines[i]);
            result.AddRange(updatedSectionLines);
            for (var i = sectionEnd; i < lines.Count; i++) result.Add(lines[i]);

            return string.Join(Environment.NewLine, result).TrimEnd() + Environment.NewLine;
        }
    }

    /// <summary>
    /// Removes [.ShellClassInfo] and its properties. Returns null if no other sections or meaningful content remains.
    /// </summary>
    public static string? RemoveShellClassInfo(string? existingContent)
    {
        if (string.IsNullOrWhiteSpace(existingContent)) return null;

        var lines = existingContent
            .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)
            .ToList();

        var sectionStart = -1;
        var nextSectionStart = -1;

        for (var i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.Equals(ShellClassInfoSection, StringComparison.OrdinalIgnoreCase))
            {
                sectionStart = i;
            }
            else if (sectionStart >= 0 && trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                nextSectionStart = i;
                break;
            }
        }

        if (sectionStart < 0)
        {
            return existingContent;
        }

        var sectionEnd = nextSectionStart >= 0 ? nextSectionStart : lines.Count;
        var remaining = new List<string>();
        for (var i = 0; i < sectionStart; i++) remaining.Add(lines[i]);
        for (var i = sectionEnd; i < lines.Count; i++) remaining.Add(lines[i]);

        var meaningful = remaining.Any(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith(';') && !l.TrimStart().StartsWith('#'));
        if (!meaningful)
        {
            return null;
        }

        return string.Join(Environment.NewLine, remaining).TrimEnd() + Environment.NewLine;
    }
}
