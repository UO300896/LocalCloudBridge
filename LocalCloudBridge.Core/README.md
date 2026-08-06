# LocalCloudBridge.Core

`LocalCloudBridge.Core` is a framework-agnostic, zero-dependency .NET 8 class library that encapsulates the shared proxy engine, authentication injection, health check probing, and Wake-on-LAN (WoL) broadcasting logic for **LocalCloudBridge**.

It is shared between both host applications:
- **`LocalCloudBridge.Server`** (Desktop/Server Kestrel host)
- **`LocalCloudBridge.Mobile`** (.NET MAUI Android/iOS host)

---

## Directory Structure

```text
LocalCloudBridge.Core/
├── Models/
│   ├── AuthenticationType.cs   # Supported authentication schemes enum
│   └── BridgeOptions.cs        # Strongly-typed configuration options & loader
├── Services/
│   ├── AuthenticationService.cs# Auth header injection logic
│   ├── HealthChecker.cs        # Upstream service availability probe
│   ├── PortableProxyServer.cs  # HttpListener-based lightweight proxy server
│   └── WakeOnLan.cs            # UDP Magic Packet generator & broadcaster
├── BridgeEngine.cs             # Core HTTP request/response forwarder
└── LocalCloudBridge.Core.csproj
```

---

## Key Components

### 1. `BridgeEngine.cs`
Framework-agnostic HTTP forwarding logic. Builds outgoing `HttpRequestMessage` objects, transfers streaming request bodies, applies authentication via `AuthenticationService`, and streams upstream responses back to caller streams.

### 2. `PortableProxyServer.cs`
A lightweight, portable HTTP server built using `HttpListener`. It enables full reverse proxy capability on platforms where full ASP.NET Core Kestrel is not lightweight or native (such as mobile devices running .NET MAUI).

### 3. `Services/HealthChecker.cs`
Probes target service health before starting the HTTP listener. Triggers `WakeOnLan` when target is unreachable and logs connection progression. Supports injected delegate logging (`Action<string>`) for real-time mobile console output.

### 4. `Services/WakeOnLan.cs`
Generates 102-byte Magic Packets and broadcasts them over UDP. Supports platform-specific extensions (such as Android Wi-Fi Multicast Lock acquisition delegates).

---

## Usage

Reference `LocalCloudBridge.Core` in any .NET project:

```xml
<ItemGroup>
    <ProjectReference Include="..\LocalCloudBridge.Core\LocalCloudBridge.Core.csproj" />
</ItemGroup>
```
