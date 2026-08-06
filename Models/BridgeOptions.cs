using Microsoft.Extensions.Configuration;

namespace LocalCloudBridge.Models;

/// <summary>
/// Root configuration options for the bridge.
/// </summary>
public class BridgeOptions
{
    /// <summary>Address and port for LocalCloudBridge to listen on (e.g., http://127.0.0.1:11435).</summary>
    public string Listen { get; init; } = string.Empty;

    /// <summary>Target upstream service configuration.</summary>
    public TargetOptions Target { get; init; } = new();

    /// <summary>Authentication settings applied to outgoing HTTP requests.</summary>
    public AuthenticationOptions Authentication { get; init; } = new();

    /// <summary>Wake-on-LAN settings used when the target host is unreachable.</summary>
    public WakeOnLanOptions WakeOnLan { get; init; } = new();

    /// <summary>
    /// Loads bridge configuration and applies default values for optional settings.
    /// </summary>
    public static BridgeOptions Load(IConfiguration configuration)
    {
        string broadcastIp = configuration["WakeOnLan:BroadcastIP"] ?? "";
        if (string.IsNullOrWhiteSpace(broadcastIp))
        {
            broadcastIp = "255.255.255.255";
        }

        int wolPort = configuration.GetValue<int>("WakeOnLan:Port");
        if (wolPort <= 0)
        {
            wolPort = 9;
        }

        return new BridgeOptions
        {
            Listen = configuration["Listen"] ?? "http://127.0.0.1:11435",
            Target = new TargetOptions
            {
                Name = configuration["Target:Name"] ?? "Target Service",
                Url = (configuration["Target:Url"] ?? string.Empty).TrimEnd('/'),
                HealthCheck = configuration["Target:HealthCheck"] ?? "/"
            },
            Authentication = new AuthenticationOptions
            {
                Type = Enum.Parse<AuthenticationType>(
                    configuration["Authentication:Type"] ?? "None",
                    ignoreCase: true),

                ClientId = configuration["Authentication:ClientId"] ?? "",
                ClientSecret = configuration["Authentication:ClientSecret"] ?? "",

                BearerToken = configuration["Authentication:BearerToken"] ?? "",
                Username = configuration["Authentication:Username"] ?? "",
                Password = configuration["Authentication:Password"] ?? "",

                ApiKeyHeader = configuration["Authentication:ApiKeyHeader"] ?? "",
                ApiKey = configuration["Authentication:ApiKey"] ?? ""
            },
            WakeOnLan = new WakeOnLanOptions
            {
                Enabled = configuration.GetValue<bool>("WakeOnLan:Enabled"),
                Host = configuration["WakeOnLan:Host"] ?? "",
                Port = wolPort,
                MacAddress = configuration["WakeOnLan:MacAddress"] ?? "",
                BroadcastIP = broadcastIp
            }
        };
    }
}

/// <summary>
/// Target upstream service options.
/// </summary>
public class TargetOptions
{
    /// <summary>Friendly  name of the target service used for logging.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Base URL of the target service or tunnel (e.g., https://subdomain.yourdomain.com).</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>Health check endpoint path (e.g., /api/tags).</summary>
    public string HealthCheck { get; init; } = "/";
}

/// <summary>
/// Authentication settings applied to proxied HTTP requests.
/// </summary>
public class AuthenticationOptions
{
    /// <summary>Authentication type strategy.</summary>
    public AuthenticationType Type { get; init; } = AuthenticationType.None;

    /// <summary>Client ID used for Cloudflare Access Service Token authentication.</summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>Client Secret used for Cloudflare Access Service Token authentication.</summary>
    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>Bearer Token used for HTTP Authorization header.</summary>
    public string BearerToken { get; init; } = string.Empty;

    /// <summary>Username used for HTTP Basic authentication.</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>Password used for HTTP Basic authentication.</summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>Header name used for Custom API Key authentication.</summary>
    public string ApiKeyHeader { get; init; } = string.Empty;

    /// <summary>API key value used for Custom API Key authentication.</summary>
    public string ApiKey { get; init; } = string.Empty;
}

/// <summary>
/// Wake-on-LAN and Wake-on-WAN settings.
/// </summary>
public class WakeOnLanOptions
{
    /// <summary>Whether Wake-on-LAN is enabled when target health check fails.</summary>
    public bool Enabled { get; init; }

    /// <summary>DDNS hostname or public IP address used for Wake-on-WAN (optional).</summary>
    public string Host { get; init; } = string.Empty;

    /// <summary>UDP port for Wake-on-LAN (default: 9).</summary>
    public int Port { get; init; } = 9;

    /// <summary>MAC address of the target machine (e.g., AA:BB:CC:DD:EE:FF).</summary>
    public string MacAddress { get; init; } = string.Empty;

    /// <summary>Broadcast address used for local Wake-on-LAN packets.</summary>
    public string BroadcastIP { get; init; } = "255.255.255.255";
}