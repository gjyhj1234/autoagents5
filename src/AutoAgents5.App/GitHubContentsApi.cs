using System.Net.Http;
using System.Text.Json;

namespace AutoAgents5.App;

/// <summary>
/// Reads task file lists from GitHub via the Contents API (unauthenticated, public repos).
/// For private repos, uses the session cookie from WebView2 (not needed here as we call raw API).
/// </summary>
internal static class GitHubContentsApi
{
    private static readonly HttpClient _http = new();

    static GitHubContentsApi()
    {
        _http.DefaultRequestHeaders.Add("User-Agent", "AutoAgents5/1.0");
        _http.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
    }

    /// <summary>
    /// Returns sorted list of .md file names in .ai/tasks/{role}/ready.
    /// Returns empty list on any error.
    /// </summary>
    public static async Task<List<string>> GetReadyTasksAsync(
        string owner, string repo, string role, string? githubToken = null)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/contents/.ai/tasks/{role}/ready";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(githubToken))
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", githubToken);

            using var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return new List<string>();

            var json = await resp.Content.ReadAsStringAsync();
            var entries = JsonSerializer.Deserialize<List<ContentEntry>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return entries?
                .Where(e => e.Type == "file" && e.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Name)
                .OrderBy(n => n)
                .ToList() ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private class ContentEntry
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
}
