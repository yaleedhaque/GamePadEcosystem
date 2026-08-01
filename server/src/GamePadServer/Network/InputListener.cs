using System.Net;
using System.Net.Sockets;
using GamePadEcosystem.Server.Core;
using GamePadEcosystem.Server.VirtualController;
using Protocol = GamePadEcosystem.Server.Core.Protocol;

namespace GamePadEcosystem.Server.Network;

/// <summary>
/// High-performance UDP input listener.
/// Runs on a dedicated async task with a large receive buffer.
/// Routes incoming packets to the appropriate virtual controller.
/// </summary>
public sealed class InputListener : IDisposable
{
    private UdpClient? _udp;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private readonly ClientManager _clientManager;
    private readonly ControllerManager _controllerManager;

    /// <summary>
    /// Fires for every valid input packet received.
    /// </summary>
    public event Action<byte, InputPacket>? OnInputReceived;

    public InputListener(ClientManager clientManager, ControllerManager controllerManager)
    {
        _clientManager = clientManager;
        _controllerManager = controllerManager;
    }

    public void Start()
    {
        _udp = new UdpClient(new IPEndPoint(IPAddress.Any, Protocol.UdpPort));
        _udp.Client.ReceiveBufferSize = 2 * 1024 * 1024; // 2MB buffer
        _cts = new CancellationTokenSource();

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"[Input] Listening on UDP port {Protocol.UdpPort}");
        Console.ResetColor();

        _listenTask = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var result = await _udp.ReceiveAsync(_cts.Token);
                    _ = ProcessPacketAsync(result.Buffer, result.RemoteEndPoint);
                }
                catch (OperationCanceledException) { break; }
                catch (SocketException ex)
                {
                    // A transient socket error must NEVER kill the input listener —
                    // otherwise the server silently stops responding to controllers
                    // while discovery keeps running (observed failure). Log and continue.
                    Console.WriteLine($"[Input] Socket error {ex.SocketErrorCode} ({ex.Message}) — continuing");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Input] Error: {ex.Message}");
                }
            }
        });
    }

    private Task ProcessPacketAsync(byte[] data, IPEndPoint remoteEp)
    {
        ProcessPacket(data, remoteEp);
        return Task.CompletedTask;
    }

    private void ProcessPacket(byte[] data, IPEndPoint remoteEp)
    {
        if (data.Length < InputPacket.Size)
            return;

        var packet = InputPacket.Deserialize(data);
        if (!packet.IsValid())
        {
            Console.WriteLine($"[Input] Invalid packet from {remoteEp} (magic=0x{packet.Magic:X8})");
            return;
        }

        // Assign slot for new clients
        if (!_clientManager.TryGetSlot(remoteEp, out var slot))
        {
            slot = _clientManager.AssignSlot(remoteEp, $"Phone-{remoteEp.Port}");
            // Send assignment response back to client
            _ = SendAssignmentAsync(remoteEp, (byte)slot);
        }

        var client = _clientManager.GetClient(slot);
        if (client == null || !client.IsConnected)
            return;

        client.LastSeen = DateTime.UtcNow;
        client.LastSequence = packet.Sequence;
        packet.DeviceId = (byte)slot;

        // Skip heartbeat-only packets — just update LastSeen above
        if (packet.PacketType == (byte)Protocol.PacketType.Heartbeat)
            return;

        // Forward to virtual controller
        _controllerManager.UpdateController(slot, packet);
        OnInputReceived?.Invoke((byte)slot, packet);
    }

    private async Task SendAssignmentAsync(IPEndPoint clientEp, byte slot)
    {
        try
        {
            var assignment = new AssignmentPacket { PlayerSlot = slot, ServerName = Dns.GetHostName() };
            var response = assignment.Serialize();
            if (_udp != null)
                await _udp.SendAsync(response, response.Length, clientEp);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[Input] Sent assignment: Player {slot + 1} → {clientEp}");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Input] Failed to send assignment: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _udp?.Close();
        _udp?.Dispose();
        _cts?.Dispose();
    }
}
