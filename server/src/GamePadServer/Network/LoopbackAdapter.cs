using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;

namespace GamePadEcosystem.Server.Network;

/// <summary>
/// Ensures a Microsoft KM-TEST Loopback Adapter exists on the system.
///
/// Windows refuses to start the Mobile Hotspot (NetworkOperatorTetheringManager)
/// unless it can hand it a VALID connection profile, and it rejects every profile
/// whose network is not actually connected — including saved Wi-Fi networks that
/// are remembered but not connected.
///
/// A KM-TEST Loopback Adapter is a virtual network card that is ALWAYS connected.
/// It gives Windows a real connection profile to share from, so the hotspot can
/// be started with ZERO physical network connections (no Wi-Fi, no cellular,
/// no Ethernet). Phones join the hotspot and reach the gamepad server on
/// 192.168.137.1 — internet is not required for local gamepad traffic.
///
/// The device is created root-enumerated from the inbox "netloop.inf" driver
/// using the SetupAPI — the same operation the "Add Legacy Hardware" wizard and
/// devcon perform. No third-party tooling or network access is needed.
/// </summary>
public static class LoopbackAdapter
{
    public const string DisplayName = "GamePad Loopback";
    private const string StaticIp = "10.99.0.1";
    private const string StaticSubnet = "255.255.255.0";
    private const string LoopbackDescription = "Microsoft KM-TEST Loopback Adapter";

    private static readonly Guid NetClassGuid = new("{4d36e972-e325-11ce-bfc1-08002be10318}");

    // ── SetupAPI P/Invoke ───────────────────────────────────────────────────
    private const uint DIGCF_PRESENT = 0x00000002;
    private const uint DICD_GENERATE_ID = 0x00000001;
    private const uint DIF_REGISTERDEVICE = 0x00000019;
    private const uint DIF_SELECTDEVICE = 0x00000001;
    private const uint DIF_INSTALLDEVICE = 0x00000002;
    private const uint SPDIT_COMPATDRIVER = 0x00000001;
    private const uint SPDRP_HARDWAREID = 0x00000001;
    private const uint SPDRP_DEVICEDESC = 0x00000000;
    private const uint DI_QUIETINSTALL = 0x00000080;
    private const uint DI_NOBROWSE = 0x00000400;

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA
    {
        public uint cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SP_DRVINFO_DATA_V1
    {
        public uint cbSize;
        public uint DriverType;
        public IntPtr Reserved;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Description;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string MfgName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string ProviderName;
        public long DriverDate;
        public ulong DriverVersion;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SP_DEVINSTALL_PARAMS
    {
        public uint cbSize;
        public uint Flags;
        public uint FlagsEx;
        public IntPtr HwndParent;
        public IntPtr InstallMsgHandler;
        public IntPtr InstallMsgHandlerContext;
        public IntPtr FileQueue;
        public IntPtr ClassInstallReserved;
        public uint Reserved;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DriverPath;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SetupDiGetClassDevsW(ref Guid classGuid, IntPtr enumerator, IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr hDevInfo, uint memberIndex, ref SP_DEVINFO_DATA deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceInstanceIdW(IntPtr hDevInfo, ref SP_DEVINFO_DATA deviceInfoData, StringBuilder deviceId, uint deviceIdSize, out uint requiredSize);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceRegistryPropertyW(IntPtr hDevInfo, ref SP_DEVINFO_DATA deviceInfoData, uint property, out uint propertyRegDataType, byte[] propertyBuffer, uint propertyBufferSize, out uint requiredSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr hDevInfo);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern IntPtr SetupDiCreateDeviceInfoList(ref Guid classGuid, IntPtr hwndParent);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiCreateDeviceInfoW(IntPtr hDevInfo, string deviceName, ref Guid classGuid, string? deviceDescription, IntPtr hwndParent, uint creationFlags, ref SP_DEVINFO_DATA deviceInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiSetDeviceRegistryPropertyW(IntPtr hDevInfo, ref SP_DEVINFO_DATA deviceInfoData, uint property, byte[] propertyBuffer, uint propertyBufferSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiCallClassInstaller(uint installFunction, IntPtr hDevInfo, ref SP_DEVINFO_DATA deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiBuildDriverInfoList(IntPtr hDevInfo, ref SP_DEVINFO_DATA deviceInfoData, uint driverType);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiEnumDriverInfoW(IntPtr hDevInfo, ref SP_DEVINFO_DATA deviceInfoData, uint driverType, uint memberIndex, ref SP_DRVINFO_DATA_V1 driverInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiSetSelectedDriverW(IntPtr hDevInfo, ref SP_DEVINFO_DATA deviceInfoData, ref SP_DRVINFO_DATA_V1 driverInfoData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceInstallParamsW(IntPtr hDevInfo, ref SP_DEVINFO_DATA deviceInfoData, ref SP_DEVINSTALL_PARAMS deviceInstallParams);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiSetDeviceInstallParamsW(IntPtr hDevInfo, ref SP_DEVINFO_DATA deviceInfoData, ref SP_DEVINSTALL_PARAMS deviceInstallParams);

    /// <summary>
    /// Returns the name of the loopback adapter if it already exists.
    /// Matches ONLY the KM-TEST virtual NIC — "Loopback Pseudo-Interface 1"
    /// (the 127.0.0.1 pseudo interface) must not be treated as our adapter.
    /// </summary>
    public static string? FindExisting()
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.Description.Contains("Microsoft KM-TEST Loopback Adapter", StringComparison.OrdinalIgnoreCase))
                return ni.Name;
        }
        return null;
    }

    /// <summary>
    /// Ensures the loopback adapter exists, is enabled, named and configured.
    /// Returns the interface name, or null if it could not be provisioned.
    /// </summary>
    public static string? EnsureCreated()
    {
        var existing = FindExisting();
        if (existing != null)
        {
            Log($"Loopback adapter already present: '{existing}'");
            Configure(existing);
            return existing;
        }

        Log("Creating Microsoft KM-TEST Loopback Adapter...");
        if (!CreateDevice())
        {
            Log("SetupAPI: device creation failed");
            return null;
        }

        Log("Waiting for loopback adapter to appear...");
        var name = WaitForAdapter(TimeSpan.FromSeconds(30));
        if (name == null)
        {
            Log("Loopback adapter did not appear after creation");
            return null;
        }

        Log($"Loopback adapter created: '{name}'");
        Configure(name);
        return name;
    }

    // ── Device creation (SetupAPI) ──────────────────────────────────────────

    private static bool CreateDevice()
    {
        var instanceId = NextFreeInstanceId();
        if (instanceId == null)
        {
            Log("Unable to find a free ROOT\\NET instance id");
            return false;
        }

        var createClass = NetClassGuid;
        var hDevInfo = SetupDiCreateDeviceInfoList(ref createClass, IntPtr.Zero);
        if (hDevInfo == IntPtr.Zero || hDevInfo == new IntPtr(-1))
        {
            Log($"SetupDiCreateDeviceInfoList failed: {Marshal.GetLastWin32Error()}");
            return false;
        }

        try
        {
            var classGuid = NetClassGuid;
            var devInfoData = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
            if (!SetupDiCreateDeviceInfoW(hDevInfo, instanceId, ref classGuid, null, IntPtr.Zero, DICD_GENERATE_ID, ref devInfoData))
            {
                Log($"SetupDiCreateDeviceInfo failed: {Marshal.GetLastWin32Error()}");
                return false;
            }

            var hardwareIds = Encoding.Unicode.GetBytes($"{instanceId}\0*msloop\0\0");
            if (!SetupDiSetDeviceRegistryPropertyW(hDevInfo, ref devInfoData, SPDRP_HARDWAREID, hardwareIds, (uint)hardwareIds.Length))
            {
                Log($"SetupDiSetDeviceRegistryProperty(SPDRP_HARDWAREID) failed: {Marshal.GetLastWin32Error()}");
                return false;
            }

            if (!SetupDiCallClassInstaller(DIF_REGISTERDEVICE, hDevInfo, ref devInfoData))
            {
                Log($"DIF_REGISTERDEVICE failed: {Marshal.GetLastWin32Error()}");
                return false;
            }

            // Quiet install — no UI prompts.
            var installParams = new SP_DEVINSTALL_PARAMS { cbSize = (uint)Marshal.SizeOf<SP_DEVINSTALL_PARAMS>() };
            if (SetupDiGetDeviceInstallParamsW(hDevInfo, ref devInfoData, ref installParams))
            {
                installParams.Flags |= DI_QUIETINSTALL | DI_NOBROWSE;
                SetupDiSetDeviceInstallParamsW(hDevInfo, ref devInfoData, ref installParams);
            }

            if (!SetupDiBuildDriverInfoList(hDevInfo, ref devInfoData, SPDIT_COMPATDRIVER))
            {
                Log($"SetupDiBuildDriverInfoList failed: {Marshal.GetLastWin32Error()}");
                return false;
            }

            var drvInfoData = new SP_DRVINFO_DATA_V1
            {
                cbSize = (uint)Marshal.SizeOf<SP_DRVINFO_DATA_V1>(),
                Description = string.Empty,
                MfgName = string.Empty,
                ProviderName = string.Empty,
            };

            var found = false;
            uint index = 0;
            while (SetupDiEnumDriverInfoW(hDevInfo, ref devInfoData, SPDIT_COMPATDRIVER, index, ref drvInfoData))
            {
                if (drvInfoData.Description.Contains("Loopback", StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
                index++;
            }

            if (!found)
            {
                Log("No loopback driver found in the driver list");
                return false;
            }

            if (!SetupDiSetSelectedDriverW(hDevInfo, ref devInfoData, ref drvInfoData))
            {
                Log($"SetupDiSetSelectedDriver failed: {Marshal.GetLastWin32Error()}");
                return false;
            }

            if (!SetupDiCallClassInstaller(DIF_SELECTDEVICE, hDevInfo, ref devInfoData))
            {
                Log($"DIF_SELECTDEVICE failed: {Marshal.GetLastWin32Error()}");
                return false;
            }

            if (!SetupDiCallClassInstaller(DIF_INSTALLDEVICE, hDevInfo, ref devInfoData))
            {
                Log($"DIF_INSTALLDEVICE failed: {Marshal.GetLastWin32Error()}");
                return false;
            }

            Log("Device created and driver installed");
            return true;
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(hDevInfo);
        }
    }

    private static string? NextFreeInstanceId()
    {
        var used = new HashSet<string>();
        var classGuid = NetClassGuid;
        var hDevInfo = SetupDiGetClassDevsW(ref classGuid, IntPtr.Zero, IntPtr.Zero, DIGCF_PRESENT);
        if (hDevInfo == IntPtr.Zero || hDevInfo == new IntPtr(-1))
            return null;

        try
        {
            uint index = 0;
            while (true)
            {
                var devInfoData = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
                if (!SetupDiEnumDeviceInfo(hDevInfo, index, ref devInfoData))
                    break;

                var sb = new StringBuilder(256);
                if (SetupDiGetDeviceInstanceIdW(hDevInfo, ref devInfoData, sb, (uint)sb.Capacity, out _))
                    used.Add(sb.ToString().ToUpperInvariant());
                index++;
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(hDevInfo);
        }

        for (int i = 1; i < 1000; i++)
        {
            var id = $"ROOT\\NET\\{i:0000}";
            if (!used.Contains(id))
                return id;
        }
        return null;
    }

    // ── Post-creation configuration ─────────────────────────────────────────

    private static string? WaitForAdapter(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var name = FindExisting();
            if (name != null)
                return name;
            Thread.Sleep(500);
        }
        return null;
    }

    private static void Configure(string name)
    {
        try
        {
            RunNetsh($"interface set interface name=\"{name}\" admin=enable");

            if (!name.Equals(DisplayName, StringComparison.OrdinalIgnoreCase))
            {
                RunNetsh($"interface set interface name=\"{name}\" newname=\"{DisplayName}\"");
                name = DisplayName;
            }

            RunNetsh($"interface ip set address name=\"{name}\" static {StaticIp} {StaticSubnet}");
            Log($"Loopback adapter configured: {DisplayName} = {StaticIp}/{StaticSubnet}");
        }
        catch (Exception ex)
        {
            Log($"Loopback configuration error: {ex.Message}");
        }
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
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        using var proc = Process.Start(psi);
        if (proc == null) return string.Empty;
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(5000);
        return stdout + stderr;
    }

    private static void Log(string message)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("[Loopback] ");
        Console.ResetColor();
        Console.WriteLine(message);
    }
}
