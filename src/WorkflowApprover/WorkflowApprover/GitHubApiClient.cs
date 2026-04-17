using System.Net.Http.Headers;
using System.Text.Json;

namespace WorkflowApprover;

/// <summary>
/// Discovers GitHub Actions workflow runs that require approval
/// using the GitHub REST API.
/// </summary>
public sealed class GitHubApiClient : IDisposable
{
    private readonly HttpClient _http;

    public GitHubApiClient(string? token = null)
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("WorkflowApprover/1.0");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");

        if (!string.IsNullOrWhiteSpace(token))
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
    }

    /// <summary>
    /// Returns a list of workflow runs that are in "action_required" status.
    /// </summary>
    public async Task<List<WorkflowRunInfo>> GetPendingApprovalRunsAsync(
        string owner, string repo, CancellationToken ct = default)
    {
        var result = new List<WorkflowRunInfo>();

        try
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/actions/runs?status=action_required&per_page=50";
            var response = await _http.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                var statusCode = (int)response.StatusCode;
                var hint = statusCode switch
                {
                    401 => " (check your GitHub token)",
                    403 => " (rate limit exceeded — set a GitHub token or wait)",
                    404 => " (repository not found — check owner/repo)",
                    _ => ""
                };
                throw new Exception($"GitHub API returned {statusCode}{hint}: {errorBody}");
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("workflow_runs", out var runs))
            {
                foreach (var run in runs.EnumerateArray())
                {
                    var info = new WorkflowRunInfo
                    {
                        Id = run.GetProperty("id").GetInt64(),
                        Name = run.GetProperty("name").GetString() ?? "Unknown",
                        Status = run.GetProperty("status").GetString() ?? "unknown",
                        Conclusion = run.TryGetProperty("conclusion", out var c)
                            ? c.GetString() : null,
                        HtmlUrl = run.GetProperty("html_url").GetString() ?? "",
                        HeadBranch = run.TryGetProperty("head_branch", out var hb)
                            ? hb.GetString() ?? "" : "",
                        CreatedAt = run.TryGetProperty("created_at", out var ca)
                            ? ca.GetString() ?? "" : "",
                        Event = run.TryGetProperty("event", out var ev)
                            ? ev.GetString() ?? "" : "",
                    };
                    result.Add(info);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled, return empty
        }

        return result;
    }

    public void Dispose() => _http.Dispose();
}

/// <summary>
/// Information about a GitHub Actions workflow run.
/// </summary>
public sealed class WorkflowRunInfo
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Conclusion { get; set; }
    public string HtmlUrl { get; set; } = "";
    public string HeadBranch { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string Event { get; set; } = "";

    public override string ToString() =>
        $"[{Id}] {Name} ({Status}) — {HeadBranch} — {Event}";
}
