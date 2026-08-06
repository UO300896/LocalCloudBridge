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
    public static async Task WaitUntilOnlineAsync(
        IHttpClientFactory factory,
        BridgeOptions options)
    {
        Console.Title = "LocalCloudBridge";

        Console.WriteLine("---------------------------------------");
        Console.WriteLine("LocalCloudBridge");
        Console.WriteLine("---------------------------------------");
        Console.WriteLine();

        Console.WriteLine($"Connecting to: {options.Target.Url}");

        if (options.Authentication.Type != AuthenticationType.None)
        {
            Console.WriteLine($"Checking {options.Authentication.Type} tunnel...");
        }

        Console.WriteLine();

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

                var response = await client.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException($"Target returned status code {(int)response.StatusCode}");

                connected = true;

                if (wolSent)
                    Console.WriteLine();

                if (options.Authentication.Type != AuthenticationType.None)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("OK Tunnel reachable");
                    Console.ResetColor();
                    Console.WriteLine();
                }

                Console.WriteLine($"Checking {options.Target.Name}...");

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("OK Connected");
                Console.WriteLine("OK Ready");
                Console.ResetColor();

                Console.WriteLine();

                Console.WriteLine("Listening on:");
                Console.WriteLine(options.Listen);
                Console.WriteLine();
            }
            catch
            {
                if (!wolSent)
                {
                    Console.WriteLine();

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("ERROR: Target service offline or unreachable");
                    Console.ResetColor();

                    Console.WriteLine();

                    if (options.WakeOnLan.Enabled)
                    {
                        Console.WriteLine("Sending Wake-on-LAN...");

                        try
                        {
                            await WakeOnLan.SendAsync(options);

                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("OK Magic packet sent!");
                            Console.ResetColor();
                        }
                        catch (Exception ex)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine(ex.Message);
                            Console.ResetColor();
                        }

                        Console.WriteLine();

                        wolSent = true;
                    }
                }

                Console.WriteLine("Waiting for target service to become available...");
                await Task.Delay(30000);
            }
        }
    }
}