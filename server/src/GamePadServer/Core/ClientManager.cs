using System.Collections.Concurrent;
using System.Net;

namespace GamePadEcosystem.Server.Core;

/// <summary>
/// Manages all connected controller clients.
/// Handles player slot assignment, timeout tracking, and connection state.
/// Thread-safe for concurrent UDP packet processing.
/// </summary>
public sealed class ClientManager : IDisposable
{
    private readonly ConcurrentDictionary<int, ConnectedClient> _clients = new();
    private readonly ConcurrentDictionary<IPEndPoint, int> _endpointToSlot = new();
    private byte _nextSlot;

    public int ConnectedCount => _clients.Count(kvp => kvp.Value.IsConnected);
    public IReadOnlyCollection<ConnectedClient> Clients => _clients.Values.ToList().AsReadOnly();

    /// <summary>
    /// Assigns or retrieves a player slot for a given endpoint.
    /// Reuses slots from disconnected clients when max players reached.
    /// </summary>
    public byte AssignSlot(IPEndPoint endpoint, string deviceName)
    {
        if (_endpointToSlot.TryGetValue(endpoint, out var existingSlot))
        {
            if (_clients.TryGetValue(existingSlot, out var existing))
            {
                existing.LastSeen = DateTime.UtcNow;
                existing.DeviceName = deviceName;
                if (!existing.IsConnected)
                {
                    existing.IsConnected = true;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[+] Player {existingSlot + 1} reconnected: {deviceName} ({endpoint})");
                    Console.ResetColor();
                }
            }
            return (byte)existingSlot;
        }

        // Try to find a free slot from disconnected clients first
        if (_nextSlot >= Protocol.MaxPlayers)
        {
            foreach (var kvp in _clients)
            {
                if (!kvp.Value.IsConnected)
                {
                    // Drop the dead endpoint's stale mapping first so a returning
                    // old device can never hijack this slot's controller.
                    if (kvp.Value.Endpoint != null)
                        _endpointToSlot.TryRemove(kvp.Value.Endpoint, out _);

                    _endpointToSlot[endpoint] = kvp.Key;
                    kvp.Value.Endpoint = endpoint;
                    kvp.Value.DeviceName = deviceName;
                    kvp.Value.IsConnected = true;
                    kvp.Value.LastSeen = DateTime.UtcNow;

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[+] Player {kvp.Key + 1} reconnected (reused slot): {deviceName} ({endpoint})");
                    Console.ResetColor();
                    return (byte)kvp.Key;
                }
            }
            throw new InvalidOperationException($"Max players ({Protocol.MaxPlayers}) reached");
        }

        var slot = (byte)(_nextSlot++);
        var client = new ConnectedClient
        {
            Slot = slot,
            Endpoint = endpoint,
            DeviceName = deviceName,
            ConnectedAt = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow,
            IsConnected = true,
        };

        _clients[slot] = client;
        _endpointToSlot[endpoint] = slot;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[+] Player {slot + 1} connected: {deviceName} ({endpoint})");
        Console.ResetColor();

        return slot;
    }

    public bool TryGetSlot(IPEndPoint endpoint, out int slot)
        => _endpointToSlot.TryGetValue(endpoint, out slot);

    public ConnectedClient? GetClient(int slot)
    {
        _clients.TryGetValue(slot, out var client);
        return client;
    }

    public void MarkDisconnected(int slot)
    {
        if (_clients.TryGetValue(slot, out var client) && client.IsConnected)
        {
            client.IsConnected = false;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[-] Player {slot + 1} disconnected: {client.DeviceName}");
            Console.ResetColor();
        }
    }

    public void RemoveStaleClients(TimeSpan timeout)
    {
        foreach (var kvp in _clients)
        {
            if (kvp.Value.IsConnected && (DateTime.UtcNow - kvp.Value.LastSeen) > timeout)
                MarkDisconnected(kvp.Key);
        }
    }

    public void Reset()
    {
        _clients.Clear();
        _endpointToSlot.Clear();
        _nextSlot = 0;
    }

    public void Dispose() => Reset();
}

/// <summary>
/// Represents a single connected controller device.
/// </summary>
public sealed class ConnectedClient
{
    public byte Slot { get; set; }
    public IPEndPoint Endpoint { get; set; } = null!;
    public string DeviceName { get; set; } = "Unknown";
    public DateTime ConnectedAt { get; set; }
    public DateTime LastSeen { get; set; }
    public bool IsConnected { get; set; }
    public ushort LastSequence { get; set; }
}
