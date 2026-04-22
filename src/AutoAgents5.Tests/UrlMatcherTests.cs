using AutoAgents5.Core.Services;
using Xunit;

namespace AutoAgents5.Tests;

public class UrlMatcherTests
{
    private readonly UrlMatcher _matcher = new("gjyhj1234", "autoagents5");

    // ── IsTasksUrl ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("https://github.com/copilot/api/agents/repos/gjyhj1234/autoagents5/tasks?creator_id=123&page=1")]
    [InlineData("https://github.com/copilot/api/internal/agents/repos/gjyhj1234/autoagents5/tasks?page=1")]
    public void IsTasksUrl_matches_valid_tasks_urls(string url)
    {
        Assert.True(_matcher.IsTasksUrl(new Uri(url)));
    }

    [Theory]
    [InlineData("https://github.com/copilot/api/agents/repos/other/autoagents5/tasks")]
    [InlineData("https://github.com/copilot/api/agents/repos/gjyhj1234/other/tasks")]
    [InlineData("https://github.com/copilot/api/agents/repos/gjyhj1234/autoagents5/tasks/c0de5caf/diff")]
    [InlineData("https://api.github.com/repos/gjyhj1234/autoagents5/contents/")]
    public void IsTasksUrl_rejects_non_tasks_urls(string url)
    {
        Assert.False(_matcher.IsTasksUrl(new Uri(url)));
    }

    // ── IsDiffUrl ───────────────────────────────────────────────────────────

    [Fact]
    public void IsDiffUrl_matches_valid_diff_url()
    {
        var url = new Uri("https://github.com/copilot/api/agents/repos/gjyhj1234/autoagents5/tasks/c0de5caf/diff?base=main&other=xyz");
        Assert.True(UrlMatcher.IsDiffUrl(url, "c0de5caf"));
    }

    [Fact]
    public void IsDiffUrl_rejects_wrong_base()
    {
        var url = new Uri("https://github.com/copilot/api/agents/repos/gjyhj1234/autoagents5/tasks/c0de5caf/diff?base=develop");
        Assert.False(UrlMatcher.IsDiffUrl(url, "c0de5caf"));
    }

    [Fact]
    public void IsDiffUrl_rejects_wrong_taskId()
    {
        var url = new Uri("https://github.com/copilot/api/agents/repos/gjyhj1234/autoagents5/tasks/c0de5caf/diff?base=main");
        Assert.False(UrlMatcher.IsDiffUrl(url, "different-id"));
    }

    [Fact]
    public void IsDiffUrl_rejects_no_diff_suffix()
    {
        var url = new Uri("https://github.com/copilot/api/agents/repos/gjyhj1234/autoagents5/tasks/c0de5caf/sessions?base=main");
        Assert.False(UrlMatcher.IsDiffUrl(url, "c0de5caf"));
    }

    // ── IsSessionFile ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(".ai/workplace/session_pm_20260422_100000.md")]
    [InlineData(".ai/workplace/session_ui_20260101_235959.md")]
    [InlineData(".ai/workplace/session_architect_20260422_000000.md")]
    [InlineData(".ai/workplace/session_backend_20260422_100000.md")]
    [InlineData(".ai/workplace/session_frontend_20260422_100000.md")]
    [InlineData(".ai/workplace/session_qa_20260422_100000.md")]
    public void IsSessionFile_matches_valid_paths(string path)
    {
        Assert.True(UrlMatcher.IsSessionFile(path));
    }

    [Theory]
    [InlineData(".ai/workplace/session_dev_20260422_100000.md")]   // unknown role
    [InlineData(".ai/workplace/session_pm_2026042_100000.md")]     // wrong date length
    [InlineData("simulate_agent_error.py")]
    [InlineData(".ai/workplace/session_pm_20260422_100000.txt")]   // wrong extension
    public void IsSessionFile_rejects_invalid_paths(string path)
    {
        Assert.False(UrlMatcher.IsSessionFile(path));
    }
}
