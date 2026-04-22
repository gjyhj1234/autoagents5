namespace AutoAgents5.Core.Models;

public class AppSettings
{
    public string Owner { get; set; } = string.Empty;
    public string Repo { get; set; } = string.Empty;

    /// <summary>One of: pm, ui, architect, backend, frontend, qa</summary>
    public string Role { get; set; } = "pm";
}
