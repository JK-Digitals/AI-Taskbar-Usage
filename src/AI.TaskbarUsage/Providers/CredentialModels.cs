#nullable enable
using System.Text.Json;

namespace AiTaskbarUsage;

public sealed record LocalCredential(string AccessToken, string? AccountId, DateTimeOffset? ExpiresAt);

public static class CredentialParser
{
    public static bool TryParseClaude(string json, out LocalCredential credential)
    {
        credential = default!;
        try
        {
            using var document = JsonDocument.Parse(json);
            var token = FindString(document.RootElement, "accessToken", "access_token", "oauthToken");
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var expiresAt = FindDate(document.RootElement, "expiresAt", "expires_at");
            credential = new LocalCredential(token, null, expiresAt);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool TryParseCodex(string json, out LocalCredential credential)
    {
        credential = default!;
        try
        {
            using var document = JsonDocument.Parse(json);
            var tokens = FindObject(document.RootElement, "tokens");
            var token = tokens is null ? null : FindString(tokens.Value, "access_token", "accessToken");
            var accountId = tokens is null ? null : FindString(tokens.Value, "account_id", "accountId");
            token ??= FindString(document.RootElement, "access_token", "accessToken");
            accountId ??= FindString(document.RootElement, "account_id", "accountId");
            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            credential = new LocalCredential(token, accountId, FindDate(document.RootElement, "expires_at", "expiresAt"));
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static JsonElement? FindObject(JsonElement element, params string[] names)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (names.Any(name => property.NameEquals(name)) && property.Value.ValueKind == JsonValueKind.Object)
                {
                    return property.Value;
                }
            }

            foreach (var property in element.EnumerateObject())
            {
                var result = FindObject(property.Value, names);
                if (result is not null)
                {
                    return result;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                var result = FindObject(child, names);
                if (result is not null)
                {
                    return result;
                }
            }
        }

        return null;
    }

    private static string? FindString(JsonElement element, params string[] names)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (names.Any(name => property.NameEquals(name)) && property.Value.ValueKind == JsonValueKind.String)
                {
                    var value = property.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }

            foreach (var property in element.EnumerateObject())
            {
                var value = FindString(property.Value, names);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
            {
                var value = FindString(child, names);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }

        return null;
    }

    private static DateTimeOffset? FindDate(JsonElement element, params string[] names)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (names.Any(name => property.NameEquals(name)))
                {
                    if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt64(out var epoch))
                    {
                        try
                        {
                            return epoch > 10_000_000_000
                                ? DateTimeOffset.FromUnixTimeMilliseconds(epoch)
                                : DateTimeOffset.FromUnixTimeSeconds(epoch);
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            return null;
                        }
                    }

                    if (property.Value.ValueKind == JsonValueKind.String &&
                        DateTimeOffset.TryParse(property.Value.GetString(), out var direct))
                    {
                        return direct;
                    }
                }
            }
        }

        var value = FindString(element, names);
        if (DateTimeOffset.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return null;
    }
}
