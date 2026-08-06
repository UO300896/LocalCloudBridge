using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.Net.Http.Headers;
using LocalCloudBridge.Models;

namespace LocalCloudBridge.Services;

/// <summary>
/// Core reverse proxy handler for forwarding HTTP requests and streaming responses.
/// </summary>
public static class ProxyService
{
    /// <summary>
    /// Forwards an incoming HTTP request to the configured target service, injecting
    /// necessary authentication headers and streaming back the response.
    /// </summary>
    /// <param name="context">The current ASP.NET Core HTTP context.</param>
    /// <param name="factory">HTTP client factory.</param>
    /// <param name="options">Bridge configuration options.</param>
    public static async Task HandleAsync(
        HttpContext context,
        IHttpClientFactory factory,
        BridgeOptions options)
    {
        var client = factory.CreateClient("proxy");

        var url =
            options.Target.Url +
            context.Request.Path +
            context.Request.QueryString;

        var request = new HttpRequestMessage(
            new HttpMethod(context.Request.Method),
            url);

        // Copy request body for non-empty requests
        if (context.Request.ContentLength > 0 ||
            context.Request.Headers.ContainsKey("Transfer-Encoding"))
        {
            request.Content = new StreamContent(context.Request.Body);

            if (!string.IsNullOrWhiteSpace(context.Request.ContentType))
            {
                request.Content.Headers.ContentType =
                    MediaTypeHeaderValue.Parse(context.Request.ContentType);
            }
        }

        // Copy request headers (excluding Host header)
        foreach (var header in context.Request.Headers)
        {
            if (string.Equals(header.Key, "Host", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
            {
                request.Content?.Headers.TryAddWithoutValidation(
                    header.Key,
                    header.Value.ToArray());
            }
        }

        // Apply authentication headers (Cloudflare, Bearer, Basic, ApiKey)
        AuthenticationService.Apply(request, options);

        // Send request to upstream target service
        var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            context.RequestAborted);

        context.Response.StatusCode = (int)response.StatusCode;

        // Copy response headers
        foreach (var header in response.Headers)
            context.Response.Headers[header.Key] =
                new StringValues(header.Value.ToArray());

        foreach (var header in response.Content.Headers)
            context.Response.Headers[header.Key] =
                new StringValues(header.Value.ToArray());

        context.Response.Headers.Remove("transfer-encoding");

        // Stream response body back to downstream caller
        await using var stream =
            await response.Content.ReadAsStreamAsync();

        await stream.CopyToAsync(context.Response.Body);
    }
}