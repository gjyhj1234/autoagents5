namespace AutoAgents5.Core.Models;

/// <summary>
/// Relevant fields from the diff API response (R-URL-Match).
/// </summary>
public class DiffResult
{
    public List<DiffFile> Files { get; set; } = new();
    public int Additions { get; set; }
    public int Deletions { get; set; }
    public int Changes { get; set; }
}

public class DiffFile
{
    public string Path { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Additions { get; set; }
    public int Deletions { get; set; }
    public int Changes { get; set; }
}
