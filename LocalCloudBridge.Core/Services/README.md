# LocalCloudBridge.Services

This directory contains core application services implementing the reverse proxy pipeline, authentication injection, health check monitoring, and Wake-on-LAN (WOL) magic packet transmission for **LocalCloudBridge**.

Designed with **stateless static services**, **async/await non-blocking I/O**, and **zero third-party dependencies**, these services deliver maximum throughput and low latency.

---

## Service Architecture Overview

```text
                        Incoming HTTP Request (Desktop / Mobile)
                                          │
                                          ▼
                      BridgeEngine.ForwardRequestAsync()
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

### 1. `PortableProxyServer` ([PortableProxyServer.cs](file:///c:/cloudflared/LocalCloudBridge/LocalCloudBridge.Core/Services/PortableProxyServer.cs))
A portable `HttpListener`-based proxy server used primarily on cross-platform mobile hosts (.NET MAUI) or embedded environments where full Kestrel is unnecessary.

#### Key Features:
- Manages full lifecycle (Start/Stop).
- Delegates HTTP request forwarding to `BridgeEngine`.
- Supports real-time log callbacks (`Action<string>`) for UI log consoles.

---

### 2. `AuthenticationService` ([AuthenticationService.cs](file:///c:/cloudflared/LocalCloudBridge/LocalCloudBridge.Core/Services/AuthenticationService.cs))
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

### 3. `HealthChecker` ([HealthChecker.cs](file:///c:/cloudflared/LocalCloudBridge/LocalCloudBridge.Core/Services/HealthChecker.cs))
Monitors target service availability during startup before starting the proxy listener.

#### Operational Flow:
1. Constructs an HTTP GET request to `Target.Url + Target.HealthCheck`.
2. Applies authentication via `AuthenticationService.Apply()`.
3. Sends request using the `"proxy"` named `HttpClient`.
4. **On Success (`2xx` status code)**: Logs status and proceeds to start listener.
5. **On Failure (Target Offline)**:
   - If `WakeOnLan.Enabled == true` and WOL hasn't been sent yet, invokes `WakeOnLan.SendAsync()`.
   - Loops and retries every **30 seconds** until the target service returns a successful HTTP response.

---

### 4. `WakeOnLan` ([WakeOnLan.cs](file:///c:/cloudflared/LocalCloudBridge/LocalCloudBridge.Core/Services/WakeOnLan.cs))
Generates and broadcasts UDP Magic Packets to wake up target machines over local networks (WOL) or the internet (Wake-on-WAN).

#### Android Compatibility:
Includes support for `AcquireMulticastLock` static delegates so mobile hosts can hold native Wi-Fi Multicast locks while transmitting UDP broadcasts.
