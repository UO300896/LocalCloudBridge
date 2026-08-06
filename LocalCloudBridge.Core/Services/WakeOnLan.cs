using System.Net;
using System.Net.Sockets;
using LocalCloudBridge.Models;

namespace LocalCloudBridge.Services;

/// <summary>
/// Utility for constructing and broadcasting Wake-on-LAN (WOL) magic packets.
/// </summary>
public static class WakeOnLan
{
    /// <summary>
    /// Global delegate to acquire a multicast lock on mobile platforms.
    /// </summary>
    public static Func<IDisposable?>? AcquireMulticastLock { get; set; }

    /// <summary>
    /// Constructs a WOL magic packet and sends it via UDP to the configured broadcast address
    /// and/or unicast DDNS host (Wake-on-WAN).
    /// </summary>
    /// <param name="options">Bridge configuration options containing WakeOnLan parameters.</param>
    /// <param name="logAction">Optional custom logging callback.</param>
    public static async Task SendAsync(BridgeOptions options, Action<string>? logAction = null)
    {
        var wol = options.WakeOnLan;

        Action<string, ConsoleColor?> log = (msg, color) =>
        {
            if (logAction != null)
            {
                logAction(msg);
            }
            else
            {
                if (color.HasValue) Console.ForegroundColor = color.Value;
                Console.WriteLine(msg);
                if (color.HasValue) Console.ResetColor();
            }
        };

        if (string.IsNullOrWhiteSpace(wol.MacAddress))
        {
            log("[WoL] Warning: No MAC address specified in configuration.", ConsoleColor.Yellow);
            return;
        }

        // Clean MAC address string (remove delimiters like ':' and '-')
        string cleanMac = wol.MacAddress.Replace("-", "").Replace(":", "").Trim();

        if (cleanMac.Length != 12)
        {
            throw new FormatException($"[WoL] Invalid MAC address format: '{wol.MacAddress}'. Expected 12 hexadecimal characters.");
        }

        byte[] macBytes = new byte[6];
        for (int i = 0; i < 6; i++)
        {
            macBytes[i] = Convert.ToByte(cleanMac.Substring(i * 2, 2), 16);
        }

        // Build Magic Packet (6 bytes of 0xFF followed by MAC address repeated 16 times)
        var packet = new byte[102];
        for (int i = 0; i < 6; i++)
        {
            packet[i] = 0xFF;
        }

        for (int i = 0; i < 16; i++)
        {
            Buffer.BlockCopy(macBytes, 0, packet, 6 + (i * 6), 6);
        }

        using var mLock = AcquireMulticastLock?.Invoke();
        try
        {
            using var udp = new UdpClient();
            udp.EnableBroadcast = true;

            // 1. Send via DDNS (Wake-on-WAN) if host is provided
            if (!string.IsNullOrWhiteSpace(wol.Host))
            {
                try
                {
                    var addresses = await Dns.GetHostAddressesAsync(wol.Host);
                    var ipv4 = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);

                    if (ipv4 != null)
                    {
                        await udp.SendAsync(
                            packet,
                            packet.Length,
                            new IPEndPoint(ipv4, wol.Port));

                        log($"[WoW] Packet sent to DDNS target ({wol.Host}): {ipv4}:{wol.Port}", null);
                    }
                    else
                    {
                        log($"[WoW] Warning: No IPv4 address resolved for host '{wol.Host}'", ConsoleColor.Yellow);
                    }
                }
                catch (Exception ex)
                {
                    log($"[WoW] Resolution failed for host '{wol.Host}': {ex.Message}", ConsoleColor.Yellow);
                }
            }

            // 2. Send via Local Subnet Broadcast
            try
            {
                IPAddress broadcast = IPAddress.TryParse(wol.BroadcastIP, out var ip)
                    ? ip
                    : IPAddress.Broadcast;

                await udp.SendAsync(
                    packet,
                    packet.Length,
                    new IPEndPoint(broadcast, wol.Port));

                log($"[WoL] Packet broadcast to: {broadcast}:{wol.Port}", null);
            }
            catch (Exception ex)
            {
                log($"[WoL] Broadcast failed: {ex.Message}", ConsoleColor.Yellow);
            }
        }
        finally
        {
            // Multicast lock is disposed/released automatically here
        }
    }
}