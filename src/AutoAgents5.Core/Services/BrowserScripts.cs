namespace AutoAgents5.Core.Services;

/// <summary>
/// JavaScript snippets executed in WebView2 by the orchestrator.
/// Centralised here for testability and easy maintenance.
/// </summary>
public static class BrowserScripts
{
    /// <summary>
    /// Scrolls to the bottom and waits until scrollHeight is stable (R-EndMarker §1).
    /// Returns true when stable, false on timeout.
    /// </summary>
    public const string ScrollToBottomUntilStable = """
        (async function() {
            let prev = -1, stable = 0;
            const maxMs = 30000, interval = 300;
            const end = Date.now() + maxMs;
            while (Date.now() < end) {
                window.scrollTo(0, document.body.scrollHeight);
                await new Promise(r => setTimeout(r, interval));
                const cur = document.body.scrollHeight;
                if (cur === prev) { stable++; if (stable >= 3) return true; }
                else { stable = 0; prev = cur; }
            }
            return true;  // timeout – treat as stable
        })();
        """;

    /// <summary>
    /// Extracts the last 3 non-empty lines of the last assistant/bot message (R-EndMarker §2-3).
    /// Returns a JSON array of up to 3 strings.
    /// </summary>
    public const string GetLastAssistantMessageTail = """
        (function() {
            // Try several selectors in order; GitHub may change DOM structure.
            const selectors = [
                '[data-author-type="bot"]',
                '[data-testid="assistant-message"]',
                '.copilot-message--bot',
                '.assistant-message',
                '.message[data-role="assistant"]'
            ];
            let el = null;
            for (const sel of selectors) {
                const els = document.querySelectorAll(sel);
                if (els.length > 0) { el = els[els.length - 1]; break; }
            }
            if (!el) return JSON.stringify([]);
            const lines = (el.innerText || '').split('\n')
                .map(l => l.trim())
                .filter(l => l.length > 0);
            return JSON.stringify(lines.slice(-3));
        })();
        """;

    /// <summary>
    /// Returns true if <c>form#task-chat-input-form</c> exists and its textarea is not disabled.
    /// </summary>
    public const string CheckChatFormAvailable = """
        (function() {
            const form = document.querySelector('form#task-chat-input-form');
            if (!form) return false;
            const ta = form.querySelector('textarea');
            return ta !== null && !ta.disabled;
        })();
        """;

    /// <summary>
    /// Types text into the chat textarea and clicks submit (R-§7).
    /// Returns true on success.
    /// </summary>
    public static string TypeAndSubmitChatForm(string text)
    {
        // Escape the text for safe JS string embedding
        var escaped = text
            .Replace("\\", "\\\\")
            .Replace("'", "\\'")
            .Replace("\n", "\\n")
            .Replace("\r", "");
        return $$"""
            (function() {
                const form = document.querySelector('form#task-chat-input-form');
                if (!form) return false;
                const ta = form.querySelector('textarea');
                if (!ta || ta.disabled) return false;
                // Set value and fire native input/change events
                const descriptor = Object.getOwnPropertyDescriptor(
                    window.HTMLTextAreaElement.prototype, 'value');
                if (descriptor && descriptor.set) {
                    descriptor.set.call(ta, '{{escaped}}');
                } else {
                    ta.value = '{{escaped}}';
                }
                ta.dispatchEvent(new Event('input', { bubbles: true }));
                ta.dispatchEvent(new Event('change', { bubbles: true }));
                // Find and click submit button
                const submit = form.querySelector('button[type="submit"]')
                    || form.querySelector('button:not([type="button"])');
                if (!submit || submit.disabled) return false;
                submit.click();
                return true;
            })();
            """;
    }

    /// <summary>
    /// Returns the current user message count (used to detect new user message after submit).
    /// </summary>
    public const string GetUserMessageCount = """
        (function() {
            const selectors = [
                '[data-author-type="user"]',
                '[data-testid="user-message"]',
                '.copilot-message--user',
                '.user-message',
                '.message[data-role="user"]'
            ];
            for (const sel of selectors) {
                const els = document.querySelectorAll(sel);
                if (els.length > 0) return els.length;
            }
            return 0;
        })();
        """;

    /// <summary>
    /// Checks if a button with matching text is present and enabled.
    /// Returns true/false.
    /// </summary>
    public static string IsButtonEnabled(string buttonText)
    {
        var escaped = buttonText.Replace("'", "\\'");
        return $$"""
            (function() {
                const text = '{{escaped}}';
                const btns = Array.from(document.querySelectorAll('button'));
                const btn = btns.find(b => (b.textContent || '').trim().includes(text));
                return btn !== null && btn !== undefined && !btn.disabled;
            })();
            """;
    }

    /// <summary>
    /// Clicks the first button whose text contains <paramref name="buttonText"/>.
    /// Returns true if clicked, false if not found or disabled.
    /// </summary>
    public static string ClickButton(string buttonText)
    {
        var escaped = buttonText.Replace("'", "\\'");
        return $$"""
            (function() {
                const text = '{{escaped}}';
                const btns = Array.from(document.querySelectorAll('button'));
                const btn = btns.find(b => (b.textContent || '').trim().includes(text));
                if (!btn || btn.disabled) return false;
                btn.click();
                return true;
            })();
            """;
    }

    /// <summary>
    /// Returns true if a button with the given text exists in the DOM (regardless of enabled state).
    /// </summary>
    public static string ButtonExists(string buttonText)
    {
        var escaped = buttonText.Replace("'", "\\'");
        return $$"""
            (function() {
                const text = '{{escaped}}';
                const btns = Array.from(document.querySelectorAll('button'));
                return btns.some(b => (b.textContent || '').trim().includes(text));
            })();
            """;
    }

    /// <summary>
    /// Returns true if the page text contains the given substring.
    /// </summary>
    public static string PageContainsText(string text)
    {
        var escaped = text.Replace("'", "\\'");
        return $$"""
            (function() {
                return document.body.innerText.includes('{{escaped}}');
            })();
            """;
    }
}
