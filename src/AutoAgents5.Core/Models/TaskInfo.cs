using System.Text.Json.Serialization;

namespace AutoAgents5.Core.Models;

/// <summary>
/// Represents the fields we care about from a Copilot Agents task response.
/// Other fields are intentionally ignored (per R-URL-Match).
/// </summary>
public class TaskInfo
{
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;

    /// <summary>Last path segment of HtmlUrl, e.g. "c0de5caf-1f2e-4745-8560-bc56f550dafb"</summary>
    public string TaskId => HtmlUrl.Split('/').LastOrDefault() ?? string.Empty;
}
