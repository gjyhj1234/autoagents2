using System.Text.Json;

namespace WorkflowApprover;

/// <summary>
/// Application settings that persist across sessions.
/// </summary>
public sealed class AppSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WorkflowApprover",
        "settings.json");

    /// <summary>GitHub repository owner (e.g., "gjyhj1234").</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>GitHub repository name (e.g., "autoagents2").</summary>
    public string Repo { get; set; } = string.Empty;

    /// <summary>Polling interval in seconds (default 180 = 3 minutes).</summary>
    public int IntervalSeconds { get; set; } = 180;

    /// <summary>Whether auto-approve is enabled on startup.</summary>
    public bool AutoStartEnabled { get; set; } = false;

    /// <summary>GitHub Personal Access Token for API calls (optional, improves rate limit).</summary>
    public string GitHubToken { get; set; } = string.Empty;

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            // Ignore deserialization errors, return defaults
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Ignore save errors silently
        }
    }
}
