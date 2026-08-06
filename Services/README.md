# LocalCloudBridge.Services

This directory contains core application services implementing the reverse proxy pipeline, authentication injection, health check monitoring, and Wake-on-LAN (WOL) magic packet transmission for **LocalCloudBridge**.

Designed with **stateless static services**, **async/await non-blocking I/O**, and **zero third-party dependencies**, these services deliver maximum throughput and low latency.

---

## Service Architecture Overview

```
                        HttpContext (Incoming Request)
                                      │
                                      ▼
                        ProxyService.HandleAsync()
                                      │
           ┌──────────────────────────┴──────────────────────────┐
           ▼                                                     ▼
HttpRequestMessage Assembly                             AuthenticationService.Apply()
(Path, Query, Headers, Body)                            (Injects CF-Access, Bearer, etc)
           │                                                     │
           └──────────────────────────┬──────────────────────────┘
                                      ▼
                      HttpClient.SendAsync("proxy")
                   (ResponseHeadersRead for Streaming)
                                      │
                                      ▼
                         HttpResponse Streaming
                      (Body.CopyToAsync -> Response)
```

---

## Services Reference

### 1. `ProxyService` ([ProxyService.cs](file:///c:/cloudflared/LocalCloudBridge/Services/ProxyService.cs))
The core reverse proxy engine that routes HTTP requests between downstream clients and the upstream target service.

#### Key Mechanics:
- **URL & Query Reconstruction**: Combines `Target.Url` + `Request.Path` + `Request.QueryString`.
- **Header Forwarding**: Copies all incoming request headers to `HttpRequestMessage`, skipping the original `Host` header to allow `HttpClient` to set the correct upstream host header automatically.
- **Streaming Request & Response**:
  - Request bodies are wrapped via `StreamContent(context.Request.Body)` without buffering in memory.
  - Response streams use `HttpCompletionOption.ResponseHeadersRead` to begin streaming response chunks back to the client immediately (essential for Server-Sent Events and LLM token streaming).
- **Header Cleanup**: Copies response headers and content headers while stripping `transfer-encoding` to prevent HTTP protocol conflicts with Kestrel's chunked response writer.

---

### 2. `AuthenticationService` ([AuthenticationService.cs](file:///c:/cloudflared/LocalCloudBridge/Services/AuthenticationService.cs))
Injects authentication headers into outgoing `HttpRequestMessage` instances based on `BridgeOptions.Authentication.Type`.

#### Supported Strategies:
| Strategy | Injected Headers | Description |
|---|---|---|
| `None` | *None* | Passes requests without modification. |
| `Cloudflare` | `CF-Access-Client-Id`<br>`CF-Access-Client-Secret` | Authenticates against Cloudflare Access Zero Trust Service Tokens. |
| `Bearer` | `Authorization: Bearer <token>` | Applies standard HTTP Bearer token. |
| `Basic` | `Authorization: Basic <base64>` | Generates Base64 encoded `Username:Password` header. |
| `ApiKey` | Custom Header Name & Value | Applies arbitrary key-value headers (e.g., `X-API-Key: secret`). |

---

### 3. `HealthChecker` ([HealthChecker.cs](file:///c:/cloudflared/LocalCloudBridge/Services/HealthChecker.cs))
Monitors target service availability during startup before starting the Kestrel listener.

#### Operational Flow:
1. Constructs an HTTP GET request to `Target.Url + Target.HealthCheck`.
2. Applies authentication via `AuthenticationService.Apply()`.
3. Sends request using the `"proxy"` named `HttpClient`.
4. **On Success (`2xx` status code)**: Logs status and proceeds to start Kestrel.
5. **On Failure (Target Offline)**:
   - If `WakeOnLan.Enabled == true` and WOL hasn't been sent yet, invokes `WakeOnLan.SendAsync()`.
   - Loops and retries every **30 seconds** until the target service returns a successful HTTP response.

---

### 4. `WakeOnLan` ([WakeOnLan.cs](file:///c:/cloudflared/LocalCloudBridge/Services/WakeOnLan.cs))
Generates and broadcasts UDP Magic Packets to wake up target machines over local networks (WOL) or the internet (Wake-on-WAN).

#### Packet Construction:
A standard 102-byte WOL Magic Packet consists of:
- **6 bytes of `0xFF`** synchronizing header.
- **16 repetitions** of the target machine's 6-byte MAC address.

#### Transmission Strategy:
1. **Wake-on-WAN (Unicast)**: If `WakeOnLan.Host` is provided, performs IPv4 DNS resolution via `Dns.GetHostAddressesAsync()` and sends a UDP unicast packet to `(IPv4, Port)`.
2. **Wake-on-LAN (Subnet Broadcast)**: Parses `WakeOnLan.BroadcastIP` (falling back to `IPAddress.Broadcast`) and sends a UDP broadcast packet to `(BroadcastIP, Port)`.

---

## Developer Guide: Extending Services

### Modifying Header Transformation Rules
To strip or inject custom headers (e.g., forwarding client real IP addresses):

Edit `ProxyService.cs`:
```csharp
// Forward client IP to upstream service
if (context.Connection.RemoteIpAddress != null)
{
    request.Headers.TryAddWithoutValidation("X-Forwarded-For", context.Connection.RemoteIpAddress.ToString());
}
```

### Customizing Health Check Retry Interval
Edit `HealthChecker.cs` to adjust polling backoff:
```csharp
// Change default 30-second delay to custom interval
await Task.Delay(TimeSpan.FromSeconds(10));
```
