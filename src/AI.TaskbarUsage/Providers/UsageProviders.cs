#nullable enable
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AiTaskbarUsage;

public interface IUsageProvider
{
    ServiceKind Service { get; }
    Task<ProviderResult> RefreshAsync(CancellationToken cancellationToken);
}

public sealed class AnthropicUsageProvider(HttpClient httpClient) : IUsageProvider
{
    private static readonly Uri UsageUri = new("https://api.anthropic.com/api/oauth/usage");
    public ServiceKind Service => ServiceKind.Claude;

    public async Task<ProviderResult> RefreshAsync(CancellationToken cancellationToken)
    {
        if (!CredentialDiscovery.TryFindClaude(out var credential))
        {
            return ProviderResult.Failure(Service, ProviderState.NoLogin, "Sign in to Claude Code.");
        }

        if (credential.ExpiresAt is not null && credential.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return ProviderResult.Failure(Service, ProviderState.Unauthorized, "Re-login in Claude Code.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, UsageUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.AccessToken);
            request.Headers.Add("anthropic-beta", "oauth-2025-04-20");
            request.Headers.UserAgent.ParseAdd(CredentialDiscovery.GetClaudeUserAgent());
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(string.Empty, Encoding.UTF8, "application/json");
            var response = await UsageHttp.SendJsonAsync(httpClient, request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return ProviderResult.Failure(Service, ProviderState.Unauthorized, "Re-login in Claude Code.");
            }

            if (response.StatusCode == (HttpStatusCode)429)
            {
                return ProviderResult.Failure(Service, ProviderState.RateLimited, "Claude usage endpoint rate-limited the refresh.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return ProviderResult.Failure(Service, ProviderState.Error, "Claude usage endpoint returned HTTP " + (int)response.StatusCode + ".");
            }

            using var document = JsonDocument.Parse(response.Body);
            if (!UsageParser.TryParse(document, DateTimeOffset.UtcNow, out var usage))
            {
                return ProviderResult.Failure(Service, ProviderState.Error, "Claude returned no recognized quota windows.");
            }

            return new ProviderResult(Service, ProviderState.Success, usage, null, DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ProviderResult.Failure(Service, ProviderState.Offline, "Claude refresh timed out.");
        }
        catch (HttpRequestException)
        {
            return ProviderResult.Failure(Service, ProviderState.Offline, "Claude is unreachable.");
        }
        catch (JsonException)
        {
            return ProviderResult.Failure(Service, ProviderState.Error, "Claude returned invalid usage data.");
        }
    }
}

public sealed class CodexUsageProvider(HttpClient httpClient) : IUsageProvider
{
    private static readonly Uri UsageUri = new("https://chatgpt.com/backend-api/wham/usage");
    public ServiceKind Service => ServiceKind.Codex;

    public async Task<ProviderResult> RefreshAsync(CancellationToken cancellationToken)
    {
        if (!CredentialDiscovery.TryFindCodex(out var credential))
        {
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            return ProviderResult.Failure(Service,
                string.IsNullOrWhiteSpace(apiKey) ? ProviderState.NoLogin : ProviderState.ApiKeyOnly,
                string.IsNullOrWhiteSpace(apiKey) ? "Sign in to Codex CLI." : "Codex API-key mode has no ChatGPT quota session.");
        }

        if (string.IsNullOrWhiteSpace(credential.AccountId))
        {
            return ProviderResult.Failure(Service, ProviderState.Unauthorized, "Codex auth.json has no account id.");
        }

        if (credential.ExpiresAt is not null && credential.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return ProviderResult.Failure(Service, ProviderState.Unauthorized, "Re-login in Codex CLI.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, UsageUri);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credential.AccessToken);
            request.Headers.Add("ChatGPT-Account-Id", credential.AccountId);
            request.Headers.Add("Origin", "https://chatgpt.com");
            request.Headers.Add("Referer", "https://chatgpt.com/");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var response = await UsageHttp.SendJsonAsync(httpClient, request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return ProviderResult.Failure(Service, ProviderState.Unauthorized, "Re-login in Codex CLI.");
            }

            if (response.StatusCode == (HttpStatusCode)429)
            {
                return ProviderResult.Failure(Service, ProviderState.RateLimited, "Codex usage endpoint rate-limited the refresh.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return ProviderResult.Failure(Service, ProviderState.Error, "Codex usage endpoint returned HTTP " + (int)response.StatusCode + ".");
            }

            using var document = JsonDocument.Parse(response.Body);
            if (!UsageParser.TryParse(document, DateTimeOffset.UtcNow, out var usage))
            {
                return ProviderResult.Failure(Service, ProviderState.Error, "Codex returned no recognized quota windows.");
            }

            return new ProviderResult(Service, ProviderState.Success, usage, null, DateTimeOffset.UtcNow);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ProviderResult.Failure(Service, ProviderState.Offline, "Codex refresh timed out.");
        }
        catch (HttpRequestException)
        {
            return ProviderResult.Failure(Service, ProviderState.Offline, "Codex is unreachable.");
        }
        catch (JsonException)
        {
            return ProviderResult.Failure(Service, ProviderState.Error, "Codex returned invalid usage data.");
        }
    }
}

internal sealed record JsonResponse(HttpStatusCode StatusCode, bool IsSuccessStatusCode, string Body);

internal static class UsageHttp
{
    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "api.anthropic.com",
        "chatgpt.com"
    };

    public static async Task<JsonResponse> SendJsonAsync(HttpClient client, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is null || request.RequestUri.Scheme != Uri.UriSchemeHttps || !AllowedHosts.Contains(request.RequestUri.Host))
        {
            throw new InvalidOperationException("Usage request host is not allowlisted.");
        }

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.Content.Headers.ContentLength is > 1024 * 1024)
        {
            throw new InvalidDataException("Usage response exceeds the size limit.");
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (body.Length > 1024 * 1024)
        {
            throw new InvalidDataException("Usage response exceeds the size limit.");
        }

        return new JsonResponse(response.StatusCode, response.IsSuccessStatusCode, body);
    }
}
