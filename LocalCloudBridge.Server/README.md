# LocalCloudBridge.Server

`LocalCloudBridge.Server` is the desktop and server host for **LocalCloudBridge**, built on **ASP.NET Core (Kestrel)**.

It loads settings from `appsettings.json`, verifies upstream target health, triggers Wake-on-LAN if necessary, and hosts an ultra-fast local reverse proxy endpoint.

---

## Directory Structure

```text
LocalCloudBridge.Server/
├── BridgeHost.cs               # Kestrel server builder & health check orchestrator
├── ProxyService.cs             # ASP.NET Core HttpContext proxy handler middleware
├── Program.cs                  # Entry point delegating to BridgeHost
├── appsettings.json            # Runtime configuration file
└── LocalCloudBridge.Server.csproj
```

---

## Building & Publishing

### Publish as Single Binary Executable (Windows)

To produce a single, self-contained `.exe` file (with high-resolution embedded application icon):

```bash
dotnet publish LocalCloudBridge.Server
```

The resulting executable `LocalCloudBridge.exe` can be placed anywhere along with `appsettings.json`.

### Publish for Linux or macOS

```bash
# Linux x64
dotnet publish LocalCloudBridge.Server/LocalCloudBridge.Server.csproj -c Release -r linux-x64 --self-contained

# macOS Apple Silicon
dotnet publish LocalCloudBridge.Server/LocalCloudBridge.Server.csproj -c Release -r osx-arm64 --self-contained
```

---

## Configuration (`appsettings.json`)

```json
{
  "Listen": "http://127.0.0.1:11435",
  "Target": {
    "Name": "Ollama AI",
    "Url": "https://subdomain.yourdomain.com",
    "HealthCheck": "/api/tags"
  },
  "Authentication": {
    "Type": "Cloudflare",
    "ClientId": "your-service-token-client-id.access",
    "ClientSecret": "your-service-token-client-secret"
  },
  "WakeOnLan": {
    "Enabled": true,
    "Host": "yourdomain.ddns.net",
    "Port": 9,
    "MacAddress": "AA:BB:CC:DD:EE:FF",
    "BroadcastIP": "192.168.1.255"
  }
}
```
