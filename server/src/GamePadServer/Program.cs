using GamePadEcosystem.Server.Core;
using GamePadEcosystem.Server.Network;
using GamePadEcosystem.Server.VirtualController;
using GamePadEcosystem.Server.UI;

namespace GamePadEcosystem.Server;

/// <summary>
/// Entry point — wires all subsystems and runs the server loop.
/// </summary>
internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            FileLog($"FATAL: {e.ExceptionObject}");
        };

        try
        {
            Run(args);
        }
        catch (Exception ex)
        {
            FileLog($"FATAL (top-level): {ex}");
            throw;
        }
    }

    private static void Run(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(Logo);
        Console.ResetColor();

        Log("Boot", "Starting GamePad Server v1.1...");
        Log("Boot", "Initializing ViGEmBus driver interface...");

        using var clientManager = new ClientManager();
        using var controllerManager = new ControllerManager();
        using var discoveryService = new DiscoveryService();
        using var inputListener = new InputListener(clientManager, controllerManager);
        using var hud = new ServerHud(clientManager, controllerManager);

        // Start WiFi hotspot — creates local network, no internet needed
        using var hotspot = new HotspotManager();
        var hotspotReady = hotspot.Start();

        // Wire events
        discoveryService.OnClientDiscovered += (name, ep) =>
            Log("Discovery", $"New device on network: {name} @ {ep}");

        inputListener.OnInputReceived += (slot, packet) =>
            hud.IncrementStats(InputPacket.Size);

        // Start services
        discoveryService.Start();
        inputListener.Start();
        hud.Start();

        var serverIp = hotspot.HotspotIp?.ToString() ?? GetLocalIp() ?? "unknown";

        Log("Boot", "═══════════════════════════════════════════════");
        Log("Boot", "Server is READY");
        Log("Boot", "");

        if (hotspotReady)
        {
            Log("Boot", "HOTSPOT is running — connect phones to GamePad_Server WiFi:");
            Log("Boot", $"  SSID:     GamePad_Server");
            Log("Boot", $"" +
                $"Password:  gamepad123");
            Log("Boot", $"  Server:   {serverIp}:{Protocol.UdpPort}");
            Log("Boot", "");
        }
        else
        {
            Log("Boot", "HOTSPOT not available — manually enable Mobile Hotspot in");
            Log("Boot", "Settings > Network & Internet > Mobile Hotspot, then connect.");
            Log("Boot", $"  Server:   {serverIp}:{Protocol.UdpPort}");
            Log("Boot", "");
        }

        Log("Boot", "SETUP:");
        Log("Boot", "  1. Phone connects to PC's WiFi hotspot (or same router)");
        Log("Boot", "  2. Open GamePad Controller app on each phone");
        Log("Boot", "  3. App will auto-discover this server");
        Log("Boot", "");
        Log("Boot", "PC hotspot blocks phone hotspot client isolation —");
        Log("Boot", "all devices can see each other. No router needed.");
        Log("Boot", "═══════════════════════════════════════════════");
        Log("Boot", "Press [Q] to quit.\n");

        // Watchdog
        using var watchdogCts = new CancellationTokenSource();
        var watchdog = Task.Run(async () =>
        {
            while (!watchdogCts.Token.IsCancellationRequested)
            {
                try
                {
                    clientManager.RemoveStaleClients(TimeSpan.FromMilliseconds(Protocol.DisconnectionTimeoutMs));
                    controllerManager.CheckDisconnections(TimeSpan.FromMilliseconds(Protocol.DisconnectionTimeoutMs));
                    await Task.Delay(1000, watchdogCts.Token);
                }
                catch (OperationCanceledException) { break; }
            }
        });

        // Main loop
        while (true)
        {
            try
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Q) break;
                }
            }
            catch (InvalidOperationException)
            {
                // No console attached (e.g. launched from a startup shortcut) —
                // Console.KeyAvailable throws here. Keep the server alive.
            }
            Thread.Sleep(50);
        }

        // Cleanup
        Log("Shutdown", "Cleaning up...");
        watchdogCts.Cancel();
        hud.Dispose();
        inputListener.Dispose();
        discoveryService.Dispose();
        hotspot.Dispose();
        controllerManager.Dispose();
        clientManager.Reset();
        Log("Shutdown", "Server stopped cleanly.");
    }

    private static string? GetLocalIp()
    {
        try
        {
            using var socket = new System.Net.Sockets.Socket(
                System.Net.Sockets.AddressFamily.InterNetwork,
                System.Net.Sockets.SocketType.Dgram,
                System.Net.Sockets.ProtocolType.Udp);
            socket.Connect("8.8.8.8", 80);
            return (socket.LocalEndPoint as System.Net.IPEndPoint)?.Address.ToString();
        }
        catch { return null; }
    }

    private static void Log(string tag, string message)
    {
        FileLog($"[{tag}] {message}");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"[{tag}] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    /// <summary>
    /// Mirrors console output to a log file so the server can be diagnosed
    /// even when launched without a visible console (startup shortcut, etc.).
    /// The file is capped and rotated so it can never grow unbounded.
    /// </summary>
    private static readonly object _logLock = new();

    private static void FileLog(string line)
    {
        try
        {
            lock (_logLock)
            {
                var logPath = Path.Combine(AppContext.BaseDirectory, "server.log");
                var fi = new FileInfo(logPath);
                if (fi.Exists && fi.Length > 1_048_576) // 1 MB cap
                {
                    try { File.Copy(logPath, logPath + ".old", overwrite: true); } catch { }
                    try { File.Delete(logPath); } catch { }
                }
                File.AppendAllText(logPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {line}{Environment.NewLine}");
            }
        }
        catch { }
    }

    private const string Logo = @"
 ╔═══════════════════════════════════════════════════════════════════╗
 ║                                                                   ║
 ║       ██████╗  █████╗ ██████╗ ████████╗███████╗ ██████╗ ██╗     ██║
 ║      ██╔════╝ ██╔══██╗██╔══██╗╚══██╔══╝██╔════╝██╔═══██╗██║     ██║
 ║      ██║  ███╗███████║██████╔╝   ██║   █████╗  ██║   ██║██║     ██║
 ║      ██║   ██║██╔══██║██╔══██╗   ██║   ██╔══╝  ██║   ██║██║     ██║
 ║      ╚██████╔╝██║  ██║██████╔╝   ██║   ██║     ╚██████╔╝███████╗██║
 ║       ╚═════╝ ╚═╝  ╚═╝╚═════╝    ╚═╝   ╚═╝      ╚═════╝ ╚══════╝║
 ║                                                                   ║
 ║          Offline Multiplayer Virtual Gamepad Server v1.1          ║
 ║              ViGEmBus + UDP · WiFi Hotspot · Zero Cloud           ║
 ║                                                                   ║
 ╚═══════════════════════════════════════════════════════════════════╝";
}
