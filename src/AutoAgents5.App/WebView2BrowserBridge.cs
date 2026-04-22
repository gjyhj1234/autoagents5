using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using AutoAgents5.Core.Models;
using AutoAgents5.Core.Services;
using System.IO;

namespace AutoAgents5.App;

/// <summary>
/// WebView2-backed implementation of IBrowserBridge.
/// Must be used from the UI thread.
/// </summary>
internal class WebView2BrowserBridge : IBrowserBridge
{
    private readonly WebView2 _webView;
    private TaskCompletionSource<bool>? _navigationTcs;

    public event Action<string, string>? XhrReceived;

    public string CurrentUrl => _webView.Source?.ToString() ?? string.Empty;

    public WebView2BrowserBridge(WebView2 webView)
    {
        _webView = webView;
    }

    /// <summary>Initializes WebView2 with persistent UserDataFolder and registers XHR listener.</summary>
    public async Task InitializeAsync()
    {
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutoAgents5", "WebView2");

        var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
        await _webView.EnsureCoreWebView2Async(env);

        // Navigation completed
        _webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;

        // XHR interception: register response filter for GitHub API calls
        _webView.CoreWebView2.AddWebResourceRequestedFilter("https://github.com/*", CoreWebView2WebResourceContext.XmlHttpRequest);
        _webView.CoreWebView2.AddWebResourceRequestedFilter("https://api.github.com/*", CoreWebView2WebResourceContext.XmlHttpRequest);
        _webView.CoreWebView2.WebResourceResponseReceived += CoreWebView2_WebResourceResponseReceived;
    }

    public async Task NavigateAsync(string url, CancellationToken ct)
    {
        _navigationTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ct.Register(() => _navigationTcs.TrySetCanceled());

        _webView.CoreWebView2.Navigate(url);

        // Wait for navigation to complete (or be cancelled)
        await _navigationTcs.Task;
    }

    public async Task<string> ExecuteScriptAsync(string script)
    {
        var result = await _webView.CoreWebView2.ExecuteScriptAsync(script);
        // WebView2 returns JSON-encoded strings; unwrap if it's a quoted string
        if (result.StartsWith('"') && result.EndsWith('"'))
            result = System.Text.Json.JsonSerializer.Deserialize<string>(result) ?? result;
        return result;
    }

    private void CoreWebView2_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        _navigationTcs?.TrySetResult(e.IsSuccess);
    }

    private async void CoreWebView2_WebResourceResponseReceived(
        object? sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
    {
        try
        {
            var url = e.Request.Uri;
            var stream = await e.Response.GetContentAsync();
            using var reader = new System.IO.StreamReader(stream);
            var body = await reader.ReadToEndAsync();

            if (!string.IsNullOrWhiteSpace(body))
                XhrReceived?.Invoke(url, body);
        }
        catch
        {
            // Best-effort; ignore read errors on individual responses
        }
    }
}
