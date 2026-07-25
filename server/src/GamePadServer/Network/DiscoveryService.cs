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
/// </summary>
public sealed class DiscoveryService : IDisposable
{
    private UdpClient? _udp;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private Task? _broadcastTask;

    public event Action<string, IPEndPoint>? OnClientDiscovered;

    private string ServerIdentity => $"GPAD_SERVER_V1|{Dns.GetHostName()}";

    public void Start()
    {
        _udp = new UdpClient(new IPEndPoint(IPAddress.Any, Protocol.DiscoveryPort));
        _udp.EnableBroadcast = true;
        _cts = new CancellationTokenSource();

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"[Discovery] Listening on UDP port {Protocol.DiscoveryPort}");
        Console.WriteLine($"[Discovery] Broadcasting server presence every 2s");
        Console.ResetColor();

        // Listener — responds to ANY packet on this port (not just magic)
        // This ensures compatibility with all client versions
        _listenTask = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var result = await _udp.ReceiveAsync(_cts.Token);
                    var data = result.Buffer;

                    // Respond to ANY packet — old clients, new clients, broadcasts
                    var deviceName = "Android Device";
                    if (data.Length >= Protocol.DiscoveryMagic.Length &&
                        data.AsSpan().SequenceEqual(Protocol.DiscoveryMagic))
                    {
                        if (data.Length > Protocol.DiscoveryMagic.Length)
                            deviceName = Encoding.UTF8.GetString(
                                data.AsSpan(Protocol.DiscoveryMagic.Length));
                    }

                    Console.WriteLine(
                        $"[Discovery] Packet from {result.RemoteEndPoint} — responding with server identity");

                    var response = Encoding.UTF8.GetBytes(ServerIdentity);
                    await _udp.SendAsync(response, response.Length, result.RemoteEndPoint);

                    OnClientDiscovered?.Invoke(deviceName, result.RemoteEndPoint);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Discovery] Error: {ex.Message}");
                }
            }
        });

        // Broadcaster — sends to every active network interface's broadcast address
        _broadcastTask = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var broadcastData = Encoding.UTF8.GetBytes(ServerIdentity);

                    // Send to 255.255.255.255 (limited broadcast)
                    try
                    {
                        var limitedBcast = new IPEndPoint(IPAddress.Broadcast, Protocol.DiscoveryPort);
                        await _udp.SendAsync(broadcastData, broadcastData.Length, limitedBcast);
                    }
                    catch { }

                    // Send to each network interface's broadcast address
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
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"[Discovery] Broadcast for {uniAddr.Address}: {string.Join(".", broadcast)}");
                    Console.ResetColor();
                }
            }
        }
        catch { }
        return addresses;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _udp?.Close();
        _udp?.Dispose();
        _cts?.Dispose();
    }
}
