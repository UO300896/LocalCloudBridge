using LocalCloudBridge.Models;

namespace LocalCloudBridge.Services;

/// <summary>
/// Monitors target service availability and triggers Wake-on-LAN if offline.
/// </summary>
public static class HealthChecker
{
    /// <summary>
    /// Repeatedly checks target service health at startup until reachable.
    /// If target service is unreachable, sends WOL magic packet and polls every 30 seconds.
    /// </summary>
    /// <param name="factory">HTTP client factory.</param>
    /// <param name="options">Bridge configuration options.</param>
    /// <param name="logAction">Optional custom logging callback.</param>
    public static async Task WaitUntilOnlineAsync(
        IHttpClientFactory factory,
        BridgeOptions options,
        Action<string>? logAction = null)
    {
        Action<string, ConsoleColor?> log = (msg, color) =>
        {
            if (logAction != null)
            {
                logAction(msg);
            }
            else
            {
                if (color.HasValue) Console.ForegroundColor = color.Value;
                Console.WriteLine(msg);
                if (color.HasValue) Console.ResetColor();
            }
        };

        log("---------------------------------------", null);
        log("LocalCloudBridge Health Monitor", null);
        log("---------------------------------------", null);
        log($"Connecting to: {options.Target.Url}", null);

        if (options.Authentication.Type != AuthenticationType.None)
        {
            log($"Checking {options.Authentication.Type} tunnel...", null);
        }

        var client = factory.CreateClient("proxy");

        bool connected = false;
        bool wolSent = false;

        while (!connected)
        {
            try
            {
                var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    options.Target.Url + options.Target.HealthCheck);

                AuthenticationService.Apply(request, options);

                var sw = System.Diagnostics.Stopwatch.StartNew();
                var response = await client.SendAsync(request);
                sw.Stop();

                log($"info: Probed target status code {(int)response.StatusCode} in {sw.Elapsed.TotalMilliseconds:F2}ms", null);

                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException($"Target returned status code {(int)response.StatusCode}");

                connected = true;

                if (options.Authentication.Type != AuthenticationType.None)
                {
                    log("OK Tunnel reachable", ConsoleColor.Green);
                }

                log($"Checking {options.Target.Name}...", null);
                log("OK Connected", ConsoleColor.Green);
                log("OK Ready", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                if (!wolSent)
                {
                    log($"ERROR: Target service offline or unreachable ({ex.Message})", ConsoleColor.Red);

                    if (options.WakeOnLan.Enabled)
                    {
                        log("Sending Wake-on-LAN...", null);

                        try
                        {
                            await WakeOnLan.SendAsync(options, logAction);
                            log("OK Magic packet sent!", ConsoleColor.Green);
                        }
                        catch (Exception wolEx)
                        {
                            log($"WOL Error: {wolEx.Message}", ConsoleColor.Red);
                        }

                        wolSent = true;
                    }
                }

                log("Waiting for target service to become available...", null);
                await Task.Delay(30000);
            }
        }
    }
}