using AutoAgents5.Core.Models;

namespace AutoAgents5.Core.Services;

/// <summary>
/// Abstracts WebView2 browser operations for testability.
/// All methods must be called from the UI thread unless noted.
/// </summary>
public interface IBrowserBridge
{
    /// <summary>Navigate to URL and await NavigationCompleted (IsSuccess==true).</summary>
    Task NavigateAsync(string url, CancellationToken ct);

    /// <summary>Current page URL.</summary>
    string CurrentUrl { get; }

    /// <summary>Execute JavaScript and return result as string.</summary>
    Task<string> ExecuteScriptAsync(string script);

    /// <summary>
    /// Subscribe to XHR/Fetch response. Callback receives (url, responseBodyJson).
    /// Only one global handler; set once at startup.
    /// </summary>
    event Action<string, string> XhrReceived;
}
