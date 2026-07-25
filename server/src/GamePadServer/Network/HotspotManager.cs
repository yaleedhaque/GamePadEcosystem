using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Win32;

namespace GamePadEcosystem.Server.Network;

public sealed class HotspotManager : IDisposable
{
    private const string DefaultSsid = "GamePad_Server";
    private const string DefaultKey = "gamepad123";
    private const string IcsScopeIp = "192.168.137.1";
    private const string IcsSubnet = "255.255.255.0";
    private const string SharedAccessParams = @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters";

    private readonly string _ssid;
    private readonly string _key;
    private bool _isRunning;
    private Process? _hotspotProcess;

    public IPAddress? HotspotIp { get; private set; }
    public string? HotspotAdapterName { get; private set; }
    public bool IsRunning => _isRunning;

    public HotspotManager(string? ssid = null, string? key = null)
    {
        _ssid = ssid ?? DefaultSsid;
        _key = key ?? DefaultKey;
    }

    public bool Start()
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"[Hotspot] Setting up WiFi hotspot: {_ssid}");
        Console.ResetColor();

        EnsureWifiAdapterEnabled();

        if (TryTetheringApi())
            return true;

        if (CheckHostedNetworkSupport() && TryHostedNetwork())
            return true;

        if (EnableMobileHotspot())
            return true;

        return UseExistingLan();
    }

    public void Dispose()
    {
        _isRunning = false;
        try
        {
            if (_hotspotProcess != null && !_hotspotProcess.HasExited)
            {
                _hotspotProcess.Kill();
                _hotspotProcess.Dispose();
                Log("Hotspot", "Hotspot process stopped");
            }
        }
        catch { }
    }

    // ════════════════════════════════════════════════════════════════════
    // STRATEGY 1: NetworkOperatorTetheringManager
    // ════════════════════════════════════════════════════════════════════
    // This is what Windows Settings uses internally.
    // Key finding: it works with ANY saved WiFi profile, even when
    // WiFi is not connected. The WiFi adapter just needs to be enabled.
    //
    // Priority order for connection profile:
    //   1. GetInternetConnectionProfile() — active WiFi/Ethernet/cellular
    //   2. First saved WLAN profile from GetConnectionProfiles()
    //   3. Any connection profile with connectivity
    // ════════════════════════════════════════════════════════════════════

    private bool TryTetheringApi()
    {
        Log("Hotspot", "Strategy: NetworkOperatorTetheringManager (Windows native hotspot)");

        var scriptPath = Path.Combine(Path.GetTempPath(), "gamepad_tether.ps1");
        try { File.WriteAllText(scriptPath, BuildTetheringScript()); }
        catch (Exception ex) { Log("Hotspot", $"Script write error: {ex.Message}"); return false; }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _hotspotProcess = Process.Start(psi);
            if (_hotspotProcess == null) { Log("Hotspot", "Failed to start PowerShell"); return false; }

            // Use async reads — the PS script runs an infinite loop to keep hotspot alive,
            // so synchronous ReadToEnd() would block forever waiting for process exit.
            var outputBuffer = new System.Text.StringBuilder();
            var errorBuffer = new System.Text.StringBuilder();
            var stdoutDone = new ManualResetEventSlim();
            var stderrDone = new ManualResetEventSlim();

            _hotspotProcess.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null) { lock (outputBuffer) { outputBuffer.AppendLine(e.Data); } }
                else stdoutDone.Set();
            };
            _hotspotProcess.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null) { lock (errorBuffer) { errorBuffer.AppendLine(e.Data); } }
                else stderrDone.Set();
            };

            _hotspotProcess.BeginOutputReadLine();
            _hotspotProcess.BeginErrorReadLine();

            // Wait up to 45 seconds for the script to signal HOTSPOT_ACTIVE or ERROR
            var deadline = DateTime.UtcNow.AddSeconds(45);
            var gotActive = false;
            var gotError = false;
            while (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(500);
                lock (outputBuffer)
                {
                    if (outputBuffer.ToString().Contains("HOTSPOT_ACTIVE")) gotActive = true;
                    if (outputBuffer.ToString().Contains("ERROR")) gotError = true;
                }
                if (gotActive || gotError) break;
            }

            var stdout = outputBuffer.ToString();
            var stderr = errorBuffer.ToString();
            Log("Hotspot", $"Output: {stdout.Trim()}");
            if (!string.IsNullOrWhiteSpace(stderr))
                Log("Hotspot", $"Stderr: {stderr.Trim()}");

            if (gotError)
            {
                Log("Hotspot", "Tethering API failed");
                return false;
            }

            if (!gotActive)
            {
                Log("Hotspot", "Tethering did not signal HOTSPOT_ACTIVE within timeout");
                if (_hotspotProcess != null && !_hotspotProcess.HasExited && _hotspotProcess.ExitCode != 0)
                {
                    Log("Hotspot", $"Tethering exited with code {_hotspotProcess.ExitCode}");
                    return false;
                }
            }

            Log("Hotspot", "Tethering API reports hotspot is active");
        }
        catch (Exception ex)
        {
            Log("Hotspot", $"Tethering error: {ex.Message}");
            return false;
        }

        // Wait for the hotspot adapter to appear
        Log("Hotspot", "Waiting for hotspot adapter...");
        for (int i = 0; i < 20; i++)
        {
            Thread.Sleep(1000);
            if (_hotspotProcess != null && _hotspotProcess.HasExited && _hotspotProcess.ExitCode != 0)
            {
                Log("Hotspot", "Tethering process exited with error");
                return false;
            }
            if (FindHotspotAdapter())
            {
                _isRunning = true;
                LogSuccess();
                return true;
            }
            if (i % 5 == 4)
                Log("Hotspot", $"  Waiting... ({i + 1}/20)");
        }

        Log("Hotspot", "Hotspot adapter did not appear");
        Dispose();
        return false;
    }

    private string BuildTetheringScript()
    {
        var ssidEsc = _ssid.Replace("'", "''");
        var keyEsc = _key.Replace("'", "''");

        return @"
$ErrorActionPreference = 'Stop'
try {
    Add-Type -AssemblyName System.Runtime.WindowsRuntime

    $asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
    function Await($WinRtTask, $ResultType) {
        $asTask = $asTaskGeneric.MakeGenericMethod($ResultType)
        $netTask = $asTask.Invoke($null, @($WinRtTask))
        $netTask.Wait(-1) | Out-Null
        $netTask.Result
    }

    [Windows.Networking.Connectivity.NetworkInformation,Windows.Networking.Connectivity,ContentType=WindowsRuntime] | Out-Null
    [Windows.Networking.NetworkOperators.NetworkOperatorTetheringManager,Windows.Networking.NetworkOperators,ContentType=WindowsRuntime] | Out-Null
    [Windows.Networking.NetworkOperators.NetworkOperatorTetheringOperationResult,Windows.Networking.NetworkOperators,ContentType=WindowsRuntime] | Out-Null

    # Stop any existing hotspot first
    try {
        $existingCp = [Windows.Networking.Connectivity.NetworkInformation]::GetInternetConnectionProfile()
        if ($existingCp) {
            $existingTm = [Windows.Networking.NetworkOperators.NetworkOperatorTetheringManager]::CreateFromConnectionProfile($existingCp)
            if ($existingTm.TetheringOperationalState -eq 'On') {
                Await ($existingTm.StopTetheringAsync()) ([Windows.Networking.NetworkOperators.NetworkOperatorTetheringOperationResult]) | Out-Null
                Start-Sleep -Seconds 2
            }
        }
    } catch {}

    # Find the best connection profile
    $connectionProfile = $null

    # Priority 1: Active internet connection
    $connectionProfile = [Windows.Networking.Connectivity.NetworkInformation]::GetInternetConnectionProfile()
    if ($connectionProfile) {
        Write-Host ""Using active connection: $($connectionProfile.ProfileName)""
    }

    # Priority 2: Any saved WLAN profile (works even when WiFi is disconnected)
    if (-not $connectionProfile) {
        $allProfiles = [Windows.Networking.Connectivity.NetworkInformation]::GetConnectionProfiles()
        foreach ($p in $allProfiles) {
            if ($p.IsWlanConnectionProfile) {
                $connectionProfile = $p
                Write-Host ""Using saved WLAN profile: $($p.ProfileName)""
                break
            }
        }
    }

    # Priority 3: Any profile at all
    if (-not $connectionProfile) {
        $allProfiles = [Windows.Networking.Connectivity.NetworkInformation]::GetConnectionProfiles()
        if ($allProfiles.Count -gt 0) {
            $connectionProfile = $allProfiles[0]
            Write-Host ""Using first available profile: $($connectionProfile.ProfileName)""
        }
    }

    if (-not $connectionProfile) {
        Write-Host 'ERROR: No connection profile available'
        exit 1
    }

    # Create tethering manager and start hotspot
    $tetheringManager = [Windows.Networking.NetworkOperators.NetworkOperatorTetheringManager]::CreateFromConnectionProfile($connectionProfile)
    $state = $tetheringManager.TetheringOperationalState
    Write-Host ""Current state: $state""

    if ($state -eq 'On') {
        Write-Host 'HOTSPOT_ACTIVE'
        try { while ($true) { Start-Sleep -Seconds 5 } } catch { }
    }

    # Start with per-session config (sets SSID + password)
    $started = $false
    try {
        $config = [Windows.Networking.NetworkOperators.NetworkOperatorTetheringSessionAccessPointConfiguration]::new()
        $config.Ssid = '" + _ssid + @"'
        $config.Passphrase = '" + _key + @"'
        $result = Await ($tetheringManager.StartTetheringAsync($config)) ([Windows.Networking.NetworkOperators.NetworkOperatorTetheringOperationResult])
        Write-Host ""Config start result: $($result.Status)""
        if ($result.Status -eq 'Success') { $started = $true }
    } catch {
        Write-Host ""Config start failed: $($_.Exception.Message)""
    }

    # Fallback: simple start (uses previously configured SSID/password)
    if (-not $started) {
        try {
            $result = Await ($tetheringManager.StartTetheringAsync()) ([Windows.Networking.NetworkOperators.NetworkOperatorTetheringOperationResult])
            Write-Host ""Simple start result: $($result.Status)""
            if ($result.Status -eq 'Success') { $started = $true }
        } catch {
            Write-Host ""Simple start failed: $($_.Exception.Message)""
        }
    }

    # Check final state
    Start-Sleep -Seconds 2
    $finalState = $tetheringManager.TetheringOperationalState
    Write-Host ""Final state: $finalState""

    if ($started -or $finalState -eq 'On') {
        Write-Host 'HOTSPOT_ACTIVE'
        # Keep process alive to maintain hotspot
        try { while ($true) { Start-Sleep -Seconds 5 } } catch { }
    } else {
        Write-Host 'ERROR:Hotspot failed to start'
        exit 1
    }
} catch {
    Write-Host ""ERROR:$($_.Exception.Message)""
    exit 1
}";
    }

    // ════════════════════════════════════════════════════════════════════
    // SHARED HELPERS
    // ════════════════════════════════════════════════════════════════════

    private void EnsureWifiAdapterEnabled()
    {
        try
        {
            var adapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.Description.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase) ||
                             ni.Description.Contains("Wireless", StringComparison.OrdinalIgnoreCase) ||
                             ni.Name.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var adapter in adapters)
            {
                if (adapter.OperationalStatus == OperationalStatus.Up)
                {
                    Log("Hotspot", $"WiFi adapter is enabled: {adapter.Name}");
                    return;
                }
            }

            Log("Hotspot", "Enabling WiFi adapter...");
            RunCmd("powershell -NoProfile -Command \"Get-NetAdapter | Where-Object { $_.InterfaceDescription -like '*Wi-Fi*' -or $_.InterfaceDescription -like '*Wireless*' } | Enable-NetAdapter -Confirm:$false\"");
            Thread.Sleep(3000);
            Log("Hotspot", "WiFi adapter enabled");
        }
        catch (Exception ex)
        {
            Log("Hotspot", $"WiFi enable: {ex.Message}");
        }
    }

    private bool FindHotspotAdapter()
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;

            var desc = ni.Description;
            var name = ni.Name;

            var isHotspot = desc.Contains("Microsoft Wi-Fi Direct") ||
                            desc.Contains("Wi-Fi Direct") ||
                            desc.Contains("Local Area Connection*") ||
                            (desc.Contains("Virtual") && desc.Contains("Wi-Fi")) ||
                            desc.Contains("Mobile Hotspot");

            if (!isHotspot) continue;

            var ip = GetIPv4(ni);
            if (ip != null)
            {
                HotspotIp = IPAddress.Parse(ip);
                HotspotAdapterName = name;
                Log("Hotspot", $"Found hotspot adapter: {name} ({desc}) -> {ip}");
                return true;
            }

            Log("Hotspot", $"Found adapter {name} — assigning IP...");
            try
            {
                RunNetsh($"interface ip set address name=\"{name}\" static {IcsScopeIp} {IcsSubnet}");
                Thread.Sleep(500);
                var recheck = GetIPv4(ni);
                if (recheck != null)
                {
                    HotspotIp = IPAddress.Parse(recheck);
                    HotspotAdapterName = name;
                    return true;
                }
            }
            catch { }
        }
        return false;
    }

    private bool CheckHostedNetworkSupport()
    {
        try
        {
            var output = RunNetsh("wlan show drivers");
            if (output.Contains("Hosted network supported  : Yes")) return true;
            Log("Hotspot", "Driver does NOT support hosted network");
            return false;
        }
        catch { return false; }
    }

    private bool TryHostedNetwork()
    {
        Log("Hotspot", "Strategy: Hosted network");
        RunNetsh($"wlan set hostednetwork mode=allow ssid={_ssid} key={_key}");
        var startOut = RunNetsh("wlan start hostednetwork");
        if (!startOut.Contains("started")) return false;

        Thread.Sleep(3000);
        if (FindHotspotAdapter())
        {
            _isRunning = true;
            LogSuccess();
            return true;
        }
        return false;
    }

    private bool EnableMobileHotspot()
    {
        Log("Hotspot", "Strategy: ICS via registry");
        try
        {
            using var paramKey = Registry.LocalMachine.OpenSubKey(SharedAccessParams, true);
            if (paramKey == null) return false;
            paramKey.SetValue("ScopeAddress", IcsScopeIp, RegistryValueKind.String);
            paramKey.SetValue("ScopeAddressBackup", IcsScopeIp, RegistryValueKind.String);
            paramKey.SetValue("ScopeAddressPool", "192.168.137.2-192.168.137.254", RegistryValueKind.String);
        }
        catch { return false; }

        try { RunCmd("sc config SharedAccess start= auto"); RunCmd("net start SharedAccess"); } catch { }

        for (int i = 0; i < 10; i++)
        {
            Thread.Sleep(1000);
            if (FindHotspotAdapter())
            {
                _isRunning = true;
                LogSuccess();
                return true;
            }
        }
        return false;
    }

    private bool UseExistingLan()
    {
        Log("Hotspot", "No hotspot — using existing LAN IP");
        var ip = GetBestLanIp();
        if (ip == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[Hotspot] ERROR: No network connection found.");
            Console.WriteLine("[Hotspot] Connect to WiFi or Ethernet first.");
            Console.ResetColor();
            return false;
        }

        HotspotIp = IPAddress.Parse(ip);
        HotspotAdapterName = "LAN";
        _isRunning = true;

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine($"  SERVER ON LAN: {ip}:{Core.Protocol.UdpPort}");
        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.ResetColor();
        return true;
    }

    private static string? GetIPv4(NetworkInterface ni)
    {
        try
        {
            return ni.GetIPProperties().UnicastAddresses
                .FirstOrDefault(ua =>
                    ua.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(ua.Address))?
                .Address.ToString();
        }
        catch { return null; }
    }

    private static string? GetBestLanIp()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect("8.8.8.8", 80);
            return (socket.LocalEndPoint as IPEndPoint)?.Address.ToString();
        }
        catch { return null; }
    }

    private static string RunNetsh(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "netsh",
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };
        using var proc = Process.Start(psi);
        if (proc == null) return string.Empty;
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(5000);
        return stdout + stderr;
    }

    private static string RunCmd(string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd",
            Arguments = $"/c {command}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi);
        if (proc == null) return string.Empty;
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(5000);
        return output;
    }

    private static void Log(string tag, string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"[{tag}] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }

    private void LogSuccess()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine($"  HOTSPOT ACTIVE: {_ssid}");
        Console.WriteLine($"  Password:       {_key}");
        Console.WriteLine($"  Server IP:      {HotspotIp}");
        Console.WriteLine($"  Adapter:        {HotspotAdapterName}");
        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.ResetColor();

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("[Hotspot] Connect your phone to this WiFi network.");
        Console.WriteLine("[Hotspot] The GamePad app will auto-discover the server.");
        Console.ResetColor();
    }
}
