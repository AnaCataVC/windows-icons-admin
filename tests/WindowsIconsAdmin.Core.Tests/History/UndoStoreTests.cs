using WindowsIconsAdmin.Core.History;

namespace WindowsIconsAdmin.Core.Tests.History;

public sealed class UndoStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wia-undo-" + Guid.NewGuid().ToString("N"));

    public UndoStoreTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    private string NewFile(string name = "undo.json") => Path.Combine(_dir, name);

    private static FolderSnapshot Snapshot(string path, bool hadIni = true, string? content = "[.ShellClassInfo]\r\nIconFile=a.ico", uint attrs = 16, uint? iniAttrs = 6, string? copied = "a.ico")
        => new(path, hadIni, content, attrs, iniAttrs, copied);

    private static BatchRecord Batch(
        string id,
        bool reverted = false,
        OperationKind kind = OperationKind.ApplyIcon,
        IReadOnlyList<FolderSnapshot>? folders = null,
        string? source = @"C:\icons\a.ico",
        DateTimeOffset? ts = null)
        => new(id, ts ?? new DateTimeOffset(2025, 3, 4, 5, 6, 7, TimeSpan.Zero).AddMinutes(1), kind, source, folders ?? [Snapshot(@"C:\f\" + id)], reverted);

    private static void AssertSameBatch(BatchRecord expected, BatchRecord actual)
    {
        Assert.Equal(expected.BatchId, actual.BatchId);
        Assert.Equal(expected.TimestampUtc.UtcDateTime, actual.TimestampUtc.UtcDateTime);
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.SourceIconPath, actual.SourceIconPath);
        Assert.Equal(expected.Reverted, actual.Reverted);
        Assert.Equal(expected.Folders.Count, actual.Folders.Count);
        for (var i = 0; i < expected.Folders.Count; i++)
        {
            Assert.Equal(expected.Folders[i], actual.Folders[i]);
        }
    }

    private static void AssertSameBatches(IReadOnlyList<BatchRecord> expected, IReadOnlyList<BatchRecord> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            AssertSameBatch(expected[i], actual[i]);
        }
    }

    // U1
    [Fact]
    public void U1_NewInstanceSeesBatchesFromPreviousInstance()
    {
        var path = NewFile();
        var a = Batch("A");
        var b = Batch("B", reverted: true, kind: OperationKind.RestoreDefault);
        var first = new UndoStore(path);
        first.Add(a);
        first.Add(b);

        var second = new UndoStore(path);

        AssertSameBatches([a, b], second.GetAll());
    }

    [Fact]
    public void U1_NullValuesRoundTrip()
    {
        var path = NewFile();
        var folder = new FolderSnapshot(@"C:\x", false, null, 0, null, null);
        var batch = new BatchRecord("N", DateTimeOffset.UnixEpoch, OperationKind.RestoreDefault, null, [folder], false);
        new UndoStore(path).Add(batch);

        var loaded = new UndoStore(path).GetAll();

        AssertSameBatches([batch], loaded);
        Assert.Null(loaded[0].SourceIconPath);
        Assert.Null(loaded[0].Folders[0].PreviousDesktopIniContent);
        Assert.Null(loaded[0].Folders[0].PreviousDesktopIniAttributes);
        Assert.Null(loaded[0].Folders[0].CopiedIconFileName);
    }

    [Fact]
    public void U1_TimestampInstantRoundTrips()
    {
        var path = NewFile();
        var ts = new DateTimeOffset(2024, 12, 31, 23, 59, 59, 123, TimeSpan.FromHours(-3));
        new UndoStore(path).Add(Batch("T", ts: ts));

        var loaded = new UndoStore(path).GetAll()[0];

        Assert.Equal(ts.UtcDateTime, loaded.TimestampUtc.UtcDateTime);
    }

    [Fact]
    public void U1_EnumValuesAndFolderOrderRoundTrip()
    {
        var path = NewFile();
        var folders = new[] { Snapshot(@"C:\3"), Snapshot(@"C:\1"), Snapshot(@"C:\2") };
        new UndoStore(path).Add(Batch("K", kind: OperationKind.RestoreDefault, folders: folders));

        var loaded = new UndoStore(path).GetAll()[0];

        Assert.Equal(OperationKind.RestoreDefault, loaded.Kind);
        Assert.Equal([@"C:\3", @"C:\1", @"C:\2"], loaded.Folders.Select(f => f.FolderPath).ToArray());
    }

    [Fact]
    public void U1_LargeAttributeValuesRoundTrip()
    {
        var path = NewFile();
        var folder = new FolderSnapshot(@"C:\x", true, "x", uint.MaxValue, 4000000000u, "i.ico");
        new UndoStore(path).Add(Batch("L", folders: [folder]));

        var loaded = new UndoStore(path).GetAll()[0].Folders[0];

        Assert.Equal(folder, loaded);
    }

    [Fact]
    public void U1_SpecialCharactersRoundTrip()
    {
        var path = NewFile();
        var folder = new FolderSnapshot("C:\\ñandú \"q\" \\ /\u00e9", true, "line1\r\nline2\t\"x\"\u00fc", 1, 2, "ic\u00f3n.ico");
        new UndoStore(path).Add(Batch("S", folders: [folder], source: "C:\\ícono \"x\".ico"));

        var loaded = new UndoStore(path).GetAll()[0];

        Assert.Equal(folder, loaded.Folders[0]);
        Assert.Equal("C:\\ícono \"x\".ico", loaded.SourceIconPath);
    }

    [Fact]
    public void U1_EmptyFoldersListRoundTrips()
    {
        var path = NewFile();
        new UndoStore(path).Add(Batch("E", folders: []));

        Assert.Empty(new UndoStore(path).GetAll()[0].Folders);
    }

    [Fact]
    public void U1_RevertedFlagPersistsAfterMarkReverted()
    {
        var path = NewFile();
        var store = new UndoStore(path);
        store.Add(Batch("A"));
        store.MarkReverted("A");

        Assert.True(new UndoStore(path).GetAll()[0].Reverted);
    }

    // U2
    [Fact]
    public void U2_MissingFileStartsEmptyAndCreatesNothing()
    {
        var path = NewFile();

        var store = new UndoStore(path);

        Assert.Empty(store.GetAll());
        Assert.Null(store.GetLastPending());
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void U2_MissingParentDirectoriesAreCreatedOnFirstWrite()
    {
        var path = Path.Combine(_dir, "a", "b", "c", "undo.json");
        var store = new UndoStore(path);

        store.Add(Batch("A"));

        Assert.True(File.Exists(path));
        Assert.Single(new UndoStore(path).GetAll());
    }

    // U3 / U3b
    [Fact]
    public void U3_CorruptFileIsBackedUpAndStoreStartsEmpty()
    {
        var path = NewFile();
        File.WriteAllText(path, "not json");

        var store = new UndoStore(path);

        Assert.Empty(store.GetAll());
        Assert.True(File.Exists(path + ".bak"));
        Assert.Equal("not json", File.ReadAllText(path + ".bak"));
    }

    [Fact]
    public void U3_StoreIsUsableAfterCorruptFile()
    {
        var path = NewFile();
        File.WriteAllText(path, "{{{ broken");
        var store = new UndoStore(path);

        store.Add(Batch("A"));

        Assert.Single(new UndoStore(path).GetAll());
        Assert.Equal("{{{ broken", File.ReadAllText(path + ".bak"));
    }

    [Fact]
    public void U3b_ExistingBackupIsOverwritten()
    {
        var path = NewFile();
        File.WriteAllText(path + ".bak", "old backup");
        File.WriteAllText(path, "not json");

        _ = new UndoStore(path);

        Assert.Equal("not json", File.ReadAllText(path + ".bak"));
    }

    // U4
    [Fact]
    public void U4_GetAllReturnsInsertionOrder()
    {
        var store = new UndoStore(NewFile());
        var ids = new[] { "Z", "A", "M", "B" };
        foreach (var id in ids)
        {
            store.Add(Batch(id));
        }

        Assert.Equal(ids, store.GetAll().Select(b => b.BatchId).ToArray());
    }

    // U5
    [Fact]
    public void U5_NoBatchesReturnsNull()
    {
        Assert.Null(new UndoStore(NewFile()).GetLastPending());
    }

    [Fact]
    public void U5_ReturnsMostRecentNonReverted()
    {
        var store = new UndoStore(NewFile());
        store.Add(Batch("A"));
        store.Add(Batch("B"));
        store.Add(Batch("C", reverted: true));

        Assert.Equal("B", store.GetLastPending()?.BatchId);
    }

    [Fact]
    public void U5_AllRevertedReturnsNull()
    {
        var store = new UndoStore(NewFile());
        store.Add(Batch("A", reverted: true));

        Assert.Null(store.GetLastPending());
    }

    [Fact]
    public void U5_ReturnsFullRecord()
    {
        var store = new UndoStore(NewFile());
        var a = Batch("A");
        store.Add(a);

        AssertSameBatch(a, store.GetLastPending()!);
    }

    // U6
    [Fact]
    public void U6_MarkRevertedSetsFlag()
    {
        var store = new UndoStore(NewFile());
        store.Add(Batch("A"));

        store.MarkReverted("A");

        Assert.True(store.GetAll()[0].Reverted);
    }

    [Fact]
    public void U6_MarkRevertedKeepsOtherFieldsAndOtherBatches()
    {
        var store = new UndoStore(NewFile());
        var a = Batch("A");
        var b = Batch("B");
        store.Add(a);
        store.Add(b);

        store.MarkReverted("A");

        var all = store.GetAll();
        AssertSameBatch(a with { Reverted = true }, all[0]);
        AssertSameBatch(b, all[1]);
    }

    [Fact]
    public void U6_MarkRevertedTwiceIsNoOp()
    {
        var store = new UndoStore(NewFile());
        store.Add(Batch("A"));
        store.MarkReverted("A");

        store.MarkReverted("A");

        Assert.True(store.GetAll()[0].Reverted);
        Assert.Single(store.GetAll());
    }

    [Fact]
    public void U6_UnknownIdThrowsKeyNotFound()
    {
        var store = new UndoStore(NewFile());
        store.Add(Batch("A"));

        Assert.Throws<KeyNotFoundException>(() => store.MarkReverted("nope"));
    }

    [Fact]
    public void U6_UnknownIdOnEmptyStoreThrowsKeyNotFound()
    {
        Assert.Throws<KeyNotFoundException>(() => new UndoStore(NewFile()).MarkReverted("x"));
    }

    // U7
    [Fact]
    public void U7_DuplicateIdThrowsAndStoreUnchanged()
    {
        var path = NewFile();
        var store = new UndoStore(path);
        var a = Batch("A");
        store.Add(a);

        Assert.Throws<ArgumentException>(() => store.Add(Batch("A", kind: OperationKind.RestoreDefault)));

        AssertSameBatches([a], store.GetAll());
        AssertSameBatches([a], new UndoStore(path).GetAll());
    }

    // U8
    [Fact]
    public void U8_Adding51BatchesKeeps50AndDropsOldest()
    {
        var store = new UndoStore(NewFile());
        for (var i = 0; i < 51; i++)
        {
            store.Add(Batch("B" + i));
        }

        var all = store.GetAll();
        Assert.Equal(50, all.Count);
        Assert.Equal("B1", all[0].BatchId);
        Assert.Equal("B50", all[^1].BatchId);
        Assert.DoesNotContain(all, b => b.BatchId == "B0");
    }

    [Fact]
    public void U8_ExactlyMaxBatchesAreRetained()
    {
        var store = new UndoStore(NewFile());
        for (var i = 0; i < UndoStore.MaxBatches; i++)
        {
            store.Add(Batch("B" + i));
        }

        Assert.Equal(50, store.GetAll().Count);
        Assert.Equal("B0", store.GetAll()[0].BatchId);
    }

    [Fact]
    public void U8_MaxBatchesIs50()
    {
        Assert.Equal(50, UndoStore.MaxBatches);
    }

    [Fact]
    public void U8_OldestDroppedEvenIfNotReverted()
    {
        var store = new UndoStore(NewFile());
        store.Add(Batch("pending-old"));
        for (var i = 0; i < 50; i++)
        {
            store.Add(Batch("R" + i, reverted: true));
        }

        Assert.DoesNotContain(store.GetAll(), b => b.BatchId == "pending-old");
        Assert.Null(store.GetLastPending());
    }

    [Fact]
    public void U8_RetentionIsPersisted()
    {
        var path = NewFile();
        var store = new UndoStore(path);
        for (var i = 0; i < 55; i++)
        {
            store.Add(Batch("B" + i));
        }

        var reloaded = new UndoStore(path).GetAll();
        Assert.Equal(50, reloaded.Count);
        Assert.Equal("B5", reloaded[0].BatchId);
    }

    [Fact]
    public void U8_DroppedIdCanBeAddedAgain()
    {
        var store = new UndoStore(NewFile());
        for (var i = 0; i < 51; i++)
        {
            store.Add(Batch("B" + i));
        }

        store.Add(Batch("B0"));

        Assert.Equal("B0", store.GetAll()[^1].BatchId);
    }

    // U9 / U9b
    [Fact]
    public void U9_NullFilePathThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new UndoStore(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void U9_BlankFilePathThrowsArgumentException(string path)
    {
        Assert.ThrowsAny<ArgumentException>(() => new UndoStore(path));
    }

    [Fact]
    public void U9_AddNullThrowsArgumentNull()
    {
        var store = new UndoStore(NewFile());

        Assert.Throws<ArgumentNullException>(() => store.Add(null!));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void U9_BlankBatchIdThrowsArgumentException(string? id)
    {
        var store = new UndoStore(NewFile());

        Assert.ThrowsAny<ArgumentException>(() => store.Add(Batch(id!)));
        Assert.Empty(store.GetAll());
    }

    [Fact]
    public void U9b_NullFolderElementThrowsAndStoreUnchanged()
    {
        var path = NewFile();
        var store = new UndoStore(path);
        var a = Batch("A");
        store.Add(a);
        var bad = Batch("B", folders: new FolderSnapshot?[] { Snapshot(@"C:\x"), null }!);

        Assert.Throws<ArgumentException>(() => store.Add(bad));

        AssertSameBatches([a], store.GetAll());
        AssertSameBatches([a], new UndoStore(path).GetAll());
    }

    [Fact]
    public void U9b_NullFoldersListThrowsArgumentNullAndStoreUnchanged()
    {
        var store = new UndoStore(NewFile());
        var bad = new BatchRecord("B", DateTimeOffset.UtcNow, OperationKind.ApplyIcon, null, null!, false);

        Assert.Throws<ArgumentNullException>(() => store.Add(bad));

        Assert.Empty(store.GetAll());
    }

    // U10
    [Fact]
    public void U10_FileReflectsStateAfterEachCall()
    {
        var path = NewFile();
        var store = new UndoStore(path);

        store.Add(Batch("A"));
        store.Add(Batch("B"));
        AssertSameBatches(store.GetAll(), new UndoStore(path).GetAll());

        store.MarkReverted("B");
        AssertSameBatches(store.GetAll(), new UndoStore(path).GetAll());
    }

    [Fact]
    public void U10_NoTemporaryFilesLeftBehindWithMainFileIntact()
    {
        var path = NewFile();
        var store = new UndoStore(path);
        for (var i = 0; i < 5; i++)
        {
            store.Add(Batch("B" + i));
        }

        Assert.True(File.Exists(path));
        Assert.True(new FileInfo(path).Length > 0);
        Assert.Equal(5, new UndoStore(path).GetAll().Count);
    }

    [Fact]
    public void U10_FailedAddDoesNotDamageMainFile()
    {
        var path = NewFile();
        var store = new UndoStore(path);
        store.Add(Batch("A"));
        var before = File.ReadAllBytes(path);

        Assert.Throws<ArgumentException>(() => store.Add(Batch("A")));

        Assert.Equal(before, File.ReadAllBytes(path));
    }

    // U11
    [Fact]
    public void U11_ConcurrentAddsLoseNoBatch()
    {
        var path = NewFile();
        var store = new UndoStore(path);
        const int count = 40;

        Parallel.For(0, count, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i => store.Add(Batch("C" + i)));

        var ids = store.GetAll().Select(b => b.BatchId).ToHashSet();
        Assert.Equal(count, ids.Count);
        for (var i = 0; i < count; i++)
        {
            Assert.Contains("C" + i, ids);
        }
        Assert.Equal(count, new UndoStore(path).GetAll().Count);
    }

    [Fact]
    public void U11_ConcurrentMixedOperationsDoNotThrow()
    {
        var store = new UndoStore(NewFile());
        for (var i = 0; i < 10; i++)
        {
            store.Add(Batch("S" + i));
        }

        Parallel.For(0, 10, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
        {
            store.MarkReverted("S" + i);
            _ = store.GetAll();
            _ = store.GetLastPending();
        });

        Assert.All(store.GetAll(), b => Assert.True(b.Reverted));
        Assert.Null(store.GetLastPending());
    }

    // U12
    [Fact]
    public void U12_MutatingPassedListAfterAddDoesNotAffectStore()
    {
        var store = new UndoStore(NewFile());
        var folders = new List<FolderSnapshot> { Snapshot(@"C:\1"), Snapshot(@"C:\2") };
        store.Add(Batch("A", folders: folders));

        folders.Add(Snapshot(@"C:\3"));
        folders.RemoveAt(0);

        var stored = store.GetAll()[0];
        Assert.Equal([@"C:\1", @"C:\2"], stored.Folders.Select(f => f.FolderPath).ToArray());
    }

    [Fact]
    public void U12_MutatingPassedArrayAfterAddDoesNotAffectStore()
    {
        var path = NewFile();
        var store = new UndoStore(path);
        var folders = new[] { Snapshot(@"C:\1"), Snapshot(@"C:\2") };
        store.Add(Batch("A", folders: folders));

        folders[0] = Snapshot(@"C:\changed");

        Assert.Equal(@"C:\1", store.GetLastPending()!.Folders[0].FolderPath);
        Assert.Equal(@"C:\1", new UndoStore(path).GetAll()[0].Folders[0].FolderPath);
    }

    // Acceptance
    [Fact]
    public void Acceptance_LastPendingWalksBackThroughReverts()
    {
        var store = new UndoStore(NewFile());
        store.Add(Batch("A"));
        store.Add(Batch("B"));

        Assert.Equal("B", store.GetLastPending()?.BatchId);
        store.MarkReverted("B");
        Assert.Equal("A", store.GetLastPending()?.BatchId);
        store.MarkReverted("A");
        Assert.Null(store.GetLastPending());
    }

    [Fact]
    public void Acceptance_PersistedBatchEqualsOriginalInNewInstance()
    {
        var path = NewFile();
        var a = Batch("A");
        new UndoStore(path).Add(a);

        AssertSameBatches([a], new UndoStore(path).GetAll());
    }
}
