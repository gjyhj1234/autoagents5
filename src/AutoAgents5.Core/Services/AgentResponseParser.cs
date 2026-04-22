using System.Text.Json;
using System.Text.Json.Serialization;
using AutoAgents5.Core.Models;

namespace AutoAgents5.Core.Services;

/// <summary>
/// Parses GitHub Agents API responses (R-URL-Match - tolerates unknown fields).
/// </summary>
public static class AgentResponseParser
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
    };

    /// <summary>
    /// Parses task list JSON. Returns empty list on any parse error (logs first 500 chars).
    /// </summary>
    public static (List<TaskInfo> Tasks, string? Error) ParseTaskList(string json)
    {
        try
        {
            var wrapper = JsonSerializer.Deserialize<TaskListWrapper>(json, _options);
            return (wrapper?.Tasks ?? new List<TaskInfo>(), null);
        }
        catch (Exception ex)
        {
            var snippet = json.Length > 500 ? json[..500] : json;
            return (new List<TaskInfo>(), $"Parse error: {ex.Message}. Snippet: {snippet}");
        }
    }

    /// <summary>
    /// Parses diff JSON. Returns null result on any parse error.
    /// </summary>
    public static (DiffResult? Result, string? Error) ParseDiff(string json)
    {
        try
        {
            var result = JsonSerializer.Deserialize<DiffResult>(json, _options);
            return (result, null);
        }
        catch (Exception ex)
        {
            var snippet = json.Length > 500 ? json[..500] : json;
            return (null, $"Parse error: {ex.Message}. Snippet: {snippet}");
        }
    }

    private class TaskListWrapper
    {
        [JsonPropertyName("tasks")]
        public List<TaskInfo> Tasks { get; set; } = new();
    }
}
