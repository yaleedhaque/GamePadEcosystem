using GamePadEcosystem.Server.Core;
using GamePadEcosystem.Server.VirtualController;

namespace GamePadEcosystem.Server.UI;

public sealed class ServerHud : IDisposable
{
    private readonly ClientManager _clientManager;
    private readonly ControllerManager _controllerManager;
    private CancellationTokenSource? _cts;
    private Task? _renderTask;

    private int _totalPackets;
    private long _totalBytes;

    public void IncrementStats(int bytes)
    {
        Interlocked.Increment(ref _totalPackets);
        Interlocked.Add(ref _totalBytes, bytes);
    }

    public ServerHud(ClientManager clientManager, ControllerManager controllerManager)
    {
        _clientManager = clientManager;
        _controllerManager = controllerManager;
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _renderTask = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    Render();
                    await Task.Delay(500, _cts.Token);
                }
                catch (OperationCanceledException) { break; }
            }
        });
    }

    private void Render()
    {
        try
        {
            if (Console.IsOutputRedirected) return;
            var width = Console.WindowWidth;
            var height = Console.WindowHeight;
            if (width < 60 || height < 12) return;

            Console.SetCursorPosition(0, 0);

            var pkt = Interlocked.CompareExchange(ref _totalPackets, 0, 0);
            var byt = Interlocked.CompareExchange(ref _totalBytes, 0, 0);
            var players = _clientManager.ConnectedCount;

            Console.WriteLine($@"
 ╔════════════════════════════════════════════════════════════════════╗
 ║                    GAMEPAD SERVER — MULTIPLAYER                   ║
 ║              Offline Virtual Gamepad via WiFi Hotspot             ║
 ╠════════════════════════════════════════════════════════════════════╣
 ║ Players: {players}/{Protocol.MaxPlayers}   │ Packets: {pkt,-10} │ Bytes: {byt,-12}  ║
 ║ UDP: {Protocol.UdpPort}  │ Discovery: {Protocol.DiscoveryPort}  │ Timeout: {Protocol.DisconnectionTimeoutMs}ms            ║
 ╠════════════════════════════════════════════════════════════════════╣");

            for (int i = 0; i < Protocol.MaxPlayers; i++)
            {
                var client = _clientManager.GetClient(i);
                if (client != null && client.IsConnected)
                {
                    var elapsed = (DateTime.UtcNow - client.LastSeen).TotalSeconds;
                    var status = elapsed < 2 ? "●" : "○";
                    var color = elapsed < 2 ? ConsoleColor.Green : ConsoleColor.DarkYellow;
                    Console.ForegroundColor = color;
                    Console.Write($" ║  Player {i + 1}: {status} {client.DeviceName,-20}");
                    Console.ResetColor();
                    Console.WriteLine($" last: {elapsed:F1}s ago      ║");
                }
                else
                {
                    Console.WriteLine($" ║  Player {i + 1}: —  Waiting for connection...                    ║");
                }
            }

            Console.WriteLine(@" ╠════════════════════════════════════════════════════════════════════╣
 ║  [Q] Quit  │  Use PC Hotspot (not phone hotspot!) for multiplayer  ║
 ╚════════════════════════════════════════════════════════════════════╝");
        }
        catch { /* Console may be resizing */ }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
