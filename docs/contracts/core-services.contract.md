# Contract: Core Services v1 (RuleEngine, IcoEncoder, UndoStore)

Status: FROZEN for cleanroom cycle 1. Language: C# 13 / .NET 9, `Nullable` enabled.
Assembly: `WindowsIconsAdmin.Core`. Test framework: xUnit (`tests/WindowsIconsAdmin.Core.Tests`).

This contract is the only shared source of truth between implementer and tester. Anything not stated here is unspecified and MUST NOT be asserted by tests nor relied upon.

Out of scope for this cycle: `ShellIconService` (Win32/desktop.ini/registry), storage strategy, UI.

---

## 1. RuleEngine (`WindowsIconsAdmin.Core.Rules`)

### 1.1 Public API

```csharp
namespace WindowsIconsAdmin.Core.Rules;

public enum RuleCondition { Contains, StartsWith, EndsWith, Regex }

public sealed record FolderRule(
    string Id,
    string Name,
    bool Enabled,
    RuleCondition Condition,
    string Pattern,
    bool CaseSensitive,
    string IconPath,
    int Priority);

public sealed record RuleMatch(string FolderPath, FolderRule? Rule);

public sealed class InvalidRuleException : Exception
{
    public InvalidRuleException(string message);
    public InvalidRuleException(string message, Exception innerException);
}

public static class RuleEngine
{
    public static IReadOnlyList<RuleMatch> Evaluate(
        IEnumerable<string> folderPaths,
        IEnumerable<FolderRule> rules);
}
```

### 1.2 Behavior

- **R1 Output shape:** returns exactly one `RuleMatch` per input path, in the same order as input, with `FolderPath` equal to the original input string (unmodified). Duplicate input paths yield duplicate results.
- **R2 Name extraction:** the matched text is the final path segment (folder name), after trimming trailing `\` and `/` characters. Both `\` and `/` are separators. For `C:\Work\proyecto-1\` the name is `proyecto-1`. Only the folder name is matched, never parent segments.
- **R3 Empty name:** if the final segment is empty (for example a drive root `C:\` or an empty string), no rule matches (`Rule` is `null`).
- **R4 Rule order:** enabled rules are considered by ascending `Priority`; rules with equal `Priority` keep their relative input order. The first matching rule wins. A rule with `Enabled == false` is never considered.
- **R5 No match:** `Rule` is `null`.
- **R6 Conditions:** `Contains`, `StartsWith`, `EndsWith` are substring/prefix/suffix tests on the folder name. `Regex` uses .NET regex semantics with `Regex.IsMatch` (unanchored: it matches if the pattern is found anywhere in the name).
- **R7 Case:** when `CaseSensitive == false`, comparison ignores case (ordinal, culture-invariant). When `true`, it is exact. Applies to all four conditions.
- **R8 Empty pattern:** an enabled non-regex rule with an empty `Pattern` never matches. For `Regex`, an empty pattern is a valid regex and matches every non-empty name.
- **R9 Invalid regex:** if an **enabled** rule has `Condition == Regex` and its pattern is not a valid regular expression, `Evaluate` throws `InvalidRuleException` (with the original exception as inner when available) before returning any result, regardless of whether earlier rules would have matched. Disabled rules are never validated.
- **R10 Catastrophic regex:** a regex evaluation that exceeds 1 second counts as "no match" for that rule and evaluation continues; no exception.
- **R11 Nulls:** `folderPaths == null` or `rules == null` throws `ArgumentNullException`. Null elements inside either collection throw `ArgumentException`.
- **R12 Empty inputs:** empty `folderPaths` returns an empty list. Empty `rules` returns all matches with `Rule == null`.
- **R13 Purity:** no file system access; the folders need not exist. Inputs are not mutated.

### 1.3 Acceptance scenarios

- Given a rule `Contains "proyecto"` (case-insensitive) and folder `C:\Dev\Mi Proyecto Final`, When evaluated, Then the rule matches.
- Given two enabled rules with Priority 1 and 2 that both match, Then the Priority 1 rule is returned.
- Given a disabled matching rule only, Then `Rule` is `null`.
- Given `StartsWith "2024"` and folders `2024-fotos` and `fotos-2024`, Then only the first matches.
- Given a Regex rule `^\d{4}-` and folder `2024-x`, Then it matches; for `x-2024` it does not.

---

## 2. IcoEncoder (`WindowsIconsAdmin.Core.Imaging`)

### 2.1 Public API

```csharp
namespace WindowsIconsAdmin.Core.Imaging;

public sealed class InvalidImageException : Exception
{
    public InvalidImageException(string message);
    public InvalidImageException(string message, Exception innerException);
}

public static class IcoEncoder
{
    public static IReadOnlyList<int> Sizes { get; }   // exactly: 16, 24, 32, 48, 64, 128, 256
    public static byte[] FromPng(byte[] pngBytes);
}
```

### 2.2 Behavior

- **I1 Input:** `pngBytes` is the complete content of a PNG file (any dimensions >= 1x1, square or not; with or without alpha channel; any PNG color type/bit depth).
- **I2 Output container:** returns a valid ICO file. `ICONDIR`: reserved = 0, type = 1, count = 7. One `ICONDIRENTRY` per size in `Sizes`, in ascending size order.
- **I3 Directory entries:** for size S, width and height bytes equal `S`, except 256 which is stored as `0` (ICO convention). Planes = 1, bit count = 32. `bytesInRes` and `imageOffset` must be exact: every entry's data lies fully inside the file, entries do not overlap, and offsets are strictly increasing.
- **I4 Image data format:** for S <= 128 the entry data is an uncompressed DIB: a `BITMAPINFOHEADER` (biSize = 40, biWidth = S, biHeight = 2*S, biPlanes = 1, biBitCount = 32, biCompression = 0 (BI_RGB)), followed by S*S BGRA pixels in bottom-up row order, followed by a 1-bit AND mask (rows padded to 4 bytes). For S = 256 the entry data is a complete PNG file (begins with the 8-byte PNG signature) of exactly 256x256 pixels.
- **I5 Aspect ratio:** non-square sources are scaled uniformly to fit within SxS and centered on a fully transparent SxS canvas (letterbox); the image is never stretched. Square sources fill the canvas.
- **I6 Transparency:** alpha is preserved. Pixels that are fully transparent in the source remain fully transparent (alpha = 0) in every output size. An opaque source yields opaque pixels in the central region (alpha = 255). Sources without alpha are treated as fully opaque.
- **I7 Upscaling:** sources smaller than a target size are scaled up so all 7 entries are always present.
- **I8 Determinism:** the same input bytes always produce the same output bytes.
- **I9 Errors:** `pngBytes == null` throws `ArgumentNullException`. Empty array, truncated data, or data that is not a decodable PNG throws `InvalidImageException`.
- **I10 Purity:** no file system or registry access; input array is not mutated.

### 2.3 Acceptance scenarios

- Given a 64x64 opaque red PNG, When converted, Then the result parses as ICO with 7 entries, the 256 entry holds a PNG signature, and the 32x32 DIB center pixel is BGRA (0, 0, 255, 255).
- Given a 200x100 PNG, Then in the 128 entry the top row and bottom row are fully transparent and the middle rows contain image pixels.
- Given the bytes `[1,2,3]`, Then `InvalidImageException` is thrown.

---

## 3. UndoStore (`WindowsIconsAdmin.Core.History`)

### 3.1 Public API

```csharp
namespace WindowsIconsAdmin.Core.History;

public enum OperationKind { ApplyIcon, RestoreDefault }

public sealed record FolderSnapshot(
    string FolderPath,
    bool HadDesktopIni,
    string? PreviousDesktopIniContent,
    uint PreviousFolderAttributes,
    uint? PreviousDesktopIniAttributes,
    string? CopiedIconFileName);

public sealed record BatchRecord(
    string BatchId,
    DateTimeOffset TimestampUtc,
    OperationKind Kind,
    string? SourceIconPath,
    IReadOnlyList<FolderSnapshot> Folders,
    bool Reverted);

public sealed class UndoStore
{
    public const int MaxBatches = 50;

    public UndoStore(string filePath);

    public void Add(BatchRecord batch);
    public BatchRecord? GetLastPending();
    public void MarkReverted(string batchId);
    public IReadOnlyList<BatchRecord> GetAll();
}
```

### 3.2 Behavior

- **U1 Persistence:** state is stored as JSON at `filePath`. A new `UndoStore` constructed on the same path sees every batch added by previous instances, with all fields round-tripped exactly (including `null` values, `DateTimeOffset` instant, enum values, folder order and the `Reverted` flag).
- **U2 Missing file:** if the file does not exist, the store starts empty and no file is required until the first mutation. Missing parent directories are created on the first write.
- **U3 Corrupt file:** if the file exists but cannot be parsed, the original content is preserved by copying it to `filePath + ".bak"`, the store starts empty, and no exception is thrown.
- **U4 Order:** `GetAll()` returns batches in insertion order (oldest first).
- **U5 Last pending:** `GetLastPending()` returns the most recently added batch whose `Reverted == false`, or `null` if none.
- **U6 Mark reverted:** `MarkReverted(id)` sets `Reverted = true` for that batch and persists it. Calling it on an already reverted batch is a no-op. Unknown id throws `KeyNotFoundException`.
- **U7 Duplicates:** `Add` with a `BatchId` that already exists throws `ArgumentException`; the store is unchanged.
- **U8 Retention:** after `Add`, at most `MaxBatches` (50) batches are retained; when exceeded, the oldest batches are dropped (regardless of `Reverted`).
- **U9 Nulls and blanks:** `filePath` null or whitespace throws `ArgumentException` (`ArgumentNullException` for null); `Add(null)` throws `ArgumentNullException`; `BatchId` null or whitespace throws `ArgumentException`.
- **U10 Durability:** a failed or interrupted write must never leave the main file truncated or half-written (write to a temporary file in the same directory, then replace). After any completed public call, the file on disk reflects the in-memory state.
- **U11 Thread safety:** all public members are safe to call concurrently from multiple threads on one instance; no batch is lost under concurrent `Add` calls (up to the retention limit).
- **U12 Immutability:** records returned by the store are not affected by later mutation of the collections passed to `Add`.

### 3.3 Acceptance scenarios

- Given an empty store, When two batches A then B are added, Then `GetLastPending()` is B; after `MarkReverted("B")` it is A; after reverting A it is `null`.
- Given a store with 1 batch persisted, When a new instance opens the same file, Then `GetAll()` equals the original list.
- Given a file containing `not json`, When opened, Then the store is empty and `<file>.bak` contains `not json`.
- Given 51 distinct batches added in order, Then `GetAll()` has 50 items and the first batch is gone.

---

## 3.4 Clarifications (amendment 1)

- **I4b:** for S <= 128, `bytesInRes` equals exactly `40 + S*S*4 + maskBytes`, where `maskBytes = ((S + 31) / 32) * 4 * S` (1-bit AND mask rows padded to 4 bytes). The AND mask bits are all 0 (alpha channel carries transparency).
- **I6b:** the PNG layer (S = 256) is verified by tests only via the PNG signature and IHDR width/height = 256; its pixel content is unspecified beyond being a valid PNG of the same image.
- **U3b:** if `filePath + ".bak"` already exists it is overwritten.
- **U9b:** a `null` element inside `BatchRecord.Folders` makes `Add` throw `ArgumentException`; a `null` `Folders` list throws `ArgumentNullException`. The store is unchanged in both cases.
- **R7b:** case-insensitive comparison is guaranteed for ASCII letters; behavior for other scripts is unspecified.
- **R10b:** a catastrophic regex must not make `Evaluate` run unbounded; tests may assert only a loose upper bound (no more than 30 seconds for the whole call).

---

## 4. Prohibited details (not dictated by this contract)

JSON property names and schema layout, regex/image libraries used, resampling algorithm, internal helper classes, file names of temp files, logging. Tests must not assert on any of them (for example: no assertions on exact exception message text, JSON structure, or exact resampled pixel values away from the checks stated above).
