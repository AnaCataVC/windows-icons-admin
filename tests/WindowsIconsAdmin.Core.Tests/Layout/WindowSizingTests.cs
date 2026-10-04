using WindowsIconsAdmin.Core.Layout;
using Xunit;

namespace WindowsIconsAdmin.Core.Tests.Layout;

public class WindowSizingTests
{
    private static readonly SizePx Desired = new(1160, 720);
    private static readonly SizePx Min = new(800, 520);

    private static SizePx Compute(SizePx desired, SizePx min, SizePx work, double scale = 1.0)
        => WindowSizing.ComputeInitialSize(desired, min, work, scale);

    // ---- Acceptance criteria ----

    [Fact]
    public void Ac1_FitsWithinWorkArea_ReturnsDesired()
        => Assert.Equal(new SizePx(1160, 720), Compute(Desired, Min, new SizePx(1920, 1040)));

    [Fact]
    public void Ac2_ScaledDesiredCappedAt90Percent()
        => Assert.Equal(new SizePx(1728, 972), Compute(Desired, Min, new SizePx(1920, 1080), 1.5));

    [Fact]
    public void Ac3_SmallWorkArea_HeightCappedWidthFits()
        => Assert.Equal(new SizePx(1160, 655), Compute(Desired, Min, new SizePx(1366, 728)));

    [Fact]
    public void Ac4_WorkAreaSmallerThanMinimum_NinetyPercentWins()
        => Assert.Equal(new SizePx(540, 360), Compute(Desired, Min, new SizePx(600, 400)));

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Ac5_InvalidScale_BehavesLikeScaleOne(double scale)
    {
        var work = new SizePx(1920, 1040);
        Assert.Equal(Compute(Desired, Min, work, 1.0), Compute(Desired, Min, work, scale));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-3.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidScale_FallbackAlsoAppliesWhenCapping(double scale)
    {
        var work = new SizePx(1366, 728);
        Assert.Equal(Compute(Desired, Min, work, 1.0), Compute(Desired, Min, work, scale));
    }

    // ---- Scaling and rounding ----

    [Fact]
    public void Scale_MultipliesDesired()
        => Assert.Equal(new SizePx(2000, 1000),
            Compute(new SizePx(1000, 500), new SizePx(10, 10), new SizePx(10000, 10000), 2.0));

    [Theory]
    [InlineData(101, 152)]
    [InlineData(3, 5)]
    [InlineData(1, 2)]
    public void Rounding_HalfRoundsAwayFromZero_Desired(int desiredDim, int expected)
    {
        var r = Compute(new SizePx(desiredDim, desiredDim), new SizePx(1, 1), new SizePx(10000, 10000), 1.5);
        Assert.Equal(new SizePx(expected, expected), r);
    }

    [Fact]
    public void Rounding_HalfRoundsAwayFromZero_Minimum()
    {
        var r = Compute(new SizePx(1, 1), new SizePx(101, 101), new SizePx(10000, 10000), 1.5);
        Assert.Equal(new SizePx(152, 152), r);
    }

    [Fact]
    public void Rounding_BelowHalfRoundsDown()
    {
        var r = Compute(new SizePx(10, 10), new SizePx(1, 1), new SizePx(10000, 10000), 1.04);
        Assert.Equal(new SizePx(10, 10), r);
    }

    [Fact]
    public void Rounding_AboveHalfRoundsUp()
    {
        var r = Compute(new SizePx(10, 10), new SizePx(1, 1), new SizePx(10000, 10000), 1.06);
        Assert.Equal(new SizePx(11, 11), r);
    }

    // ---- 90% cap ----

    [Theory]
    [InlineData(1000, 900)]
    [InlineData(1366, 1229)]
    [InlineData(728, 655)]
    [InlineData(19, 17)]
    [InlineData(10, 9)]
    public void Cap_IsNinetyPercentRoundedDown(int work, int expectedCap)
    {
        var r = Compute(new SizePx(100000, 100000), new SizePx(1, 1), new SizePx(work, work));
        Assert.Equal(new SizePx(expectedCap, expectedCap), r);
    }

    [Fact]
    public void Desired_ExactlyAtCap_IsKept()
        => Assert.Equal(new SizePx(900, 900),
            Compute(new SizePx(900, 900), new SizePx(1, 1), new SizePx(1000, 1000)));

    [Fact]
    public void Desired_OneAboveCap_IsCapped()
        => Assert.Equal(new SizePx(900, 900),
            Compute(new SizePx(901, 901), new SizePx(1, 1), new SizePx(1000, 1000)));

    [Fact]
    public void Desired_OneBelowCap_IsKept()
        => Assert.Equal(new SizePx(899, 899),
            Compute(new SizePx(899, 899), new SizePx(1, 1), new SizePx(1000, 1000)));

    // ---- Minimum vs cap precedence ----

    [Fact]
    public void Minimum_RaisesDesiredWhenBelowMinimumAndCapAllows()
        => Assert.Equal(new SizePx(800, 520),
            Compute(new SizePx(100, 100), Min, new SizePx(2000, 2000)));

    [Fact]
    public void Minimum_ScaledAndAppliedAsFloor()
        => Assert.Equal(new SizePx(1200, 780),
            Compute(new SizePx(100, 100), Min, new SizePx(4000, 4000), 1.5));

    [Fact]
    public void Minimum_ExceedingCap_CapWins()
        => Assert.Equal(new SizePx(900, 900),
            Compute(new SizePx(100, 100), new SizePx(1000, 1000), new SizePx(1000, 1000)));

    [Fact]
    public void Minimum_EqualToCap_ResultEqualsCap()
        => Assert.Equal(new SizePx(900, 900),
            Compute(new SizePx(100, 100), new SizePx(900, 900), new SizePx(1000, 1000)));

    [Fact]
    public void Minimum_ScaledPastCap_CapWins()
        => Assert.Equal(new SizePx(900, 900),
            Compute(new SizePx(100, 100), new SizePx(700, 700), new SizePx(1000, 1000), 2.0));

    // ---- Axis independence ----

    [Fact]
    public void Axes_WidthCappedHeightNot()
        => Assert.Equal(new SizePx(900, 300),
            Compute(new SizePx(5000, 300), new SizePx(1, 1), new SizePx(1000, 1000)));

    [Fact]
    public void Axes_HeightCappedWidthNot()
        => Assert.Equal(new SizePx(300, 900),
            Compute(new SizePx(300, 5000), new SizePx(1, 1), new SizePx(1000, 1000)));

    [Fact]
    public void Axes_MinimumRaisesOnlyOneAxis()
        => Assert.Equal(new SizePx(800, 600),
            Compute(new SizePx(100, 600), new SizePx(800, 500), new SizePx(3000, 3000)));

    [Fact]
    public void Axes_MinimumExceedsCapOnOneAxisOnly()
        => Assert.Equal(new SizePx(800, 360),
            Compute(new SizePx(100, 100), new SizePx(800, 520), new SizePx(2000, 400)));

    // ---- Invalid arguments ----

    public static IEnumerable<object[]> InvalidSizes => new[]
    {
        new object[] { new SizePx(0, 10) },
        new object[] { new SizePx(10, 0) },
        new object[] { new SizePx(0, 0) },
        new object[] { new SizePx(-1, 10) },
        new object[] { new SizePx(10, -1) },
        new object[] { new SizePx(int.MinValue, 10) },
    };

    [Theory]
    [MemberData(nameof(InvalidSizes))]
    public void InvalidDesired_Throws(SizePx bad)
        => Assert.Throws<ArgumentOutOfRangeException>(() => Compute(bad, Min, new SizePx(1920, 1080)));

    [Theory]
    [MemberData(nameof(InvalidSizes))]
    public void InvalidMinimum_Throws(SizePx bad)
        => Assert.Throws<ArgumentOutOfRangeException>(() => Compute(Desired, bad, new SizePx(1920, 1080)));

    [Theory]
    [MemberData(nameof(InvalidSizes))]
    public void InvalidWorkArea_Throws(SizePx bad)
        => Assert.Throws<ArgumentOutOfRangeException>(() => Compute(Desired, Min, bad));

    [Fact]
    public void InvalidArguments_ThrowEvenWithInvalidScale()
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => Compute(new SizePx(0, 10), Min, new SizePx(1920, 1080), double.NaN));

    // ---- Result invariants ----

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(2, 100)]
    [InlineData(3, 3)]
    public void Result_IsAtLeastOne_ForTinyWorkAreas(int w, int h)
    {
        var r = Compute(Desired, Min, new SizePx(w, h));
        Assert.True(r.Width >= 1);
        Assert.True(r.Height >= 1);
    }

    [Fact]
    public void Result_IsAtLeastOne_ForTinyDesiredAndMinimum()
    {
        var r = Compute(new SizePx(1, 1), new SizePx(1, 1), new SizePx(1000, 1000), 0.01);
        Assert.True(r.Width >= 1);
        Assert.True(r.Height >= 1);
    }

    [Theory]
    [InlineData(1920, 1080, 1.0)]
    [InlineData(1366, 728, 1.25)]
    [InlineData(600, 400, 2.0)]
    [InlineData(3840, 2160, 3.0)]
    [InlineData(100, 100, 8.0)]
    public void Result_NeverExceedsNinetyPercentOfWorkArea(int w, int h, double scale)
    {
        var r = Compute(Desired, Min, new SizePx(w, h), scale);
        Assert.True(r.Width <= (w * 9) / 10);
        Assert.True(r.Height <= (h * 9) / 10);
    }

    // ---- Overflow safety ----

    [Fact]
    public void Overflow_MaxDimensionsAtMaxScale_DoesNotThrowAndIsCapped()
    {
        var max = new SizePx(16384, 16384);
        Assert.Equal(new SizePx(14745, 14745), Compute(max, max, max, 8.0));
    }

    [Fact]
    public void Overflow_MaxDesiredSmallWorkArea_IsCapped()
        => Assert.Equal(new SizePx(900, 900),
            Compute(new SizePx(16384, 16384), new SizePx(16384, 16384), new SizePx(1000, 1000), 8.0));

    [Fact]
    public void Overflow_MaxMinimumAtMaxScale_NeverNegative()
        => Assert.Equal(new SizePx(14745, 14745),
            Compute(new SizePx(1, 1), new SizePx(16384, 16384), new SizePx(16384, 16384), 8.0));

    // ---- Purity and thread-safety ----

    [Fact]
    public void Repeated_Calls_ReturnSameResult()
    {
        var work = new SizePx(1920, 1080);
        var first = Compute(Desired, Min, work, 1.5);
        for (var i = 0; i < 100; i++)
        {
            Assert.Equal(first, Compute(Desired, Min, work, 1.5));
        }
    }

    [Fact]
    public void Concurrent_Calls_ReturnConsistentResults()
    {
        var work = new SizePx(1920, 1080);
        var expected = Compute(Desired, Min, work, 1.5);
        var results = new SizePx[64];
        Parallel.For(0, results.Length, i => results[i] = Compute(Desired, Min, work, 1.5));
        Assert.All(results, r => Assert.Equal(expected, r));
    }
}
