namespace AutoAgents5.App;

static class Program
{
    [STAThread]
    static void Main()
    {
        // Single-instance mutex (per-machine)
        using var mutex = new Mutex(false, "Global\\AutoAgents5");
        if (!mutex.WaitOne(0))
        {
            MessageBox.Show(
                "AutoAgents5 已在运行中（另一个实例正在处理任务）。",
                "重复启动",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        // Detect WebView2 Runtime
        try
        {
            var ver = Microsoft.Web.WebView2.Core.CoreWebView2Environment.GetAvailableBrowserVersionString();
            if (string.IsNullOrEmpty(ver))
                throw new InvalidOperationException("版本为空");
        }
        catch
        {
            var result = MessageBox.Show(
                "未检测到 Microsoft Edge WebView2 Runtime。\n\n" +
                "点击「是」打开下载页面，安装后重新启动本程序。",
                "缺少 WebView2 Runtime",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Error);
            if (result == DialogResult.Yes)
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                    "https://developer.microsoft.com/en-us/microsoft-edge/webview2/") { UseShellExecute = true });
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}