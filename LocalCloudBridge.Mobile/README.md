# LocalCloudBridge.Mobile

`LocalCloudBridge.Mobile` is the cross-platform mobile application for **LocalCloudBridge**, built with **.NET MAUI** (.NET 10).

It allows mobile devices (Android, iOS) to host a local HTTP reverse proxy server on `http://127.0.0.1:<port>`, transparently injecting credentials (Cloudflare Access, Bearer, Basic, API Key) and sending Wake-on-LAN Magic Packets to remote servers directly from your mobile device.

---

## Directory Structure

```text
LocalCloudBridge.Mobile/
├── Platforms/
│   └── Android/
│       └── AndroidManifest.xml # Android permissions (Multicast lock, Internet) & App Icon
├── Resources/
│   ├── AppIcon/                # App icon assets (fav.png)
│   └── Splash/                 # Custom dark splash screen logo (fav_splash.png)
├── MainPage.xaml               # Mobile UI (Dark mode layout, form inputs & log console)
├── MainPage.xaml.cs            # Event handlers, log auto-scroll & Preferences persistence
├── MauiProgram.cs              # MAUI application builder & Android Multicast Lock binding
└── LocalCloudBridge.Mobile.csproj
```

---

## Key Features

1. **Native Settings Persistence**: All input fields (Port, Remote URL, HealthCheck, Authentication credentials, WoL MAC/IP/Host) are automatically saved via `Microsoft.Maui.Storage.ISecureStorage` and restored on application startup.
2. **Real-time Log Console with Auto-Scroll**: Log outputs automatically scroll to the latest entry asynchronously as proxy requests flow through the bridge.
3. **Android Wi-Fi Multicast Lock Integration**: Uses Android native `WifiManager.MulticastLock` wired in `MauiProgram.cs` to prevent the Android OS from dropping UDP Magic Packets during Wake-on-LAN operations over Wi-Fi.
4. **Custom Branding**: Includes custom application icon and dark-themed splash screen configured in `LocalCloudBridge.Mobile.csproj`.

---

## Compiling & Exporting to Android APK

To build, sign, and publish the mobile application into a standalone **Android APK**, execute the following command from the repository root:

```bash
dotnet publish LocalCloudBridge.Mobile
```

### Build Artifact Output
- The MSBuild build target automatically renames the final signed package to **`LocalCloudBridge.apk`** and cleans up temporary unsigned packages.
- Primary output path:
  ```text
  LocalCloudBridge.Mobile/bin/Release/net10.0-android/publish/LocalCloudBridge.apk
  ```

---

## Technical Notes

> [!WARNING]
> **VPN Note**: If your mobile device is connected to a corporate VPN, UDP broadcast packets (Wake-on-LAN) may be blocked by the VPN adapter. All proxying and authentication features will continue to work normally over VPN.
