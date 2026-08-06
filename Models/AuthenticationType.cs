namespace LocalCloudBridge.Models;

/// <summary>
/// Supported authentication types for the upstream target service/tunnel.
/// </summary>
public enum AuthenticationType
{
    /// <summary>No authentication header will be added.</summary>
    None,

    /// <summary>Cloudflare Access Service Token authentication (CF-Access-Client-Id &amp; CF-Access-Client-Secret).</summary>
    Cloudflare,

    /// <summary>HTTP Bearer Token authentication (Authorization: Bearer &lt;token&gt;).</summary>
    Bearer,

    /// <summary>HTTP Basic authentication (Authorization: Basic &lt;base64(user:pass)&gt;).</summary>
    Basic,

    /// <summary>Custom API Key header authentication.</summary>
    ApiKey
}