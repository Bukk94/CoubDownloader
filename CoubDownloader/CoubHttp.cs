using System;
using System.Net;
using System.Net.Http;
using CoubDownloader.Configurations;

namespace CoubDownloader
{
    public static class CoubHttp
    {
        public static HttpClient Create(Configuration configuration)
        {
            var handler = new HttpClientHandler
            {
                // Cookie header with access token is set manually
                UseCookies = false,
                AutomaticDecompression = DecompressionMethods.All
            };

            if (!string.IsNullOrWhiteSpace(configuration.ProxyUrl))
            {
                handler.Proxy = new WebProxy(configuration.ProxyUrl);
            }

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(5)
            };
            
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent.GetRandomAgent());

            return client;
        }
    }
}
