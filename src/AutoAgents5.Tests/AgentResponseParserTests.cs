using AutoAgents5.Core.Services;
using Xunit;

namespace AutoAgents5.Tests;

public class AgentResponseParserTests
{
    private const string ValidTaskListJson = """
        {
          "tasks": [
            {
              "state": "queued",
              "html_url": "https://github.com/owner/repo/tasks/abc-123",
              "created_at": "2026-04-22T01:00:00Z",
              "unknown_field": "should be ignored"
            },
            {
              "state": "completed",
              "html_url": "https://github.com/owner/repo/tasks/def-456",
              "created_at": "2026-04-22T02:00:00Z"
            }
          ],
          "total_active_count": 2
        }
        """;

    [Fact]
    public void ParseTaskList_returns_tasks_ignoring_unknown_fields()
    {
        var (tasks, error) = AgentResponseParser.ParseTaskList(ValidTaskListJson);
        Assert.Null(error);
        Assert.Equal(2, tasks.Count);
        Assert.Equal("queued", tasks[0].State);
        Assert.Equal("abc-123", tasks[0].TaskId);
        Assert.Equal("completed", tasks[1].State);
    }

    [Fact]
    public void ParseTaskList_returns_error_on_invalid_json()
    {
        var (tasks, error) = AgentResponseParser.ParseTaskList("{ invalid json }");
        Assert.NotNull(error);
        Assert.Empty(tasks);
    }

    [Fact]
    public void ParseDiff_returns_files()
    {
        const string json = """
            {
              "files": [
                { "path": ".ai/workplace/session_pm_20260422_100000.md", "status": "A" },
                { "path": "simulate_agent_error.py", "status": "M" }
              ],
              "additions": 10,
              "deletions": 0
            }
            """;
        var (result, error) = AgentResponseParser.ParseDiff(json);
        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal(2, result!.Files.Count);
        Assert.Equal(".ai/workplace/session_pm_20260422_100000.md", result.Files[0].Path);
    }

    [Fact]
    public void ParseDiff_returns_error_on_invalid_json()
    {
        var (result, error) = AgentResponseParser.ParseDiff("not json");
        Assert.NotNull(error);
        Assert.Null(result);
    }
}
