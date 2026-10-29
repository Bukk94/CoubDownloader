using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CoubDownloader.Extensions;

namespace CoubDownloader.Models;

public class RetryHandler : DelegatingHandler
{
    private const int MaxRetries = 4;
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(60);

    public RetryHandler(HttpMessageHandler innerHandler) : base(innerHandler)
    {
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            TimeSpan delay;
            string reason;

            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                if (!IsTransient(response.StatusCode) || attempt > MaxRetries)
                {
                    return response;
                }

                reason = $"HTTP {(int)response.StatusCode}";
                delay = GetDelay(response, attempt);
                response.Dispose();
            }
            catch (HttpRequestException ex) when (attempt <= MaxRetries)
            {
                reason = ex.Message;
                delay = GetBackoff(attempt);
            }

            ConsoleEx.WriteLineColor($"    [Request failed ({reason}), retry {attempt} of {MaxRetries} in {delay.TotalSeconds:0}s]", ConsoleColor.Yellow);
            await Task.Delay(delay, cancellationToken);
        }
    }

    private static bool IsTransient(HttpStatusCode status)
    {
        return status == HttpStatusCode.RequestTimeout ||
               status == HttpStatusCode.TooManyRequests ||
               (int)status >= 500;
    }

    private static TimeSpan GetDelay(HttpResponseMessage response, int attempt)
    {
        // Respect server's own wish if there is one
        var retryAfter = response.Headers.RetryAfter;
        var delay = retryAfter?.Delta ?? (retryAfter?.Date - DateTimeOffset.UtcNow) ?? GetBackoff(attempt);

        return delay > MaxDelay ? MaxDelay : delay < TimeSpan.Zero ? TimeSpan.Zero : delay;
    }

    private static TimeSpan GetBackoff(int attempt)
    {
        // 2s, 4s, 8s, 16s
        return TimeSpan.FromSeconds(Math.Pow(2, attempt));
    }
}
