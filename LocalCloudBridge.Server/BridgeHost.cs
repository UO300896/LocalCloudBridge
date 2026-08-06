using LocalCloudBridge.Models;
using LocalCloudBridge.Services;
using Microsoft.AspNetCore.Http.Features;
using System.Net;

namespace LocalCloudBridge.Server;

public sealed class BridgeHost
{
    public async Task StartAsync(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        BridgeOptions options = BridgeOptions.Load(builder.Configuration);

        builder.WebHost.UseUrls(options.Listen);

        builder.Services.Configure<FormOptions>(o =>
        {
            o.MultipartBodyLengthLimit = long.MaxValue;
        });

        builder.Services
            .AddHttpClient("proxy")
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                return new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    AutomaticDecompression = DecompressionMethods.All,
                    PooledConnectionLifetime = TimeSpan.FromMinutes(10)
                };
            });

        var app = builder.Build();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\nStopping bridge...");
            Console.ResetColor();

            Environment.Exit(0);
        };

        app.Use(async (ctx, next) =>
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} {ctx.Request.Method} {ctx.Request.Path}");
            Console.ResetColor();

            await next();
        });

        app.MapGet("/", () => Results.Json(new
        {
            status = "ok",
            service = options.Target.Name,
            target = options.Target.Url,
            health = options.Target.HealthCheck
        }));

        app.MapGet("/health", async (IHttpClientFactory factory) =>
        {
            try
            {
                var client = factory.CreateClient("proxy");

                var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    options.Target.Url + options.Target.HealthCheck);

                AuthenticationService.Apply(request, options);

                var response = await client.SendAsync(request);

                return Results.Json(new
                {
                    connected = response.IsSuccessStatusCode,
                    status = (int)response.StatusCode,
                    service = options.Target.Name
                });
            }
            catch (Exception ex)
            {
                return Results.Json(new
                {
                    connected = false,
                    error = ex.Message
                });
            }
        });

        app.Map("/{**path}", async (
            HttpContext context,
            IHttpClientFactory factory) =>
        {
            await ProxyService.HandleAsync(
                context,
                factory,
                options);
        });

        await HealthChecker.WaitUntilOnlineAsync(
            app.Services.GetRequiredService<IHttpClientFactory>(),
            options);

        await app.RunAsync();
    }
}