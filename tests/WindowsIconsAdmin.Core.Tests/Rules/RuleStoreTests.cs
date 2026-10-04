using WindowsIconsAdmin.Core.Rules;

namespace WindowsIconsAdmin.Core.Tests.Rules;

public class RuleStoreTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _rulesFile;

    public RuleStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "wia_rulestore_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _rulesFile = Path.Combine(_tempDir, "rules.json");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
        }
        catch { }
    }

    [Fact]
    public void SaveAll_PersistsAndReloadsRulesInOrder()
    {
        var store = new RuleStore(_rulesFile);
        var rules = new[]
        {
            new FolderRule("r1", "Clients", true, RuleCondition.StartsWith, "Client_", false, @"C:\icons\a.ico", 10, RuleMatchTarget.FolderName),
            new FolderRule("r2", "GitRepos", false, RuleCondition.Wildcard, "*.git*", true, @"C:\icons\b.ico", 20, RuleMatchTarget.FullPath)
        };

        store.SaveAll(rules);

        var reloaded = new RuleStore(_rulesFile).GetAll();
        Assert.Equal(2, reloaded.Count);
        Assert.Equal("r1", reloaded[0].Id);
        Assert.Equal(1, reloaded[0].Priority);
        Assert.Equal(RuleMatchTarget.FolderName, reloaded[0].MatchTarget);

        Assert.Equal("r2", reloaded[1].Id);
        Assert.Equal(2, reloaded[1].Priority);
        Assert.False(reloaded[1].Enabled);
        Assert.Equal(RuleCondition.Wildcard, reloaded[1].Condition);
        Assert.Equal(RuleMatchTarget.FullPath, reloaded[1].MatchTarget);
    }

    [Fact]
    public void Load_WhenCorruptedJson_CreatesBackupAndStartsEmpty()
    {
        File.WriteAllText(_rulesFile, "{ invalid json");

        var store = new RuleStore(_rulesFile);

        Assert.Empty(store.GetAll());
        Assert.True(File.Exists(_rulesFile + ".bak"));
    }
}
