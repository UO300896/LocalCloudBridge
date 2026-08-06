using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.Net.Http.Headers;
using LocalCloudBridge.Models;
using LocalCloudBridge.Core;

namespace LocalCloudBridge.Server;

public static class ProxyService
{
    public static async Task HandleAsync(
        HttpContext context,
        IHttpClientFactory factory,
        BridgeOptions options)
    {
        var engine = new BridgeEngine(factory, options);

        var client = engine.CreateClient();

        var request = engine.CreateRequest(
            context.Request.Method,
            context.Request.Path.Value ?? "/",
            context.Request.QueryString.Value ?? string.Empty);

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

        var response = await client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            context.RequestAborted);

        context.Response.StatusCode = (int)response.StatusCode;

        // Copy response headers
        foreach (var header in response.Headers)
        {
            context.Response.Headers[header.Key] =
                new StringValues(header.Value.ToArray());
        }

        foreach (var header in response.Content.Headers)
        {
            context.Response.Headers[header.Key] =
                new StringValues(header.Value.ToArray());
        }

        context.Response.Headers.Remove("transfer-encoding");

        await using var stream =
            await response.Content.ReadAsStreamAsync();

        await stream.CopyToAsync(context.Response.Body);
    }
}