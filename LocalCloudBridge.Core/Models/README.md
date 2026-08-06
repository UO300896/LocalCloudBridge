# LocalCloudBridge.Models

This directory contains the domain models, configuration objects, and enumeration types for **LocalCloudBridge**.

Designed with **C# 12 / .NET 8** immutability patterns (`init` properties) and strong typing, these models serve as the single source of truth for runtime configuration, authentication strategies, target endpoints, and Wake-on-LAN parameters.

---

## Component Architecture

```
                       IConfiguration (appsettings.json)
                                      │
                                      ▼
                             BridgeOptions.Load()
                                      │
       ┌──────────────────────────────┼──────────────────────────────┐
       ▼                              ▼                              ▼
 TargetOptions               AuthenticationOptions            WakeOnLanOptions
 (Name, Url, HealthCheck)   (Type, Tokens, Keys, Auth)     (Host, Port, MAC, Broadcast)
                                      │
                                      ▼
                             AuthenticationType
                    (None, Cloudflare, Bearer, Basic, ApiKey)
```

---

## Models Reference

### 1. `BridgeOptions` ([BridgeOptions.cs](file:///c:/cloudflared/LocalCloudBridge/Models/BridgeOptions.cs))
The root configuration object holding all operational settings for the proxy bridge.

- **`Listen`** (`string`): The local address and port Kestrel binds to (e.g., `http://127.0.0.1:11435`).
- **`Target`** (`TargetOptions`): Configuration for the remote service.
- **`Authentication`** (`AuthenticationOptions`): Credentials and type of authentication applied to proxied requests.
- **`WakeOnLan`** (`WakeOnLanOptions`): Magic packet broadcasting settings.

#### Static Factory Method
- **`BridgeOptions.Load(IConfiguration configuration)`**:
  Parses ASP.NET Core `IConfiguration` and builds a strongly-typed `BridgeOptions` instance.
  - Normalizes base URLs by stripping trailing slashes (`TrimEnd('/')`).
  - Sets safe defaults (e.g., fallback broadcast IP `255.255.255.255`, default UDP WOL port `9`).
  - Performs case-insensitive enum parsing for `AuthenticationType`.

---

### 2. `TargetOptions` ([BridgeOptions.cs](file:///c:/cloudflared/LocalCloudBridge/Models/BridgeOptions.cs#L78))
Defines the upstream target endpoint.

- **`Name`** (`string`): Human-readable name used in console logging.
- **`Url`** (`string`): Public or remote URL of the target service (e.g., `https://subdomain.yourdomain.com`).
- **`HealthCheck`** (`string`): Relative HTTP path used by `HealthChecker` to probe target availability (e.g., `/api/tags`).

---

### 3. `AuthenticationType` ([AuthenticationType.cs](file:///c:/cloudflared/LocalCloudBridge/Models/AuthenticationType.cs))
Enumeration representing supported authentication strategies:

```csharp
public enum AuthenticationType
{
    None,       // Direct proxying without authentication headers
    Cloudflare, // Cloudflare Access Service Tokens (CF-Access-Client-Id & CF-Access-Client-Secret)
    Bearer,     // HTTP Bearer Token (Authorization: Bearer <token>)
    Basic,      // HTTP Basic Auth (Authorization: Basic <base64(user:pass)>)
    ApiKey      // Custom HTTP Header (e.g., X-API-Key: <key>)
}
```

---

### 4. `AuthenticationOptions` ([BridgeOptions.cs](file:///c:/cloudflared/LocalCloudBridge/Models/BridgeOptions.cs#L92))
Contains authentication credentials. Only fields matching the selected `AuthenticationType` are used at runtime.

- **`Type`** (`AuthenticationType`): Selected authentication strategy.
- **`ClientId`** / **`ClientSecret`**: Credentials for `AuthenticationType.Cloudflare`.
- **`BearerToken`**: Secret token for `AuthenticationType.Bearer`.
- **`Username`** / **`Password`**: Credentials for `AuthenticationType.Basic`.
- **`ApiKeyHeader`** / **`ApiKey`**: Custom header name and key value for `AuthenticationType.ApiKey`.

---

### 5. `WakeOnLanOptions` ([BridgeOptions.cs](file:///c:/cloudflared/LocalCloudBridge/Models/BridgeOptions.cs#L124))
Options controlling local broadcast and remote unicast (Wake-on-WAN) magic packets.

- **`Enabled`** (`bool`): Master flag to trigger WOL when target health check fails.
- **`Host`** (`string`): Optional DDNS hostname or public IP for Wake-on-WAN.
- **`Port`** (`int`): UDP port for WOL transmission (default: `9`).
- **`MacAddress`** (`string`): MAC address of target host (`AA:BB:CC:DD:EE:FF` or `AA-BB-CC-DD-EE-FF`).
- **`BroadcastIP`** (`string`): Subnet broadcast IP address (default: `255.255.255.255`).

---

## Developer Guide: Adding a New Authentication Type

To implement a new authentication strategy (e.g., `OAuth2` or `CustomHmac`):

1. **Update `AuthenticationType.cs`**:
   ```csharp
   public enum AuthenticationType
   {
       // ... existing types
       CustomHmac
   }
   ```

2. **Extend `AuthenticationOptions` in `BridgeOptions.cs`**:
   ```csharp
   public class AuthenticationOptions
   {
       // ... existing properties
       public string HmacHeader { get; init; } = string.Empty;
       public string HmacSecret { get; init; } = string.Empty;
   }
   ```

3. **Map Properties in `BridgeOptions.Load()`**:
   ```csharp
   HmacHeader = configuration["Authentication:HmacHeader"] ?? "",
   HmacSecret = configuration["Authentication:HmacSecret"] ?? ""
   ```

4. **Implement the authentication scheme in AuthenticationService.cs**:
   ```csharp
   case AuthenticationType.CustomHmac:
       request.Headers.Add(options.Authentication.HmacHeader, CalculateHmac(options.Authentication.HmacSecret));
       break;
   ```
