using System.Net.Http.Headers;
using LocalCloudBridge.Models;
using LocalCloudBridge.Services;

namespace LocalCloudBridge.Core;

public sealed class BridgeEngine
{
    private readonly IHttpClientFactory _factory;
    private readonly BridgeOptions _options;

    public BridgeEngine(
        IHttpClientFactory factory,
        BridgeOptions options)
    {
        _factory = factory;
        _options = options;
    }

    public HttpClient CreateClient()
    {
        return _factory.CreateClient("proxy");
    }

    public string BuildTargetUrl(string path, string query)
    {
        return _options.Target.Url + path + query;
    }

    public void ApplyAuthentication(HttpRequestMessage request)
    {
        AuthenticationService.Apply(request, _options);
    }

    public HttpRequestMessage CreateRequest(string method, string path, string query)
    {
        var request = new HttpRequestMessage(
            new HttpMethod(method),
            BuildTargetUrl(path, query));

        ApplyAuthentication(request);

        return request;
    }
}