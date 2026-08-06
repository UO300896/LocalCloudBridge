# LocalCloudBridge

[![NET 8.0](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![License](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)

**LocalCloudBridge** is a lightweight **reverse proxy** written in **C#/.NET 8** that exposes a **local HTTP endpoint** for securely accessing **remote HTTP services**. It transparently injects authentication credentials, forwards requests, and can **automatically wake the remote host before establishing the connection.**

---

## Overview

Imagine you have a **self-hosted AI inference server**, such as **Ollama**, running on your home PC. You want to **securely access** your **local LLMs** from **anywhere over the Internet**, allowing your applications, **IDE extensions**, and **AI tools** to use them just like any **cloud-hosted AI API**—while keeping the models and hardware **entirely under your control**.

A common approach is to **expose the service** through a **secure reverse tunnel** protected by a **Zero Trust gateway** such as **Cloudflare Access**. This makes the service securely **accessible over the Internet** while ensuring that only authenticated users or approved service tokens can access it. **Unlike exposing ports directly**, your service remains protected behind Cloudflare's Zero Trust authentication layer.

The problem is that many **CLI tools**, **IDE extensions**, **desktop applications**, and **mobile apps** cannot complete **browser-based MFA flows** or **inject the authentication headers** required by Cloudflare Access. As a result, they cannot communicate with the protected service directly. And if your **home server is sleeping or powered off**, the service is unavailable until the machine is woken up.

**LocalCloudBridge solves both problems:**

1. **Transparent Authentication** – It runs locally and automatically injects the required authentication credentials (**Cloudflare Access Service Tokens**, **Bearer Tokens**, **HTTP Basic Auth**, **API Keys**, or none) into every outgoing request.
2. **Automatic Wake-on-LAN** – If the target **machine** is sleeping or powered off, it automatically sends a **Wake-on-LAN (WOL) or Wake-on-WAN (WoW) Magic Packet** and waits until the service becomes available.
3. **Zero Client Modifications** – Existing CLI tools, desktop applications, IDE extensions and mobile apps **simply connect to a local endpoint** such as `http://127.0.0.1:11435`.

This allows your **self-hosted services** to behave like **cloud-hosted APIs**, while remaining under your control and protected behind a **Zero Trust gateway**.

In many scenarios, LocalCloudBridge can also be used as an alternative to connecting your **entire private network** through a **VPN** or solutions such as **Tailscale**. Instead of granting **network-level access**, **only the specific HTTP service** is securely exposed, authenticated, and proxied.

## Why?

Most **self-hosted AI services** are either only available on the local network or require a **VPN** to access remotely. Exposing them securely through **Cloudflare Zero Trust** solves the networking problem, but many **applications cannot authenticate** against **Cloudflare Access**. LocalCloudBridge fills that gap by acting as a **local reverse proxy** that transparently **authenticates requests** and optionally **wakes the remote machine** when needed.

## Typical Use Cases

- **Remote Ollama**: Run **LLM inferences** on your home **GPU server** from your **laptop while traveling**.
- **Remote Open WebUI & AI Frontends**: Connect local **AI interfaces** to **remote backends**.
- **Home Assistant & Smart Home APIs**: Interact with **home automation APIs** securely from **external networks**.
- **Immich & Media Services**: Access **self-hosted photo/video APIs** remotely.
- **Private REST APIs**: Proxy **developer tooling** and requests to **internal microservices**.
- **AI IDE Extensions**: Use extensions (**Cline**, **Continue**, **Roo Code**, etc.) with **remote LLM backends** **without changing extension code**.
- **Local Desktop Applications**: Any **desktop app** or **CLI** that expects a local HTTP endpoint.

---

## Key Features

- **Pluggable Authentication**: Supports **Cloudflare Access Service Tokens** (`CF-Access-Client-Id` & `CF-Access-Client-Secret`), **HTTP Bearer Tokens**, **HTTP Basic Auth**, **Custom API Keys**, or **unauthenticated forwarding**. The modular codebase makes it easy to add custom auth schemes.
- **Automated Health Monitoring & Auto-Wake**: Monitors upstream target health at startup. If unreachable, broadcasts **UDP Magic Packets** (local **WOL** broadcast or remote **DDNS Wake-on-WAN**) and waits until the target host powers on.
- **Full HTTP Streaming & Large Payloads**: Supports **chunked transfer encoding**, **streaming responses** (**Server-Sent Events / LLM tokens**), and **unlimited request body upload limits** (ideal for uploading large AI model weights).
- **Single Executable Deployment**: Ships as a **single**, self-contained binary (`.exe`) with **Zero Third-Party Dependencies**.
- **Transparent Reverse Proxy**: Preserves **HTTP methods**, **headers**, **query strings**, **request bodies** and **streaming responses** without requiring application changes.
---

## How It Works

```
┌────────────────────────────────┐
│  Local Application / Client    │
│  (e.g., Ollama CLI, VS Code)   │
└───────────────┬────────────────┘
                │ Local HTTP (http://127.0.0.1:11435)
                ▼
┌────────────────────────────────┐
│       LocalCloudBridge         │
│  • Injects Auth Credentials    │
│  • Monitors Upstream Health    │
│  • Triggers WOL if offline     │
└───────────────┬────────────────┘
                │ Authenticated Connection
                ▼ (Cloudflare, Bearer, Basic, API Key, None)
┌────────────────────────────────┐
│     Remote Gateway / Tunnel    │
└───────────────┬────────────────┘
                │ Internal Network
                ▼
┌────────────────────────────────┐
│        Upstream Service        │
│  (Ollama, Home Assistant, etc) │
└────────────────────────────────┘
```

---

## Project Structure

```text
LocalCloudBridge/
├── Models/                      # Data transfer objects and configuration models
│   ├── AuthenticationType.cs   # Enum defining supported authentication strategies
│   ├── BridgeOptions.cs        # Root configuration classes and appsettings.json loader
│   └── README.md               # Developer documentation for the Models layer
├── Services/                    # Core proxy logic, auth injection, health checking, and WOL
│   ├── AuthenticationService.cs# Applies configured authentication headers to outgoing HTTP requests
│   ├── HealthChecker.cs        # Probes target service health at startup and triggers WOL if offline
│   ├── ProxyService.cs          # Core reverse proxy handling request forwarding and response streaming
│   ├── WakeOnLan.cs           # Builds and transmits UDP Magic Packets (WOL/WoW) over LAN or DDNS
│   └── README.md               # Developer documentation for the Services layer
├── .gitignore                   # Specifies untracked files (e.g. appsettings.json, build outputs)
├── appsettings.example.json     # Template configuration file with example parameters
├── LocalCloudBridge.csproj      # .NET 8 project file with single-file publish configuration
├── Program.cs                   # Application entry point configuring ASP.NET Core Minimal API routes
└── README.md                    # This file.
```

---

## Configuration (`appsettings.json`)

To configure LocalCloudBridge, copy `appsettings.example.json` to `appsettings.json` and adjust the settings to match your environment.

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

### Options Breakdown

| Section | Setting | Type | Description |
|---|---|---|---|
| **Root** | `Listen` | String | Local HTTP URL and port for the bridge to listen on (e.g., `http://127.0.0.1:11435`). |
| **Target** | `Name` | String | Friendly display name for logging purposes. |
| | `Url` | String | Base URL of the remote service or domain (e.g., `https://subdomain.yourdomain.com`). |
| | `HealthCheck` | String | Endpoint path used to verify remote service availability (e.g., `/api/tags` or `/health`). |
| **Authentication** | `Type` | String | Auth strategy: `None`, `Cloudflare`, `Bearer`, `Basic`, or `ApiKey`. |
| | `ClientId` | String | `CF-Access-Client-Id` header value (for `Cloudflare` type). |
| | `ClientSecret` | String | `CF-Access-Client-Secret` header value (for `Cloudflare` type). |
| | `BearerToken` | String | Token value for `Authorization: Bearer <token>` (for `Bearer` type). |
| | `Username` | String | Username for `Authorization: Basic <base64>` (for `Basic` type). |
| | `Password` | String | Password for `Authorization: Basic <base64>` (for `Basic` type). |
| | `ApiKeyHeader` | String | Custom header name (for `ApiKey` type, e.g., `X-API-Key`). |
| | `ApiKey` | String | Custom API key value (for `ApiKey` type). |
| **WakeOnLan** | `Enabled` | Boolean | Enables/disables Wake-on-LAN feature when the target service is unreachable. |
| | `Host` | String | *(Optional)* DDNS hostname or public IP for Wake-on-WAN unicast packet. |
| | `Port` | Integer | UDP port for WOL Magic Packet (default: `9`). |
| | `MacAddress` | String | MAC address of the target machine (format: `AA:BB:CC:DD:EE:FF` or `AA-BB-CC-DD-EE-FF`). |
| | `BroadcastIP` | String | Local subnet broadcast IP address (e.g., `192.168.1.255` or `255.255.255.255`). |

---

## Building and Running

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### Running in Development
```bash
# Clone the repository
git clone https://github.com/yourusername/LocalCloudBridge.git
cd LocalCloudBridge

# Restore dependencies & run
dotnet run
```

### Publishing as a Standalone Single Executable (Windows x64)
The project file (`.csproj`) is pre-configured to generate a trimmed, self-contained, single-file executable for Windows x64 without debug symbols:

```bash
dotnet publish
```

The output executable will be generated at:
`bin/Release/net8.0/win-x64/publish/LocalCloudBridge.exe`

To compile for other platforms, override the runtime identifier:
```bash
dotnet publish -c Release -r linux-x64 
dotnet publish -c Release -r osx-arm64
```

---

## Usage Example: Connecting Remote Ollama

To access your remote Ollama instance securely over the Internet via Cloudflare Access:

### Step 1: Configure Cloudflare Tunnel (`cloudflared`) on Remote Server
Expose your local Ollama instance (defaulting to port `11434`) to a public domain (e.g., `subdomain.yourdomain.com`) using Cloudflare Tunnels:

#### Option A: Via Cloudflare Zero Trust Dashboard (Recommended)
1. Log in to the [Cloudflare Zero Trust Dashboard](https://one.dash.cloudflare.com/).
2. Go to **Networks** -> **Tunnels** and click **Add a tunnel**.
3. Select **Cloudflared** as the connector and name your tunnel (e.g., `home-ollama`).
4. Follow the on-screen command to run `cloudflared` on your remote server host.
5. In **Public Hostname**, route `subdomain.yourdomain.com` to `HTTP` `127.0.0.1:11434`.

#### Option B: Via Command Line (`config.yml`)
```bash
cloudflared tunnel create ollama-tunnel
cloudflared tunnel route dns ollama-tunnel subdomain.yourdomain.com
```
In `~/.cloudflared/config.yml`:
```yaml
tunnel: <YOUR-TUNNEL-UUID>
credentials-file: /root/.cloudflared/<YOUR-TUNNEL-UUID>.json

ingress:
  - hostname: subdomain.yourdomain.com
    service: http://127.0.0.1:11434
  - service: http_status:404
```
Start the tunnel:
```bash
cloudflared tunnel run ollama-tunnel
```

---

### Step 2: Prepare Remote Ollama Host Service
Set `OLLAMA_HOST` on your **remote server** to accept requests:
```bash
export OLLAMA_HOST="*"
ollama serve
```

---

### Step 3: Configure `LocalCloudBridge` on Client Machine

1. Create `appsettings.json` next to `LocalCloudBridge.exe`:
   ```json
   {
     "Listen": "http://127.0.0.1:11435",
     "Target": {
       "Name": "Ollama",
       "Url": "https://subdomain.yourdomain.com",
       "HealthCheck": "/api/tags"
     },
     "Authentication": {
       "Type": "Cloudflare",
       "ClientId": "your-client-id.access",
       "ClientSecret": "your-client-secret"
     }
   }
   ```

2. Point your local CLI or tools to the bridge (`http://127.0.0.1:11435`):
   - **Command Prompt (CMD)**:
     ```cmd
     set OLLAMA_HOST=127.0.0.1:11435
     ollama run llama3
     ```
   - **PowerShell**:
     ```powershell
     $env:OLLAMA_HOST="127.0.0.1:11435"
     ollama run llama3
     ```
   - **IDE Extensions (e.g., Cline, Continue)**: Set "Use custom base URL" to `http://127.0.0.1:11435` with provider set to Ollama.

3. Run `LocalCloudBridge.exe`. It verifies upstream availability (waking the target host via WOL if offline) and proxies all incoming traffic transparently.

   **Example Output:**
   ```text
   ---------------------------------------
   LocalCloudBridge
   ---------------------------------------

   Connecting to: https://subdomain.yourdomain.com
   Checking Cloudflare tunnel...

   OK Tunnel reachable

   Checking Ollama...
   OK Connected
   OK Ready

   Listening on:
   http://127.0.0.1:11435
   
   info: Microsoft.Hosting.Lifetime[14]
         Now listening on: http://127.0.0.1:11435
   info: Microsoft.Hosting.Lifetime[0]
         Application started. Press Ctrl+C to shut down.
   ```

---

## Additional Guides

### Authentication Example: Cloudflare Access Service Tokens

When protecting a self-hosted service with **Cloudflare Zero Trust (Cloudflare Access)**, human users log in via browser-based Identity Providers (Google, GitHub, SSO, MFA). Automated tools, however, require a **Service Token** to authenticate programmatically via HTTP headers. ### Step-by-Step Setup in Cloudflare Zero Trust:

 1. **Log in to Cloudflare Zero Trust Dashboard**: 
 - Go to [Zero Trust Dashboard](https://one.dash.cloudflare.com/). 
 
 2. **Create a Service Token**: 
 - Navigate to **Access** -> **Service Tokens**. 
 - Click **Add a Service Token**. 
 - Enter a **Token Name** (e.g., LocalCloudBridge-Token) and choose a service token duration. 
 - Click **Save**. 

 3. **Copy Credentials**: 
 - Cloudflare will display a **Client ID** and a **Client Secret**. 
 - ⚠️ **Important**: Copy both values immediately. The secret will never be displayed again! 
 
 4. **Attach Service Token to Your Cloudflare Application Policy**: 
 - Navigate to **Access** -> **Applications**. 
 - Find your target application (e.g., subdomain.yourdomain.com) and click **Edit**. 
 - Under the **Policies** tab, click **Add Policy**. 
 - Configure the policy: 
  - **Action**: Service Token (or Non Identity) 
  - **Rule Type**: Service Token 
  - **Value**: Select the Service Token you created in Step 2. 
 - Save the policy. 
 
 5. **Configure appsettings.json**: 
 - Set "Type": "Cloudflare". 
 - Paste the Client ID into "ClientId". 
 - Paste the Client Secret into "ClientSecret". 
 
 --- 
 
 ### Wake-on-LAN (WOL) & Wake-on-WAN (WoW) Guide 
 
 #### What is Wake-on-LAN? 
 
 **Wake-on-LAN (WOL)** is a networking standard that allows a computer to be turned on or woken up remotely by a network message called a **Magic Packet**. A Magic Packet is a UDP frame containing 6 bytes of 0xFF followed by 16 repetitions of the target network card's MAC address. 
 **Wake-on-WAN (WoW)** extends this across the internet by sending the Magic Packet to a router's public IP address or Dynamic DNS (DDNS) hostname, which forwards the UDP packet to the local broadcast network. 
 
 #### How to Enable Wake-on-LAN on Target Computer 
 
 1. **BIOS/UEFI Settings (Target PC)** 
 
 - Reboot the target computer and enter the **BIOS/UEFI setup** (usually by pressing DEL, F2, or F12 during startup). 
 - Locate power management settings (often under *Advanced*, *Power Management*, or *APM Configuration*). 
 - Enable options such as: 
  - **Power On By PCI-E/PCI Device** 
  - **Resume by LAN** or **Wake on LAN** 
 - Disable power-saving features that completely cut standby power to Ethernet ports: 
  - Disable **ErP Ready** / **EuP Ready** / **Deep Sleep State**. 
 - Save changes and exit. 
 
 2. **Windows Network Adapter Settings (Target PC)**     

 - Press Win + X and select **Device Manager**.
 - Expand **Network adapters**, right-click your Ethernet card (e.g., Realtek, Intel), and select **Properties**.
 - Go to the **Advanced** tab: 
   - Set **Wake on Magic Packet** to **Enabled**.
   - Set **Shutdown Wake-On-LAN** to **Enabled**. 
 - Go to the **Power Management** tab: 
   - Check **Allow this device to wake the computer**. 
   - Check **Only allow a magic packet to wake the computer**. 
 - **Disable Windows Fast Startup**: 
   - Fast Startup can put Windows into a hybrid shutdown state (S4) where WOL does not respond. 
   - Open **Control Panel** -> **Power Options** -> **Choose what the power buttons do**. 
   - Click **Change settings that are currently unavailable**. 
   - Uncheck **Turn on fast startup**. Click **Save changes**. 
 
 3. **Linux Configuration (Target PC - Optional)** 
 
 - If your target machine runs Linux, ensure ethtool is configured: 
 
 ```bash
sudo apt install ethtool
sudo ethtool -s eth0 wol g
 ```
 - *(Replace eth0 with your network interface name).*
 
 4. **Router Setup for Remote Wake-on-WAN (Optional)** 

 - To wake your target PC over the Internet from outside your home network: 

 - Setup a **DDNS service** (e.g., No-IP, DuckDNS, Cloudflare DDNS) on your router and enter the host into "Host": "yourdomain.ddns.net". 
 - Configure **Port Forwarding** on your router: - Forward external UDP Port 9 to your target PC's IP or subnet broadcast IP (192.168.1.255). 
 

> [!NOTE]
> **VPN Compatibility Note**: Wake-on-LAN / Wake-on-WAN UDP magic packets **may fail** to reach the target host if your client computer is connected to an active **VPN** (especially corporate VPNs with strict broadcast filtering or split-tunneling policies). All HTTP proxying, streaming, and authentication features **will continue to work normally** through the VPN, but remote host power-on (WOL) **may be blocked** while the VPN connection is **active**.

---

## License

This project is licensed under the [MIT License](LICENSE).
