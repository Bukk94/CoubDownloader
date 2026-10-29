using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CoubDownloader.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CoubDownloader.Services;

public class AuthToken
{
    private const string CookieName = "sb-gotrue-auth-token";
    private const string Prefix = "base64-";
    private const string RefreshUrl = "https://coub.com/foryou";
    private const int RefreshMarginSeconds = 5;

    private readonly HttpClient _http;
    private string _token;

    public AuthToken(HttpClient http)
    {
        _http = http;
    }

    public static string GetCookie(string token)
    {
        return $"{CookieName}={token}";
    }

    public string Get()
    {
        if (!string.IsNullOrWhiteSpace(_token))
        {
            // If user already passed token, return it
            return _token;
        }

        ConsoleEx.WriteLineColor($"[Enter/paste your auth token (value of '{CookieName}' cookie, starts with base64-)]. Read README if you don't know how to get it.", ConsoleColor.Green);
        ConsoleEx.WriteColor("[Auth Token]: ", ConsoleColor.Green);
        var token = Console.ReadLine()?.Trim();

        // Allow pasting whole cookie string or 'name=value'
        var cookiePrefix = CookieName + "=";
        var start = token?.IndexOf(cookiePrefix, StringComparison.Ordinal) ?? -1;
        if (start >= 0)
        {
            token = token?.Substring(start + cookiePrefix.Length).Split(';')[0].Trim();
        }

        if (string.IsNullOrWhiteSpace(token) || 
            !token.StartsWith(Prefix, StringComparison.Ordinal))
        {
            ConsoleEx.WriteLineColor($"[Token must start with '{Prefix}'. Make sure you copied the value of '{CookieName}' cookie.]", ConsoleColor.Red);
            return null;
        }

        _token = token;

        return token;
    }

    public async Task<string> RefreshIfExpiringAsync(string token)
    {
        var expiresAt = token == null ? null : GetExpiresAt(token);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (expiresAt == null || expiresAt > now + RefreshMarginSeconds)
        {
            return token;
        }

        Console.WriteLine("Auth token is expired or about to expire, refreshing it...");
        var newToken = await RefreshAsync(token);
        if (newToken != null)
        {
            _token = newToken;
            return newToken;
        }

        if (expiresAt <= now)
        {
            throw new InvalidOperationException("Auth token could not be refreshed (session ended or token was already used). Get a new token from your browser.");
        }

        // Token is still valid, Coub just does not want to refresh it yet
        return token;
    }

    private static long? GetExpiresAt(string token)
    {
        // Token is base64 (url-safe, no padding) encoded session JSON
        var base64 = token.Substring(Prefix.Length).Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');

        try
        {
            var session = JObject.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(base64)));
            return (long?)session["expires_at"];
        }
        catch (Exception ex) when (ex is FormatException or JsonReaderException)
        {
            return null;
        }
    }

    private async Task<string> RefreshAsync(string token)
    {
        // Coub refreshes expired session on any page request and returns new token in Set-Cookie
        using var request = new HttpRequestMessage(HttpMethod.Get, RefreshUrl);
        request.Headers.Add("Cookie", GetCookie(token));

        using var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var cookiePrefix = CookieName + "=";
        var cookie = response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.FirstOrDefault(x => x.StartsWith(cookiePrefix, StringComparison.Ordinal))
            : null;
        var newToken = cookie?.Substring(cookiePrefix.Length).Split(';')[0];

        // No new token (or cleared cookie) means Coub did not refresh the session
        return string.IsNullOrEmpty(newToken) ? null : newToken;
    }
}
