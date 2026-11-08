using System.Security.Cryptography;
using System.Text;

namespace WindowsIconsAdmin.Core.Apps;

public class AppDiscoveryService : IAppDiscoveryService
{
    private readonly IShortcutService _shortcutService;
    private readonly IReadOnlyList<string>? _customSearchPaths;

    private static readonly HashSet<string> IgnoredExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".chm", ".htm", ".html", ".url", ".pdf", ".txt", ".md", ".rtf", ".doc", ".docx"
    };

    private static readonly HashSet<string> SharedHostExecutables = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome_proxy.exe", "msedge_proxy.exe", "cmd.exe", "powershell.exe", "pwsh.exe", "wt.exe", "rundll32.exe"
    };

    public AppDiscoveryService(IShortcutService? shortcutService = null, IReadOnlyList<string>? customSearchPaths = null)
    {
        _shortcutService = shortcutService ?? new WindowsShortcutService();
        _customSearchPaths = customSearchPaths;
    }

    public IReadOnlyList<InstalledApp> DiscoverInstalledApps()
    {
        var discoveryLocations = GetDiscoveryLocations();
        var rawTargets = new List<(string ShortcutPath, IconTargetType TargetType, AppSupportStatus SupportStatus)>();

        foreach (var (dirPath, targetType, supportStatus) in discoveryLocations)
        {
            if (!Directory.Exists(dirPath)) continue;

            try
            {
                var lnkFiles = Directory.EnumerateFiles(dirPath, "*.lnk", SearchOption.AllDirectories);
                foreach (var file in lnkFiles)
                {
                    // Ignore uninstallers and help shortcuts
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    if (IsIgnoredShortcut(fileName)) continue;

                    rawTargets.Add((file, targetType, supportStatus));
                }
            }
            catch
            {
                // Access denied or directory traversal error on protected subfolders
            }
        }

        var apps = new List<InstalledApp>();

        foreach (var (shortcutPath, targetType, supportStatus) in rawTargets)
        {
            try
            {
                var (targetPath, iconLoc, iconIdx) = _shortcutService.ReadShortcut(shortcutPath);

                // Filter out non-app targets (directories or document links)
                if (IsNonAppTarget(targetPath)) continue;

                var displayName = CleanDisplayName(Path.GetFileNameWithoutExtension(shortcutPath));
                var appId = ComputeAppId(targetPath, shortcutPath, displayName, iconLoc);

                var target = new IconTarget(
                    TargetType: targetType,
                    TargetPath: shortcutPath,
                    CurrentIconLocation: iconLoc,
                    CurrentIconIndex: iconIdx,
                    SupportStatus: supportStatus,
                    UnsupportedReason: supportStatus == AppSupportStatus.PartiallySupported ? "Requiere permisos de administrador" : null);

                // Group by appId if already found
                var existing = apps.FirstOrDefault(a => a.Id.Equals(appId, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    var updatedTargets = existing.Targets.Append(target).ToList();
                    var index = apps.IndexOf(existing);
                    apps[index] = existing with { Targets = updatedTargets };
                }
                else
                {
                    var originalSource = !string.IsNullOrWhiteSpace(iconLoc) ? iconLoc : targetPath;
                    apps.Add(new InstalledApp(
                        Id: appId,
                        DisplayName: displayName,
                        ExecutablePath: string.IsNullOrWhiteSpace(targetPath) ? null : targetPath,
                        OriginalIconSource: string.IsNullOrWhiteSpace(originalSource) ? null : originalSource,
                        Targets: [target]));
                }
            }
            catch
            {
                // Skip problematic single shortcut
            }
        }

        return apps.OrderBy(a => a.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static bool IsNonAppTarget(string? targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath)) return false;
        var expanded = Environment.ExpandEnvironmentVariables(targetPath.Trim('"', ' '));

        if (Directory.Exists(expanded)) return true;

        var ext = Path.GetExtension(expanded);
        if (!string.IsNullOrEmpty(ext) && IgnoredExtensions.Contains(ext)) return true;

        return false;
    }

    private List<(string Path, IconTargetType TargetType, AppSupportStatus SupportStatus)> GetDiscoveryLocations()
    {
        if (_customSearchPaths != null)
        {
            return _customSearchPaths
                .Select(p => (p, IconTargetType.UserStartMenuShortcut, AppSupportStatus.Supported))
                .ToList();
        }

        var list = new List<(string, IconTargetType, AppSupportStatus)>();

        // 1. User Start Menu (Supported)
        var userStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        if (!string.IsNullOrWhiteSpace(userStartMenu))
        {
            list.Add((userStartMenu, IconTargetType.UserStartMenuShortcut, AppSupportStatus.Supported));
        }

        // 2. User Desktop (Supported)
        var userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        if (!string.IsNullOrWhiteSpace(userDesktop))
        {
            list.Add((userDesktop, IconTargetType.UserDesktopShortcut, AppSupportStatus.Supported));
        }

        // 3. Common Start Menu (PartiallySupported - requires Admin)
        var commonStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms);
        if (!string.IsNullOrWhiteSpace(commonStartMenu))
        {
            list.Add((commonStartMenu, IconTargetType.CommonStartMenuShortcut, AppSupportStatus.PartiallySupported));
        }

        // 4. Common Desktop (PartiallySupported - requires Admin)
        var commonDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
        if (!string.IsNullOrWhiteSpace(commonDesktop))
        {
            list.Add((commonDesktop, IconTargetType.CommonDesktopShortcut, AppSupportStatus.PartiallySupported));
        }

        return list;
    }

    private static string CleanDisplayName(string rawName)
    {
        var cleaned = rawName
            .Replace(" - Shortcut", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" - Acceso directo", "", StringComparison.OrdinalIgnoreCase)
            .Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? rawName : cleaned;
    }

    private static bool IsIgnoredShortcut(string name)
    {
        var lower = name.ToLowerInvariant();
        return lower.Contains("uninstall") ||
               lower.Contains("desinstalar") ||
               lower.Contains("help") ||
               lower.Contains("ayuda") ||
               lower.Contains("readme") ||
               lower.Contains("leeme");
    }

    private static string ComputeAppId(string? targetPath, string shortcutPath, string displayName, string? iconLoc)
    {
        var exeName = !string.IsNullOrWhiteSpace(targetPath) ? Path.GetFileName(targetPath.Trim('"', ' ')) : string.Empty;
        string seed;

        if (!string.IsNullOrWhiteSpace(exeName) && SharedHostExecutables.Contains(exeName))
        {
            // For shared hosts like chrome_proxy.exe, include displayName and iconLoc to avoid collision
            seed = $"{targetPath?.ToLowerInvariant()}|{displayName.ToLowerInvariant()}|{iconLoc?.ToLowerInvariant()}";
        }
        else if (!string.IsNullOrWhiteSpace(targetPath))
        {
            seed = targetPath.ToLowerInvariant();
        }
        else
        {
            seed = shortcutPath.ToLowerInvariant();
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed))).ToLowerInvariant();
        return $"app-{hash[..12]}";
    }
}
