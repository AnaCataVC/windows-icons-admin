using System.Collections.Frozen;

namespace WindowsIconsAdmin.Core.Safety;

public static class SystemFolderGuard
{
    private static readonly string[] BaseRestrictedList =
    [
        ".exe", ".dll", ".sys", ".bat", ".cmd", ".com", ".scr", ".msi", ".vbs", ".ps1", ".reg", ".lnk", ".cpl", ".drv"
    ];

    public static readonly IReadOnlySet<string> RestrictedExtensions =
        new HashSet<string>(
            BaseRestrictedList.Concat(BaseRestrictedList.Select(e => e.TrimStart('.'))),
            StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> KnownFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Desktop", "Documents", "Downloads", "Music", "Pictures", "Videos",
        "Favorites", "Contacts", "Links", "Searches", "Saved Games", "3D Objects"
    };

    public static bool IsRestrictedExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) return false;
        var trimmed = extension.Trim();
        if (!trimmed.StartsWith('.'))
        {
            trimmed = "." + trimmed;
        }
        return RestrictedExtensions.Contains(trimmed);
    }

    public static SafetyValidationResult ValidateExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return SafetyValidationResult.Violation(SafetyViolationKind.InvalidCharacters, "Extension cannot be null or empty.");
        }

        if (IsRestrictedExtension(extension))
        {
            return SafetyValidationResult.Violation(SafetyViolationKind.RestrictedFileExtension, $"Extension '{extension}' is restricted.");
        }

        return SafetyValidationResult.Success();
    }

    public static void EnsureSafeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            throw new ArgumentException("Extension cannot be null or empty.", nameof(extension));
        }

        var result = ValidateExtension(extension);
        if (!result.IsValid)
        {
            if (result.ViolationKind == SafetyViolationKind.RestrictedFileExtension)
            {
                throw new RestrictedExtensionException(result.Reason ?? "Restricted extension.", extension);
            }

            throw new ArgumentException(result.Reason, nameof(extension));
        }
    }

    public static SafetyValidationResult ValidateEmbeddedFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return SafetyValidationResult.Violation(SafetyViolationKind.InvalidCharacters, "File name cannot be null or empty.");
        }

        var trimmed = fileName.Trim();

        // SC-E2: Traversal Sequences
        if (trimmed.Contains("..") ||
            trimmed.Contains('/') ||
            trimmed.Contains('\\') ||
            Path.GetFileName(trimmed) != trimmed)
        {
            return SafetyValidationResult.Violation(SafetyViolationKind.PathTraversal, "File name contains path traversal sequences or directory separators.");
        }

        // SC-E3: Invalid Characters
        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return SafetyValidationResult.Violation(SafetyViolationKind.InvalidCharacters, "File name contains invalid characters.");
        }

        // SC-E4: Extension Restriction
        var ext = Path.GetExtension(trimmed);
        if (IsRestrictedExtension(ext))
        {
            return SafetyValidationResult.Violation(SafetyViolationKind.RestrictedFileExtension, $"File extension '{ext}' is restricted.");
        }

        if (!trimmed.EndsWith(".ico", StringComparison.OrdinalIgnoreCase))
        {
            return SafetyValidationResult.Violation(SafetyViolationKind.InvalidCharacters, "Embedded file name must have a .ico extension.");
        }

        return SafetyValidationResult.Success();
    }

    public static void EnsureSafeEmbeddedFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));
        }

        var result = ValidateEmbeddedFileName(fileName);
        if (!result.IsValid)
        {
            if (result.ViolationKind == SafetyViolationKind.PathTraversal)
            {
                throw new PathTraversalException(result.Reason ?? "Path traversal detected in embedded file name.");
            }

            throw new ArgumentException(result.Reason, nameof(fileName));
        }
    }

    public static bool IsContainedWithin(string parentPath, string candidateChildPath)
    {
        if (string.IsNullOrWhiteSpace(parentPath) || string.IsNullOrWhiteSpace(candidateChildPath))
        {
            return false;
        }

        try
        {
            var fullParent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullChild = Path.GetFullPath(candidateChildPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // SC-C3: Exact match must return false
            if (string.Equals(fullParent, fullChild, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var parentWithSep = fullParent + Path.DirectorySeparatorChar;
            var fullChildWithSep = fullChild + Path.DirectorySeparatorChar;

            return fullChildWithSep.StartsWith(parentWithSep, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static SafetyValidationResult ValidateTargetFolder(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return SafetyValidationResult.Violation(SafetyViolationKind.InvalidCharacters, "Folder path cannot be null or empty.");
        }

        var trimmed = folderPath.Trim();

        if (trimmed.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return SafetyValidationResult.Violation(SafetyViolationKind.InvalidCharacters, "Folder path contains invalid characters.");
        }

        // SC-F2: Drive Root checks
        if (trimmed == "/" || trimmed == "\\" || trimmed == "\\\\" ||
            (trimmed.Length == 2 && char.IsLetter(trimmed[0]) && trimmed[1] == ':') ||
            (trimmed.Length == 3 && char.IsLetter(trimmed[0]) && trimmed[1] == ':' && (trimmed[2] == '\\' || trimmed[2] == '/')))
        {
            return SafetyValidationResult.Violation(SafetyViolationKind.DriveRoot, "Cannot customize drive root.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(trimmed);
        }
        catch (Exception ex)
        {
            return SafetyValidationResult.Violation(SafetyViolationKind.InvalidCharacters, $"Invalid path: {ex.Message}");
        }

        var root = Path.GetPathRoot(fullPath);
        if (!string.IsNullOrEmpty(root))
        {
            var cleanFull = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var cleanRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(cleanFull, cleanRoot, StringComparison.OrdinalIgnoreCase))
            {
                return SafetyValidationResult.Violation(SafetyViolationKind.DriveRoot, "Cannot customize drive root.");
            }
        }

        // SC-F3: Windows & System Roots
        foreach (var sysRoot in GetSystemRootPaths())
        {
            if (IsSameOrSubfolder(sysRoot, fullPath))
            {
                return SafetyValidationResult.Violation(SafetyViolationKind.SystemRoot, $"Target folder is within Windows system root '{sysRoot}'.");
            }
        }

        // SC-F4: Program Files
        foreach (var pf in GetProgramFilesPaths())
        {
            if (IsSameOrSubfolder(pf, fullPath))
            {
                return SafetyValidationResult.Violation(SafetyViolationKind.ProgramFiles, $"Target folder is within Program Files '{pf}'.");
            }
        }

        // SC-F5: User Profile Root
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(userProfile))
        {
            userProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? @"C:\Users\Default";
        }
        var fullUserProfile = Path.GetFullPath(userProfile).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var cleanCandidate = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (string.Equals(cleanCandidate, fullUserProfile, StringComparison.OrdinalIgnoreCase))
        {
            return SafetyValidationResult.Violation(SafetyViolationKind.UserProfileRoot, "Target folder is user profile root.");
        }

        var usersParent = Path.GetDirectoryName(fullUserProfile);
        if (!string.IsNullOrEmpty(usersParent))
        {
            if (string.Equals(cleanCandidate, usersParent, StringComparison.OrdinalIgnoreCase))
            {
                return SafetyValidationResult.Violation(SafetyViolationKind.UserProfileRoot, "Target folder is users directory.");
            }

            var candidateParent = Path.GetDirectoryName(cleanCandidate);
            if (string.Equals(candidateParent, usersParent, StringComparison.OrdinalIgnoreCase))
            {
                return SafetyValidationResult.Violation(SafetyViolationKind.UserProfileRoot, "Target folder is a user profile root.");
            }
        }

        // SC-F6: Sensitive AppData (exempting application's own cache folder "WindowsIconsAdmin")
        if (IsAppDataFolder(fullPath))
        {
            if (!IsAppOwnFolder(fullPath))
            {
                return SafetyValidationResult.Violation(SafetyViolationKind.AppDataSensitive, "Target folder is within sensitive AppData.");
            }
        }

        return SafetyValidationResult.Success();
    }

    public static void EnsureSafeTargetFolder(string? folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ArgumentException("Target folder path cannot be null or empty.", nameof(folderPath));
        }

        var result = ValidateTargetFolder(folderPath);
        if (!result.IsValid)
        {
            if (result.ViolationKind == SafetyViolationKind.InvalidCharacters)
            {
                throw new ArgumentException(result.Reason, nameof(folderPath));
            }

            throw new SystemProtectionException(result.Reason ?? "Protected folder.", result.ViolationKind);
        }
    }

    public static bool IsProtectedFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return true;
        return !ValidateTargetFolder(folderPath).IsValid;
    }

    public static bool IsKnownFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return false;

        string full;
        try
        {
            full = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return false;
        }

        // 1. Check Windows Environment SpecialFolders
        Span<Environment.SpecialFolder> specialFolders = stackalloc[]
        {
            Environment.SpecialFolder.Desktop,
            Environment.SpecialFolder.DesktopDirectory,
            Environment.SpecialFolder.MyDocuments,
            Environment.SpecialFolder.MyMusic,
            Environment.SpecialFolder.MyPictures,
            Environment.SpecialFolder.MyVideos,
            Environment.SpecialFolder.Favorites,
            Environment.SpecialFolder.Templates,
            Environment.SpecialFolder.CommonDesktopDirectory,
            Environment.SpecialFolder.CommonDocuments,
            Environment.SpecialFolder.CommonMusic,
            Environment.SpecialFolder.CommonPictures,
            Environment.SpecialFolder.CommonVideos
        };

        foreach (var sf in specialFolders)
        {
            var sfPath = Environment.GetFolderPath(sf);
            if (!string.IsNullOrWhiteSpace(sfPath))
            {
                var cleanSf = Path.GetFullPath(sfPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(full, cleanSf, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        // User Downloads
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            var downloads = Path.Combine(userProfile, "Downloads");
            if (string.Equals(full, Path.GetFullPath(downloads).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // 2. Check standard KnownFolder directory names under any user profile
        var folderName = Path.GetFileName(full);
        var parent = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(parent))
        {
            var grandparent = Path.GetDirectoryName(parent);
            if (!string.IsNullOrEmpty(grandparent) &&
                (string.Equals(Path.GetFileName(grandparent), "Users", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(grandparent, Path.GetDirectoryName(userProfile), StringComparison.OrdinalIgnoreCase)))
            {
                if (KnownFolderNames.Contains(folderName))
                {
                    return true;
                }
            }
        }

        // 3. Inspect existing desktop.ini for localized name or CLSID
        try
        {
            var ini = Path.Combine(full, "desktop.ini");
            if (File.Exists(ini))
            {
                var content = File.ReadAllText(ini);
                if (content.Contains("LocalizedResourceName", StringComparison.OrdinalIgnoreCase) ||
                    content.Contains("CLSID", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Ignore I/O errors during passive probe
        }

        return false;
    }

    private static bool IsSameOrSubfolder(string parent, string candidate)
    {
        var fullParent = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var fullCandidate = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (string.Equals(fullParent, fullCandidate, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var parentWithSep = fullParent + Path.DirectorySeparatorChar;
        return fullCandidate.StartsWith(parentWithSep, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GetSystemRootPaths()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrWhiteSpace(win)) set.Add(win);

        var sys = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (!string.IsNullOrWhiteSpace(sys)) set.Add(sys);

        var sysX86 = Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
        if (!string.IsNullOrWhiteSpace(sysX86)) set.Add(sysX86);

        var envSysRoot = Environment.GetEnvironmentVariable("SystemRoot");
        if (!string.IsNullOrWhiteSpace(envSysRoot)) set.Add(envSysRoot);

        var envWindir = Environment.GetEnvironmentVariable("windir");
        if (!string.IsNullOrWhiteSpace(envWindir)) set.Add(envWindir);

        set.Add(@"C:\Windows");
        return set;
    }

    private static IEnumerable<string> GetProgramFilesPaths()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(pf)) set.Add(pf);

        var pfX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(pfX86)) set.Add(pfX86);

        var envPf = Environment.GetEnvironmentVariable("ProgramFiles");
        if (!string.IsNullOrWhiteSpace(envPf)) set.Add(envPf);

        var envPfX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
        if (!string.IsNullOrWhiteSpace(envPfX86)) set.Add(envPfX86);

        var envPfW64 = Environment.GetEnvironmentVariable("ProgramW6432");
        if (!string.IsNullOrWhiteSpace(envPfW64)) set.Add(envPfW64);

        set.Add(@"C:\Program Files");
        set.Add(@"C:\Program Files (x86)");
        return set;
    }

    private static bool IsAppDataFolder(string fullPath)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        if (!string.IsNullOrWhiteSpace(appData) && IsSameOrSubfolder(appData, fullPath))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(localAppData) && IsSameOrSubfolder(localAppData, fullPath))
        {
            return true;
        }

        var clean = fullPath.Replace('/', '\\');
        if (clean.Contains(@"\AppData\Local", StringComparison.OrdinalIgnoreCase) ||
            clean.Contains(@"\AppData\Roaming", StringComparison.OrdinalIgnoreCase) ||
            clean.Contains(@"\AppData\LocalLow", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsAppOwnFolder(string fullPath)
    {
        var parts = fullPath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Any(p => p.Equals("WindowsIconsAdmin", StringComparison.OrdinalIgnoreCase));
    }
}
