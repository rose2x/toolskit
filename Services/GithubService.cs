using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ToolkitApp.Services;

public class GithubRepo
{
    [JsonPropertyName("full_name")] public string FullName { get; set; } = "";
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("stargazers_count")] public int StargazersCount { get; set; }
    [JsonPropertyName("forks_count")] public int ForksCount { get; set; }
    [JsonPropertyName("clone_url")] public string CloneUrl { get; set; } = "";
    [JsonPropertyName("html_url")] public string HtmlUrl { get; set; } = "";
    [JsonPropertyName("language")] public string? Language { get; set; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; set; }

    [JsonIgnore] public string DescriptionText => string.IsNullOrWhiteSpace(Description) ? "(no description)" : Description!;
    [JsonIgnore] public string RepoName => FullName.Contains('/') ? FullName[(FullName.IndexOf('/') + 1)..] : FullName;
    [JsonIgnore] public string Meta =>
        $"⭐ {StargazersCount:N0}   ⑂ {ForksCount:N0}" +
        (string.IsNullOrEmpty(Language) ? "" : $"   ● {Language}") +
        (UpdatedAt is DateTimeOffset d ? $"   updated {d.LocalDateTime:d}" : "");
}

public class GithubSearchResponse
{
    [JsonPropertyName("total_count")] public int TotalCount { get; set; }
    [JsonPropertyName("items")] public List<GithubRepo> Items { get; set; } = new();
}

public sealed class GithubException : Exception { public GithubException(string m) : base(m) { } }

public static class GithubService
{
    private static readonly HttpClient Http = CreateClient();
    private static readonly Regex SafeName = new(@"^[A-Za-z0-9_.\-]+/[A-Za-z0-9_.\-]+$", RegexOptions.Compiled);

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ToolKit", AppInfo.Version));
        c.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        return c;
    }

    public static bool IsSafeRepoName(string fullName) => SafeName.IsMatch(fullName ?? "");

    /// <summary>Only https://github.com/... clone URLs are ever used for cloning.</summary>
    public static bool IsSafeCloneUrl(string url) =>
        url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase) && !url.Any(c => c is '"' or ' ' or '&' or '|' or '^' or '<' or '>');

    public static string? GetToken(string? settingsToken)
    {
        if (!string.IsNullOrWhiteSpace(settingsToken)) return settingsToken.Trim();
        var env = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        return string.IsNullOrWhiteSpace(env) ? null : env.Trim();
    }

    public static string BuildUrl(string text, string? language, string sort, int page, int perPage)
    {
        string q = text.Trim();
        if (!string.IsNullOrWhiteSpace(language))
        {
            string l = language.Trim();
            q += l.Contains(' ') ? $" language:\"{l}\"" : $" language:{l}";
        }
        string url = $"https://api.github.com/search/repositories?q={Uri.EscapeDataString(q)}&per_page={perPage}&page={page}";
        if (sort is "stars" or "updated" or "forks") url += $"&sort={sort}&order=desc";
        return url;
    }

    public static async Task<GithubSearchResponse> SearchAsync(string text, string? language, string sort, int page, int perPage,
        string? token, CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, BuildUrl(text, language, sort, page, perPage));
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        if (!string.IsNullOrEmpty(token)) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var resp = await Http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode) throw Explain(resp, token != null);
        var json = await resp.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<GithubSearchResponse>(json) ?? new GithubSearchResponse();
    }

    public static async Task<string> GetReadmeAsync(string fullName, string? token, CancellationToken ct = default)
    {
        if (!IsSafeRepoName(fullName)) return "";
        using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{fullName}/readme");
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.raw+json"));
        if (!string.IsNullOrEmpty(token)) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resp = await Http.SendAsync(req, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound) return "(This repository has no README.)";
        if (!resp.IsSuccessStatusCode) throw Explain(resp, token != null);
        var text = await resp.Content.ReadAsStringAsync(ct);
        return text.Length > 20000 ? text[..20000] + "\n\n… (truncated)" : text;
    }

    private static GithubException Explain(HttpResponseMessage r, bool hadToken)
    {
        if (r.StatusCode == HttpStatusCode.Unauthorized)
            return new GithubException("GitHub rejected your token. Fix or clear it in Settings → GitHub.");
        if (r.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
        {
            string when = "";
            if (r.Headers.TryGetValues("X-RateLimit-Reset", out var v) && long.TryParse(v.FirstOrDefault(), out var epoch))
                when = $" It resets at {DateTimeOffset.FromUnixTimeSeconds(epoch).LocalDateTime:t}.";
            return new GithubException("GitHub rate limit reached." + when + (hadToken ? "" : " Add a token in Settings for higher limits."));
        }
        if (r.StatusCode == HttpStatusCode.UnprocessableEntity)
            return new GithubException("GitHub could not understand that search. Try simpler words.");
        return new GithubException($"GitHub returned {(int)r.StatusCode} {r.ReasonPhrase}.");
    }
}
