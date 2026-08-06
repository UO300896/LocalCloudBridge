using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using LocalCloudBridge.Models;

namespace LocalCloudBridge.Services;

/// <summary>
/// Applies the configured authentication scheme to outgoing proxy requests.
/// </summary>
public static class AuthenticationService
{
    /// <summary>
    /// Applies configured authentication credentials to an HTTP request message.
    /// </summary>
    /// <param name="request">The outgoing HTTP request message.</param>
    /// <param name="options">Bridge configuration options containing authentication details.</param>
    public static void Apply(HttpRequestMessage request, BridgeOptions options)
    {
        switch (options.Authentication.Type)
        {
            case AuthenticationType.None:
                break;

            case AuthenticationType.Cloudflare:
                // Inject Cloudflare Access Service Token headers
                request.Headers.Remove("CF-Access-Client-Id");
                request.Headers.Remove("CF-Access-Client-Secret");

                if (!string.IsNullOrEmpty(options.Authentication.ClientId))
                {
                    request.Headers.Add("CF-Access-Client-Id", options.Authentication.ClientId);
                }

                if (!string.IsNullOrEmpty(options.Authentication.ClientSecret))
                {
                    request.Headers.Add("CF-Access-Client-Secret", options.Authentication.ClientSecret);
                }
                break;

            case AuthenticationType.Bearer:
                // Inject HTTP Bearer authorization header
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    options.Authentication.BearerToken);
                break;

            case AuthenticationType.Basic:
                // Inject HTTP Basic authorization header
                string credentials = $"{options.Authentication.Username}:{options.Authentication.Password}";
                string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));

                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    base64);
                break;

            case AuthenticationType.ApiKey:
                // Inject custom API key header
                if (!string.IsNullOrEmpty(options.Authentication.ApiKeyHeader))
                {
                    request.Headers.Add(
                        options.Authentication.ApiKeyHeader,
                        options.Authentication.ApiKey);
                }
                break;

            default:
                throw new NotSupportedException(
                    $"Authentication type '{options.Authentication.Type}' is not supported.");
        }
    }
}