using System.Text.RegularExpressions;

namespace AutoAgents5.Core.Services;

/// <summary>
/// URL pattern matching for XHR/Fetch interception (R-URL-Match).
/// </summary>
public class UrlMatcher
{
    private readonly Regex _tasksRegex;
    private readonly string _owner;
    private readonly string _repo;

    public UrlMatcher(string owner, string repo)
    {
        _owner = owner;
        _repo = repo;

        var escapedOwner = Regex.Escape(owner);
        var escapedRepo = Regex.Escape(repo);

        // Matches: /copilot/api/agents/repos/{owner}/{repo}/tasks
        //       or /copilot/api/internal/agents/repos/{owner}/{repo}/tasks
        _tasksRegex = new Regex(
            $@"^/copilot/api(/internal)?/agents/repos/{escapedOwner}/{escapedRepo}/tasks$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    /// <summary>Returns true if the URL is a tasks-list API call we should intercept.</summary>
    public bool IsTasksUrl(Uri uri)
    {
        return _tasksRegex.IsMatch(uri.AbsolutePath);
    }

    /// <summary>
    /// Returns true if the URL is a diff API call for the specified task.
    /// Conditions (all must be true):
    ///   1. pathname ends with /diff
    ///   2. pathname contains /tasks/{taskId}/
    ///   3. query has base=main
    /// </summary>
    public static bool IsDiffUrl(Uri uri, string taskId)
    {
        if (string.IsNullOrEmpty(taskId)) return false;

        var path = uri.AbsolutePath;
        if (!path.EndsWith("/diff", StringComparison.OrdinalIgnoreCase)) return false;
        if (!path.Contains($"/tasks/{taskId}/", StringComparison.OrdinalIgnoreCase)) return false;

        var query = ParseQuery(uri.Query);
        query.TryGetValue("base", out var baseVal);
        return string.Equals(baseVal, "main", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a file path matches the session file pattern:
    ///   .ai/workplace/session_{role}_{yyyyMMdd}_{HHmmss}.md
    /// </summary>
    public static bool IsSessionFile(string filePath)
    {
        return Regex.IsMatch(filePath,
            @"^\.ai/workplace/session_(pm|ui|architect|backend|frontend|qa)_\d{8}_\d{6}\.md$",
            RegexOptions.IgnoreCase);
    }

    /// <summary>Simple query-string parser (avoids System.Web dependency).</summary>
    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(query)) return result;
        var q = query.TrimStart('?');
        foreach (var pair in q.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = pair.IndexOf('=');
            if (idx < 0) result[Uri.UnescapeDataString(pair)] = string.Empty;
            else result[Uri.UnescapeDataString(pair[..idx])] = Uri.UnescapeDataString(pair[(idx + 1)..]);
        }
        return result;
    }
}
