using System.Net;
using System.Net.Http.Headers;
using System.Text;
using LocalCloudBridge.Models;

namespace LocalCloudBridge.Services;

public sealed class PortableProxyServer
{
    private readonly BridgeOptions _options;
    private readonly HttpClient _client;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;

    public bool IsRunning => _listener?.IsListening ?? false;

    public event Action<string>? OnLog;
    public event Action<string>? OnStatusChanged;

    public PortableProxyServer(BridgeOptions options, HttpClient? client = null)
    {
        _options = options;
        _client = client ?? new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
            ConnectTimeout = TimeSpan.FromSeconds(15),
            EnableMultipleHttp2Connections = true
        });
    }

    public async Task StartAsync()
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        _listener = new HttpListener();

        string prefix = _options.Listen;
        if (!prefix.EndsWith("/"))
        {
            prefix += "/";
        }

        _listener.Prefixes.Add(prefix);

        try
        {
            _listener.Start();
            OnStatusChanged?.Invoke("Running");

            // Perform health check and WOL if target is offline
            _ = Task.Run(async () =>
            {
                await HealthChecker.WaitUntilOnlineAsync(new SingleClientFactory(_client), _options, Log);

                Log($"info: Microsoft.Hosting.Lifetime[14]");
                Log($"      Now listening on: {prefix}");
                Log($"info: Microsoft.Hosting.Lifetime[0]");
                Log($"      Application started. Press Stop to shut down.");
            });

            _ = AcceptLoopAsync(_cts.Token);
        }
        catch (Exception ex)
        {
            Log($"error: Error starting proxy listener: {ex.Message}");
            OnStatusChanged?.Invoke("Error");
            throw;
        }
    }

    public void Stop()
    {
        _cts?.Cancel();
        try
        {
            _listener?.Stop();
            _listener?.Close();
        }
        catch { }
        _listener = null;
        OnStatusChanged?.Invoke("Stopped");
        Log("info: Microsoft.Hosting.Lifetime[0]");
        Log("      Application is shutting down...");
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener != null && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = ProcessRequestAsync(context, token);
            }
            catch (HttpListenerException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    Log($"error: Accept error: {ex.Message}");
                }
            }
        }
    }

    private async Task ProcessRequestAsync(HttpListenerContext context, CancellationToken token)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        string rawUrl = context.Request.RawUrl ?? "/";
        string rawPath = rawUrl.Split('?')[0];
        string method = context.Request.HttpMethod;

        Log($"info: System.Net.Http.HttpClient.proxy.LogicalHandler[100]");
        Log($"      Start processing HTTP request {method} {rawUrl}");

        try
        {
            // Intercept root endpoint (/)
            if (string.Equals(rawPath, "/", StringComparison.OrdinalIgnoreCase))
            {
                byte[] jsonBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new
                {
                    status = "ok",
                    service = _options.Target.Name,
                    target = _options.Target.Url,
                    health = _options.Target.HealthCheck
                }));

                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.OutputStream.WriteAsync(jsonBytes, token);
                context.Response.Close();

                sw.Stop();
                Log($"info: System.Net.Http.HttpClient.proxy.LogicalHandler[101]");
                Log($"      End processing HTTP request after {sw.Elapsed.TotalMilliseconds:F4}ms - 200");
                return;
            }

            // Intercept health check endpoint (/health)
            if (string.Equals(rawPath.TrimEnd('/'), "/health", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string healthUrl = _options.Target.Url.TrimEnd('/') + _options.Target.HealthCheck;
                    using var healthRequest = new HttpRequestMessage(HttpMethod.Get, healthUrl);
                    AuthenticationService.Apply(healthRequest, _options);

                    using var healthResponse = await _client.SendAsync(healthRequest, token);

                    byte[] jsonBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new
                    {
                        connected = healthResponse.IsSuccessStatusCode,
                        status = (int)healthResponse.StatusCode,
                        service = _options.Target.Name
                    }));

                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.ContentType = "application/json; charset=utf-8";
                    await context.Response.OutputStream.WriteAsync(jsonBytes, token);
                    context.Response.Close();

                    sw.Stop();
                    Log($"info: System.Net.Http.HttpClient.proxy.LogicalHandler[101]");
                    Log($"      End processing HTTP request after {sw.Elapsed.TotalMilliseconds:F4}ms - 200");
                    return;
                }
                catch (Exception ex)
                {
                    byte[] jsonBytes = Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(new
                    {
                        connected = false,
                        error = ex.Message
                    }));

                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.ContentType = "application/json; charset=utf-8";
                    await context.Response.OutputStream.WriteAsync(jsonBytes, token);
                    context.Response.Close();

                    sw.Stop();
                    Log($"info: System.Net.Http.HttpClient.proxy.LogicalHandler[101]");
                    Log($"      End processing HTTP request after {sw.Elapsed.TotalMilliseconds:F4}ms - 200 (Error: {ex.Message})");
                    return;
                }
            }

            // Read entity body into memory buffer for potential retries
            byte[]? bodyBytes = null;
            if (context.Request.HasEntityBody)
            {
                using var ms = new MemoryStream();
                await context.Request.InputStream.CopyToAsync(ms, token);
                bodyBytes = ms.ToArray();
            }

            HttpResponseMessage? response = null;
            Exception? lastException = null;
            int maxAttempts = 2;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    string targetUrl = _options.Target.Url.TrimEnd('/') + rawUrl;
                    using var request = new HttpRequestMessage(new HttpMethod(method), targetUrl);

                    if (bodyBytes != null && bodyBytes.Length > 0)
                    {
                        request.Content = new ByteArrayContent(bodyBytes);
                        if (!string.IsNullOrWhiteSpace(context.Request.ContentType))
                        {
                            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(context.Request.ContentType);
                        }
                    }

                    foreach (string? headerName in context.Request.Headers.AllKeys)
                    {
                        if (string.IsNullOrEmpty(headerName) || string.Equals(headerName, "Host", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string[]? values = context.Request.Headers.GetValues(headerName);
                        if (values != null)
                        {
                            if (!request.Headers.TryAddWithoutValidation(headerName, values))
                            {
                                request.Content?.Headers.TryAddWithoutValidation(headerName, values);
                            }
                        }
                    }

                    AuthenticationService.Apply(request, _options);

                    response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                    lastException = null;
                    break;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (attempt < maxAttempts)
                    {
                        Log($"warn: Proxy attempt {attempt} failed ({ex.Message}). Retrying in 500ms...");
                        await Task.Delay(500, token);
                    }
                }
            }

            if (response == null || lastException != null)
            {
                throw lastException ?? new HttpRequestException("Proxy request failed.");
            }

            using (response)
            {
                sw.Stop();

                Log($"info: System.Net.Http.HttpClient.proxy.ClientHandler[101]");
                Log($"      Received HTTP response headers after {sw.Elapsed.TotalMilliseconds:F4}ms - {(int)response.StatusCode}");

                context.Response.StatusCode = (int)response.StatusCode;

                foreach (var header in response.Headers)
                {
                    context.Response.Headers[header.Key] = string.Join(", ", header.Value);
                }

                foreach (var header in response.Content.Headers)
                {
                    context.Response.Headers[header.Key] = string.Join(", ", header.Value);
                }

                context.Response.Headers.Remove("Transfer-Encoding");

                await using var responseStream = await response.Content.ReadAsStreamAsync(token);
                await responseStream.CopyToAsync(context.Response.OutputStream, token);
                context.Response.Close();

                Log($"info: System.Net.Http.HttpClient.proxy.LogicalHandler[101]");
                Log($"      End processing HTTP request after {sw.Elapsed.TotalMilliseconds:F4}ms - {(int)response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            sw.Stop();
            Log($"error: Request failed after {sw.Elapsed.TotalMilliseconds:F4}ms: {ex.Message}");
            Log($"info: System.Net.Http.HttpClient.proxy.LogicalHandler[101]");
            Log($"      End processing HTTP request after {sw.Elapsed.TotalMilliseconds:F4}ms with error - 502 Bad Gateway");
            try
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadGateway;
                byte[] errorBytes = Encoding.UTF8.GetBytes($"Proxy Error: {ex.Message}");
                context.Response.OutputStream.Write(errorBytes, 0, errorBytes.Length);
                context.Response.Close();
            }
            catch { }
        }
    }

    private void Log(string message)
    {
        OnLog?.Invoke(message);
    }

    private sealed class SingleClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _client;
        public SingleClientFactory(HttpClient client) => _client = client;
        public HttpClient CreateClient(string name) => _client;
    }
}
