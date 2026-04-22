using AutoAgents5.Core.Models;
using AutoAgents5.Core.Services;
using Xunit;

namespace AutoAgents5.Tests;

public class EndMarkerCheckerTests
{
    [Fact]
    public void AllDone_marker_returns_AllDone()
    {
        var lines = new[] { "some output", "more output", "===任务全部完成===" };
        Assert.Equal(EndMarkerResult.AllDone, EndMarkerChecker.Evaluate(lines));
    }

    [Fact]
    public void Partial_marker_returns_Continue()
    {
        var lines = new[] { "output", "===部分任务未完成===", "" };
        Assert.Equal(EndMarkerResult.Continue, EndMarkerChecker.Evaluate(
            lines.Where(l => l.Trim() != string.Empty).ToList()));
    }

    [Fact]
    public void Error_marker_returns_Continue()
    {
        var lines = new[] { "===任务执行出现错误===" };
        Assert.Equal(EndMarkerResult.Continue, EndMarkerChecker.Evaluate(lines));
    }

    [Fact]
    public void No_marker_returns_NotFound()
    {
        var lines = new[] { "line1", "line2", "line3" };
        Assert.Equal(EndMarkerResult.NotFound, EndMarkerChecker.Evaluate(lines));
    }

    [Fact]
    public void Empty_lines_returns_NotFound()
    {
        Assert.Equal(EndMarkerResult.NotFound, EndMarkerChecker.Evaluate(Array.Empty<string>()));
    }

    [Fact]
    public void Last_marker_wins_when_multiple_present()
    {
        // AllDone then Continue → Continue wins (last)
        var lines = new[] { "===任务全部完成===", "===部分任务未完成===", "extra" };
        Assert.Equal(EndMarkerResult.Continue, EndMarkerChecker.Evaluate(lines));
    }

    [Fact]
    public void Last_marker_wins_AllDone_at_end()
    {
        // Continue then AllDone → AllDone wins (last)
        var lines = new[] { "===部分任务未完成===", "===任务全部完成===", "other" };
        // "other" has no marker; the last marker was AllDone at index 1 → AllDone
        Assert.Equal(EndMarkerResult.AllDone, EndMarkerChecker.Evaluate(lines));
    }
}
