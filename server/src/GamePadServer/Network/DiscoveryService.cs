using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using GamePadEcosystem.Server.Core;

namespace GamePadEcosystem.Server.Network;

/// <summary>
/// Handles UDP broadcast auto-discovery from Android clients.
/// Two modes:
///   1. Reactive: responds to client discovery broadcasts
///   2. Active: periodically broadcasts server presence so clients find us passively
///
/// FIX: The previous version received its OWN broadcast packets back on the same
/// socket, treated them as brand-new phones ("Android Device @ <own-ip>:9878")
/// and responded to itself in an infinite loop — flooding the log and console.
/// We now ignore any packet that (a) comes from one of our own local IPs or
/// (b) contains our own server identity, and we dedupe real devices per-IP.
/// </summary>
public sealed class DiscoveryService : IDisposable
{
    private UdpClient? _udp;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private Task? _broadcastTask;

    private readonly ConcurrentDictionary<string, DateTime> _seenDevices = new();
    private readonly object _lock = new();
    private volatile HashSet<IPAddress> _ownIps = new();

    public event Action<string, IPEndPoint>? OnClientDiscovered;

    private string ServerIdentity => $"GPAD_SERVER_V1|{Dns.GetHostName()}";

    public void Start()
    {
        _udp = new UdpClient(new IPEndPoint(IPAddress.Any, Protocol.DiscoveryPort));
        _udp.EnableBroadcast = true;
        _cts = new CancellationTokenSource();
        _ownIps = CollectLocalIps();

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"[Discovery] Listening on UDP port {Protocol.DiscoveryPort}");
        Console.WriteLine($"[Discovery] Broadcasting server presence every 2s");
        Console.ResetColor();

        // Listener — responds to ANY packet on this port (not just magic)
        // This ensures compatibility with all client versions.
        _listenTask = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var result = await _udp.ReceiveAsync(_cts.Token);
                    ProcessPacket(result.Buffer, result.RemoteEndPoint);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Discovery] Error: {ex.Message}");
                }
            }
        });

        // Broadcaster — sends to every active network interface's broadcast address.
        _broadcastTask = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    _ownIps = CollectLocalIps();
                    var broadcastData = Encoding.UTF8.GetBytes(ServerIdentity);

                    try
                    {
                        var limitedBcast = new IPEndPoint(IPAddress.Broadcast, Protocol.DiscoveryPort);
                        await _udp.SendAsync(broadcastData, broadcastData.Length, limitedBcast);
                    }
                    catch { }

                    foreach (var iface in GetAllBroadcastAddresses())
                    {
                        try
                        {
                            var ep = new IPEndPoint(iface, Protocol.DiscoveryPort);
                            await _udp.SendAsync(broadcastData, broadcastData.Length, ep);
                        }
                        catch { }
                    }

                    await Task.Delay(2000, _cts.Token);
                }
                catch (OperationCanceledException) { break; }
                catch { }
            }
        });
    }

    private void ProcessPacket(byte[] data, IPEndPoint remoteEp)
    {
        // ── Guard 1: never react to our own broadcasts ──────────────────────
        if (remoteEp.Address is null) return;
        if (_ownIps.Contains(remoteEp.Address)) return;
        if (IsBroadcastAddress(remoteEp.Address)) return;

        // ── Guard 2: ignore our own server-identity broadcasts received back ─
        if (data.Length >= ServerIdentity.Length &&
            Encoding.UTF8.GetString(data.AsSpan(0, ServerIdentity.Length))
                .StartsWith("GPAD_SERVER_V1", StringComparison.Ordinal))
            return;

        var deviceName = "Android Device";
        if (data.Length >= Protocol.DiscoveryMagic.Length &&
            data.AsSpan().SequenceEqual(Protocol.DiscoveryMagic))
        {
            if (data.Length > Protocol.DiscoveryMagic.Length)
                deviceName = Encoding.UTF8.GetString(data.AsSpan(Protocol.DiscoveryMagic.Length));
        }

        // Respond with server identity so the phone can connect.
        var response = Encoding.UTF8.GetBytes(ServerIdentity);
        try { _udp?.SendAsync(response, response.Length, remoteEp).Wait(200); }
        catch { }

        // ── Guard 3: dedupe per-endpoint so we don't spam discovery events ──
        var key = $"{remoteEp.Address}:{remoteEp.Port}";
        var now = DateTime.UtcNow;
        bool isNew = false;
        lock (_lock)
        {
            if (!_seenDevices.TryGetValue(key, out var last) || (now - last) > TimeSpan.FromSeconds(10))
            {
                _seenDevices[key] = now;
                isNew = true;
            }
        }
        if (!isNew) return;

        OnClientDiscovered?.Invoke(deviceName, remoteEp);
    }

    /// <summary>
    /// Gets the broadcast address for each active IPv4 network interface.
    /// E.g. for 192.168.137.1/24 → returns 192.168.137.255
    /// </summary>
    private static List<IPAddress> GetAllBroadcastAddresses()
    {
        var addresses = new List<IPAddress>();
        try
        {
            foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (iface.OperationalStatus != OperationalStatus.Up) continue;
                if (iface.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var uniAddr in iface.GetIPProperties().UnicastAddresses)
                {
                    if (uniAddr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(uniAddr.Address)) continue;

                    var ip = uniAddr.Address.GetAddressBytes();
                    var mask = uniAddr.IPv4Mask.GetAddressBytes();

                    if (mask.All(b => b == 0)) continue;

                    var broadcast = new byte[4];
                    for (int i = 0; i < 4; i++)
                        broadcast[i] = (byte)(ip[i] | ~mask[i]);

                    addresses.Add(IPAddress.Parse(string.Join(".", broadcast)));
                }
            }
        }
        catch { }
        return addresses;
    }
    private static bool IsBroadcastAddress(IPAddress address)
    {
        try
        {
            if (address.Equals(IPAddress.Broadcast)) return true;
            var bytes = address.GetAddressBytes();
            return bytes.All(b => b == 255);
        }
        catch { return false; }
    }

    /// <summary>
    /// Collects all IPv4 addresses assigned to this machine, used to ignore
    /// our own broadcast packets.
    /// </summary>
    private static HashSet<IPAddress> CollectLocalIps()
    {
        var set = new HashSet<IPAddress> { IPAddress.Loopback };
        try
        {
            foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (iface.OperationalStatus != OperationalStatus.Up) continue;
                foreach (var uniAddr in iface.GetIPProperties().UnicastAddresses)
                {
                    if (uniAddr.Address.AddressFamily == AddressFamily.InterNetwork)
                        set.Add(uniAddr.Address);
                }
            }
        }
        catch { }
        return set;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _udp?.Close();
        _udp?.Dispose();
        _cts?.Dispose();
    }
}
