using LocalCloudBridge.Models;
using LocalCloudBridge.Services;

namespace LocalCloudBridge.Mobile;

public partial class MainPage : ContentPage
{
    private PortableProxyServer? _server;
    private readonly List<string> _logLines = new();

    public MainPage()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void OnAuthTypeChanged(object? sender, EventArgs e)
    {
        string selected = AuthTypePicker.SelectedItem?.ToString() ?? "None";

        CloudflareLayout.IsVisible = selected == "Cloudflare";
        BearerLayout.IsVisible = selected == "Bearer";
        BasicLayout.IsVisible = selected == "Basic";
        ApiKeyLayout.IsVisible = selected == "ApiKey";
    }

    private async void OnStartStopClicked(object? sender, EventArgs e)
    {
        if (_server != null && _server.IsRunning)
        {
            _server.Stop();
            _server = null;
            UpdateStatus("OFFLINE", Colors.DarkGray, "#334155");
            StartStopButton.Text = "Start Proxy Bridge";
            StartStopButton.BackgroundColor = Color.FromArgb("#0284C7");
            AppendLog("Proxy bridge stopped.");
            return;
        }

        try
        {
            var options = BuildOptions();

            // Save configurations to mobile storage
            SaveSettings(options);

            _server = new PortableProxyServer(options);

            _server.OnLog += msg =>
            {
                MainThread.BeginInvokeOnMainThread(() => AppendLog(msg));
            };

            _server.OnStatusChanged += status =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (status == "Running")
                    {
                        UpdateStatus("RUNNING", Colors.LimeGreen, "#065F46");
                        StartStopButton.Text = "Stop Proxy Bridge";
                        StartStopButton.BackgroundColor = Color.FromArgb("#DC2626");
                    }
                    else if (status == "Error")
                    {
                        UpdateStatus("ERROR", Colors.Tomato, "#7F1D1D");
                    }
                    else
                    {
                        UpdateStatus("OFFLINE", Colors.DarkGray, "#334155");
                    }
                });
            };

            await _server.StartAsync();
        }
        catch (Exception ex)
        {
            AppendLog($"Failed to start: {ex.Message}");
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
    }

    private BridgeOptions BuildOptions()
    {
        int.TryParse(WolPortEntry.Text, out int wolPort);
        if (wolPort <= 0) wolPort = 9;

        Enum.TryParse<AuthenticationType>(
            AuthTypePicker.SelectedItem?.ToString() ?? "None",
            ignoreCase: true,
            out var authType);

        return new BridgeOptions
        {
            Listen = ListenEntry.Text.Trim(),
            Target = new TargetOptions
            {
                Name = TargetNameEntry.Text.Trim(),
                Url = TargetUrlEntry.Text.Trim().TrimEnd('/'),
                HealthCheck = HealthCheckEntry.Text.Trim()
            },
            Authentication = new AuthenticationOptions
            {
                Type = authType,
                ClientId = ClientIdEntry.Text?.Trim() ?? "",
                ClientSecret = ClientSecretEntry.Text?.Trim() ?? "",
                BearerToken = BearerTokenEntry.Text?.Trim() ?? "",
                Username = UsernameEntry.Text?.Trim() ?? "",
                Password = PasswordEntry.Text?.Trim() ?? "",
                ApiKeyHeader = ApiKeyHeaderEntry.Text?.Trim() ?? "",
                ApiKey = ApiKeyValueEntry.Text?.Trim() ?? ""
            },
            WakeOnLan = new WakeOnLanOptions
            {
                Enabled = WolSwitch.IsToggled,
                Host = WolHostEntry.Text?.Trim() ?? "",
                Port = wolPort,
                MacAddress = MacAddressEntry.Text?.Trim() ?? "",
                BroadcastIP = string.IsNullOrWhiteSpace(BroadcastIpEntry.Text) ? "255.255.255.255" : BroadcastIpEntry.Text.Trim()
            }
        };
    }

    private void SaveSettings(BridgeOptions options)
    {
        Preferences.Default.Set("Listen", options.Listen);
        Preferences.Default.Set("TargetName", options.Target.Name);
        Preferences.Default.Set("TargetUrl", options.Target.Url);
        Preferences.Default.Set("HealthCheck", options.Target.HealthCheck);
        Preferences.Default.Set("AuthType", options.Authentication.Type.ToString());
        Preferences.Default.Set("ClientId", options.Authentication.ClientId);
        Preferences.Default.Set("ClientSecret", options.Authentication.ClientSecret);
        Preferences.Default.Set("BearerToken", options.Authentication.BearerToken);
        Preferences.Default.Set("Username", options.Authentication.Username);
        Preferences.Default.Set("Password", options.Authentication.Password);
        Preferences.Default.Set("ApiKeyHeader", options.Authentication.ApiKeyHeader);
        Preferences.Default.Set("ApiKey", options.Authentication.ApiKey);
        Preferences.Default.Set("WolEnabled", options.WakeOnLan.Enabled);
        Preferences.Default.Set("WolMac", options.WakeOnLan.MacAddress);
        Preferences.Default.Set("WolHost", options.WakeOnLan.Host);
        Preferences.Default.Set("WolPort", options.WakeOnLan.Port);
        Preferences.Default.Set("BroadcastIp", options.WakeOnLan.BroadcastIP);
    }

    private void LoadSettings()
    {
        ListenEntry.Text = Preferences.Default.Get("Listen", "http://127.0.0.1:11435");
        TargetNameEntry.Text = Preferences.Default.Get("TargetName", "Ollama AI");
        TargetUrlEntry.Text = Preferences.Default.Get("TargetUrl", "https://subdomain.yourdomain.com");
        HealthCheckEntry.Text = Preferences.Default.Get("HealthCheck", "/api/tags");

        string authTypeStr = Preferences.Default.Get("AuthType", "Cloudflare");
        AuthTypePicker.SelectedItem = authTypeStr;

        ClientIdEntry.Text = Preferences.Default.Get("ClientId", "");
        ClientSecretEntry.Text = Preferences.Default.Get("ClientSecret", "");
        BearerTokenEntry.Text = Preferences.Default.Get("BearerToken", "");
        UsernameEntry.Text = Preferences.Default.Get("Username", "");
        PasswordEntry.Text = Preferences.Default.Get("Password", "");
        ApiKeyHeaderEntry.Text = Preferences.Default.Get("ApiKeyHeader", "X-API-Key");
        ApiKeyValueEntry.Text = Preferences.Default.Get("ApiKey", "");

        WolSwitch.IsToggled = Preferences.Default.Get("WolEnabled", true);
        MacAddressEntry.Text = Preferences.Default.Get("WolMac", "");
        WolHostEntry.Text = Preferences.Default.Get("WolHost", "");
        WolPortEntry.Text = Preferences.Default.Get("WolPort", 9).ToString();
        BroadcastIpEntry.Text = Preferences.Default.Get("BroadcastIp", "255.255.255.255");
    }

    private void UpdateStatus(string text, Color textColor, string hexBgColor)
    {
        StatusBadgeLabel.Text = text;
        StatusBadgeLabel.TextColor = textColor;
        StatusBadgeBorder.BackgroundColor = Color.FromArgb(hexBgColor);
    }

    private async void AppendLog(string message)
    {
        _logLines.Add(message);
        if (_logLines.Count > 100)
        {
            _logLines.RemoveAt(0);
        }

        LogLabel.Text = string.Join(Environment.NewLine, _logLines);

        // Auto-scroll to the bottom of the log view
        await Task.Delay(50);
        await LogScrollView.ScrollToAsync(LogLabel, ScrollToPosition.End, animated: true);
    }
}
