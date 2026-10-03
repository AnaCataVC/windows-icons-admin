using System.Diagnostics;
using WindowsIconsAdmin.Core.Rules;

namespace WindowsIconsAdmin.Core.Tests.Rules;

public class RuleEngineTests
{
    private static FolderRule Rule(
        string id,
        RuleCondition condition,
        string pattern,
        bool caseSensitive = false,
        bool enabled = true,
        int priority = 0)
        => new(id, "name-" + id, enabled, condition, pattern, caseSensitive, "icon-" + id + ".ico", priority);

    private static string? MatchedId(string folder, params FolderRule[] rules)
    {
        var result = RuleEngine.Evaluate([folder], rules);
        Assert.Single(result);
        return result[0].Rule?.Id;
    }

    // R1
    [Fact]
    public void R1_ReturnsOneResultPerPathInOrderWithOriginalPath()
    {
        var paths = new[] { @"C:\a\One\", "C:/b/Two", "Three" };
        var result = RuleEngine.Evaluate(paths, [Rule("r", RuleCondition.Contains, "o")]);

        Assert.Equal(3, result.Count);
        Assert.Equal(paths, result.Select(r => r.FolderPath).ToArray());
    }

    [Fact]
    public void R1_DuplicatePathsYieldDuplicateResults()
    {
        var result = RuleEngine.Evaluate([@"C:\x\foo", @"C:\x\foo"], [Rule("r", RuleCondition.Contains, "foo")]);

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal("r", r.Rule?.Id));
    }

    // R2
    [Theory]
    [InlineData(@"C:\Work\proyecto-1\")]
    [InlineData(@"C:\Work\proyecto-1")]
    [InlineData("C:/Work/proyecto-1/")]
    [InlineData("C:/Work/proyecto-1")]
    [InlineData(@"C:\Work\proyecto-1\\")]
    [InlineData(@"C:\Work\proyecto-1/\")]
    public void R2_ExtractsFinalSegmentAfterTrimmingSeparators(string path)
    {
        Assert.Equal("r", MatchedId(path, Rule("r", RuleCondition.Regex, "^proyecto-1$")));
    }

    [Fact]
    public void R2_ParentSegmentsAreNeverMatched()
    {
        Assert.Null(MatchedId(@"C:\Parent\child", Rule("r", RuleCondition.Contains, "Parent")));
    }

    [Fact]
    public void R2_ParentSegmentsNeverMatchedWithForwardSlash()
    {
        Assert.Null(MatchedId("C:/Parent/child/", Rule("r", RuleCondition.StartsWith, "Parent")));
    }

    [Fact]
    public void R2_PathWithoutSeparatorsIsTheName()
    {
        Assert.Equal("r", MatchedId("justname", Rule("r", RuleCondition.EndsWith, "name")));
    }

    // R3
    [Theory]
    [InlineData(@"C:\")]
    [InlineData("")]
    [InlineData("C:/")]
    [InlineData(@"\")]
    public void R3_EmptyNameNeverMatches_EvenForEmptyRegex(string path)
    {
        Assert.Null(MatchedId(path, Rule("r", RuleCondition.Regex, "")));
    }

    [Fact]
    public void R3_EmptyNameStillProducesResultWithNullRule()
    {
        var result = RuleEngine.Evaluate([""], [Rule("r", RuleCondition.Contains, "a")]);

        var match = Assert.Single(result);
        Assert.Equal("", match.FolderPath);
        Assert.Null(match.Rule);
    }

    // R4
    [Fact]
    public void R4_LowerPriorityValueWinsRegardlessOfInputOrder()
    {
        var low = Rule("low", RuleCondition.Contains, "a", priority: 1);
        var high = Rule("high", RuleCondition.Contains, "a", priority: 2);

        Assert.Equal("low", MatchedId("abc", high, low));
        Assert.Equal("low", MatchedId("abc", low, high));
    }

    [Fact]
    public void R4_EqualPriorityKeepsInputOrder()
    {
        var first = Rule("first", RuleCondition.Contains, "a", priority: 5);
        var second = Rule("second", RuleCondition.Contains, "a", priority: 5);

        Assert.Equal("first", MatchedId("abc", first, second));
        Assert.Equal("second", MatchedId("abc", second, first));
    }

    [Fact]
    public void R4_NegativePriorityComesBeforePositive()
    {
        var neg = Rule("neg", RuleCondition.Contains, "a", priority: -3);
        var zero = Rule("zero", RuleCondition.Contains, "a", priority: 0);

        Assert.Equal("neg", MatchedId("abc", zero, neg));
    }

    [Fact]
    public void R4_FirstMatchingRuleWinsWhenHigherPriorityDoesNotMatch()
    {
        var nonMatching = Rule("no", RuleCondition.Contains, "zzz", priority: 1);
        var matching = Rule("yes", RuleCondition.Contains, "abc", priority: 2);

        Assert.Equal("yes", MatchedId("abc", nonMatching, matching));
    }

    [Fact]
    public void R4_DisabledRuleIsSkippedInFavorOfLowerPriorityEnabledRule()
    {
        var disabled = Rule("off", RuleCondition.Contains, "a", enabled: false, priority: 1);
        var enabled = Rule("on", RuleCondition.Contains, "a", priority: 2);

        Assert.Equal("on", MatchedId("abc", disabled, enabled));
    }

    [Fact]
    public void R4_ReturnedRuleIsTheInputRule()
    {
        var rule = Rule("r", RuleCondition.Contains, "a");

        var result = RuleEngine.Evaluate(["abc"], [rule]);

        Assert.Equal(rule, result[0].Rule);
    }

    // R5
    [Fact]
    public void R5_NoMatchYieldsNullRule()
    {
        Assert.Null(MatchedId(@"C:\x\hello", Rule("r", RuleCondition.Contains, "world")));
    }

    // R6
    [Fact]
    public void R6_Contains_MatchesSubstring()
    {
        Assert.Equal("r", MatchedId(@"C:\x\hello-world", Rule("r", RuleCondition.Contains, "lo-wo")));
        Assert.Null(MatchedId(@"C:\x\hello-world", Rule("r", RuleCondition.Contains, "wo-lo")));
    }

    [Fact]
    public void R6_StartsWith_MatchesPrefixOnly()
    {
        Assert.Equal("r", MatchedId(@"C:\x\hello", Rule("r", RuleCondition.StartsWith, "hel")));
        Assert.Null(MatchedId(@"C:\x\hello", Rule("r", RuleCondition.StartsWith, "llo")));
    }

    [Fact]
    public void R6_EndsWith_MatchesSuffixOnly()
    {
        Assert.Equal("r", MatchedId(@"C:\x\hello", Rule("r", RuleCondition.EndsWith, "llo")));
        Assert.Null(MatchedId(@"C:\x\hello", Rule("r", RuleCondition.EndsWith, "hel")));
    }

    [Fact]
    public void R6_Regex_IsUnanchored()
    {
        Assert.Equal("r", MatchedId(@"C:\x\abc123def", Rule("r", RuleCondition.Regex, @"\d{3}")));
    }

    [Fact]
    public void R6_Regex_HonorsAnchors()
    {
        Assert.Null(MatchedId(@"C:\x\abc123def", Rule("r", RuleCondition.Regex, @"^\d{3}")));
    }

    [Fact]
    public void R6_NonRegexConditionsTreatPatternLiterally()
    {
        Assert.Null(MatchedId(@"C:\x\abc", Rule("r", RuleCondition.Contains, "a.c")));
        Assert.Equal("r", MatchedId(@"C:\x\a.c", Rule("r", RuleCondition.Contains, "a.c")));
    }

    // R7 / R7b
    [Theory]
    [InlineData(RuleCondition.Contains, "ROY")]
    [InlineData(RuleCondition.StartsWith, "PRO")]
    [InlineData(RuleCondition.EndsWith, "ECTO")]
    [InlineData(RuleCondition.Regex, "^PROYECTO$")]
    public void R7_CaseInsensitiveIgnoresAsciiCase(RuleCondition condition, string pattern)
    {
        Assert.Equal("r", MatchedId(@"C:\x\proyecto", Rule("r", condition, pattern, caseSensitive: false)));
    }

    [Theory]
    [InlineData(RuleCondition.Contains, "ROY")]
    [InlineData(RuleCondition.StartsWith, "PRO")]
    [InlineData(RuleCondition.EndsWith, "ECTO")]
    [InlineData(RuleCondition.Regex, "^PROYECTO$")]
    public void R7_CaseSensitiveRequiresExactCase(RuleCondition condition, string pattern)
    {
        Assert.Null(MatchedId(@"C:\x\proyecto", Rule("r", condition, pattern, caseSensitive: true)));
    }

    [Theory]
    [InlineData(RuleCondition.Contains, "roy")]
    [InlineData(RuleCondition.StartsWith, "pro")]
    [InlineData(RuleCondition.EndsWith, "ecto")]
    [InlineData(RuleCondition.Regex, "^proyecto$")]
    public void R7_CaseSensitiveMatchesWhenCaseEqual(RuleCondition condition, string pattern)
    {
        Assert.Equal("r", MatchedId(@"C:\x\proyecto", Rule("r", condition, pattern, caseSensitive: true)));
    }

    [Fact]
    public void R7b_MixedAsciiCaseInsensitive()
    {
        Assert.Equal("r", MatchedId(@"C:\x\MiXeD-CaSe", Rule("r", RuleCondition.Contains, "mixed-case")));
    }

    // R8
    [Theory]
    [InlineData(RuleCondition.Contains)]
    [InlineData(RuleCondition.StartsWith)]
    [InlineData(RuleCondition.EndsWith)]
    public void R8_EmptyPatternNonRegexNeverMatches(RuleCondition condition)
    {
        Assert.Null(MatchedId(@"C:\x\abc", Rule("r", condition, "")));
    }

    [Fact]
    public void R8_EmptyRegexMatchesEveryNonEmptyName()
    {
        Assert.Equal("r", MatchedId(@"C:\x\abc", Rule("r", RuleCondition.Regex, "")));
    }

    [Fact]
    public void R8_EmptyPatternRuleFallsThroughToNextRule()
    {
        var empty = Rule("empty", RuleCondition.Contains, "", priority: 1);
        var real = Rule("real", RuleCondition.Contains, "abc", priority: 2);

        Assert.Equal("real", MatchedId(@"C:\x\abc", empty, real));
    }

    // R9
    [Fact]
    public void R9_InvalidEnabledRegexThrows()
    {
        Assert.Throws<InvalidRuleException>(() =>
            RuleEngine.Evaluate(["abc"], [Rule("bad", RuleCondition.Regex, "(unclosed")]));
    }

    [Fact]
    public void R9_ThrowsEvenWhenEarlierRuleWouldMatch()
    {
        var good = Rule("good", RuleCondition.Contains, "abc", priority: 1);
        var bad = Rule("bad", RuleCondition.Regex, "[", priority: 2);

        Assert.Throws<InvalidRuleException>(() => RuleEngine.Evaluate(["abc"], [good, bad]));
    }

    [Fact]
    public void R9_ThrowsEvenWhenFolderListIsEmpty()
    {
        Assert.Throws<InvalidRuleException>(() =>
            RuleEngine.Evaluate([], [Rule("bad", RuleCondition.Regex, "(")]));
    }

    [Fact]
    public void R9_DisabledInvalidRegexIsIgnored()
    {
        var bad = Rule("bad", RuleCondition.Regex, "(", enabled: false, priority: 1);
        var good = Rule("good", RuleCondition.Contains, "abc", priority: 2);

        Assert.Equal("good", MatchedId("abc", bad, good));
    }

    [Fact]
    public void R9_InvalidPatternInNonRegexRuleIsNotValidated()
    {
        Assert.Equal("r", MatchedId("a(b", Rule("r", RuleCondition.Contains, "(")));
    }

    // R10 / R10b
    [Fact]
    public void R10_CatastrophicRegexIsNoMatchAndEvaluationContinues()
    {
        var name = new string('a', 60) + "!";
        var catastrophic = Rule("cat", RuleCondition.Regex, "^(a+)+$", caseSensitive: true, priority: 1);
        var fallback = Rule("fallback", RuleCondition.Contains, "aaa", priority: 2);

        var sw = Stopwatch.StartNew();
        var result = RuleEngine.Evaluate(["C:\\x\\" + name], [catastrophic, fallback]);
        sw.Stop();

        Assert.Equal("fallback", result[0].Rule?.Id);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void R10b_CatastrophicRegexAloneYieldsNullWithinLooseBound()
    {
        var name = new string('a', 60) + "!";

        var sw = Stopwatch.StartNew();
        var result = RuleEngine.Evaluate(["C:\\x\\" + name], [Rule("cat", RuleCondition.Regex, "^(a+)+$")]);
        sw.Stop();

        Assert.Null(result[0].Rule);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(30));
    }

    // R11
    [Fact]
    public void R11_NullFolderPathsThrows()
    {
        Assert.Throws<ArgumentNullException>(() => RuleEngine.Evaluate(null!, []));
    }

    [Fact]
    public void R11_NullRulesThrows()
    {
        Assert.Throws<ArgumentNullException>(() => RuleEngine.Evaluate(["a"], null!));
    }

    [Fact]
    public void R11_NullPathElementThrowsArgumentException()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            RuleEngine.Evaluate(new string?[] { "a", null }!, [Rule("r", RuleCondition.Contains, "a")]));
    }

    [Fact]
    public void R11_NullRuleElementThrowsArgumentException()
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            RuleEngine.Evaluate(["a"], new FolderRule?[] { Rule("r", RuleCondition.Contains, "a"), null }!));
    }

    // R12
    [Fact]
    public void R12_EmptyFoldersReturnsEmptyList()
    {
        var result = RuleEngine.Evaluate([], [Rule("r", RuleCondition.Contains, "a")]);

        Assert.Empty(result);
    }

    [Fact]
    public void R12_EmptyRulesReturnsAllWithNullRule()
    {
        var result = RuleEngine.Evaluate(["a", "b", "c"], []);

        Assert.Equal(3, result.Count);
        Assert.All(result, r => Assert.Null(r.Rule));
        Assert.Equal(["a", "b", "c"], result.Select(r => r.FolderPath).ToArray());
    }

    // R13
    [Fact]
    public void R13_NonExistentFoldersAreEvaluated()
    {
        Assert.Equal("r", MatchedId(@"Z:\definitely\not\here\Proyecto", Rule("r", RuleCondition.Contains, "proyecto")));
    }

    [Fact]
    public void R13_InputsAreNotMutated()
    {
        var paths = new List<string> { @"C:\b\Beta", @"C:\a\Alpha" };
        var rules = new List<FolderRule>
        {
            Rule("second", RuleCondition.Contains, "a", priority: 2),
            Rule("first", RuleCondition.Contains, "a", priority: 1),
        };
        var pathsCopy = paths.ToList();
        var rulesCopy = rules.ToList();

        RuleEngine.Evaluate(paths, rules);

        Assert.Equal(pathsCopy, paths);
        Assert.Equal(rulesCopy.Select(r => r.Id), rules.Select(r => r.Id));
    }

    // Acceptance
    [Fact]
    public void Acceptance_ContainsCaseInsensitiveMatchesFolder()
    {
        Assert.Equal("r", MatchedId(@"C:\Dev\Mi Proyecto Final", Rule("r", RuleCondition.Contains, "proyecto")));
    }

    [Fact]
    public void Acceptance_BothMatchingReturnsPriorityOne()
    {
        var p1 = Rule("p1", RuleCondition.Contains, "a", priority: 1);
        var p2 = Rule("p2", RuleCondition.Contains, "a", priority: 2);

        Assert.Equal("p1", MatchedId("abc", p2, p1));
    }

    [Fact]
    public void Acceptance_OnlyDisabledMatchingRuleYieldsNull()
    {
        Assert.Null(MatchedId("abc", Rule("r", RuleCondition.Contains, "a", enabled: false)));
    }

    [Fact]
    public void Acceptance_StartsWith2024()
    {
        var rule = Rule("r", RuleCondition.StartsWith, "2024");
        var result = RuleEngine.Evaluate(["2024-fotos", "fotos-2024"], [rule]);

        Assert.Equal("r", result[0].Rule?.Id);
        Assert.Null(result[1].Rule);
    }

    [Fact]
    public void Acceptance_RegexDigitsDash()
    {
        var rule = Rule("r", RuleCondition.Regex, @"^\d{4}-");
        var result = RuleEngine.Evaluate(["2024-x", "x-2024"], [rule]);

        Assert.Equal("r", result[0].Rule?.Id);
        Assert.Null(result[1].Rule);
    }
}
