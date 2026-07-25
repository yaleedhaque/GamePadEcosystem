using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using GamePadEcosystem.Server.Core;

namespace GamePadEcosystem.Server.VirtualController;

/// <summary>
/// Manages ViGEmBus virtual Xbox 360 controller instances.
/// Dynamically creates/destroys virtual controllers as phones connect/disconnect.
/// Maps binary input packets to native Xbox 360 controller state.
/// </summary>
public sealed class ControllerManager : IDisposable
{
    private readonly ViGEmClient _viGemClient;
    private readonly IXbox360Controller?[] _controllers = new IXbox360Controller[Protocol.MaxPlayers];
    private readonly bool[] _active = new bool[Protocol.MaxPlayers];
    private readonly DateTime[] _lastInput = new DateTime[Protocol.MaxPlayers];
    private readonly object[] _locks = new object[Protocol.MaxPlayers];

    public ControllerManager()
    {
        for (int i = 0; i < Protocol.MaxPlayers; i++)
            _locks[i] = new object();

        _viGemClient = new ViGEmClient();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("[ViGEm] ViGEmClient initialized — driver connected");
        Console.ResetColor();
    }

    /// <summary>
    /// Creates a new virtual Xbox 360 controller for the given player slot.
    /// </summary>
    public void CreateController(int slot)
    {
        if (slot < 0 || slot >= Protocol.MaxPlayers) return;

        lock (_locks[slot])
        {
            if (_controllers[slot] != null) return;

            try
            {
                var controller = _viGemClient.CreateXbox360Controller();
                controller.Connect();
                _controllers[slot] = controller;
                _active[slot] = true;
                _lastInput[slot] = DateTime.UtcNow;

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"[ViGEm] Xbox 360 Controller #{slot + 1} connected");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ViGEm] Failed to create controller #{slot + 1}: {ex.Message}");
                Console.ResetColor();
            }
        }
    }

    /// <summary>
    /// Forwards an input packet to the matching virtual controller.
    /// Auto-creates the controller if it doesn't exist yet.
    /// </summary>
    public void UpdateController(int slot, InputPacket packet)
    {
        if (slot < 0 || slot >= Protocol.MaxPlayers) return;
        if (_controllers[slot] == null) CreateController(slot);

        lock (_locks[slot])
        {
            var controller = _controllers[slot];
            if (controller == null || !_active[slot]) return;

            try
            {
                _lastInput[slot] = DateTime.UtcNow;

                // Map digital buttons using static fields on Xbox360Button
                controller.SetButtonState(Xbox360Button.A, (packet.Buttons & (uint)Protocol.ButtonFlag.A) != 0);
                controller.SetButtonState(Xbox360Button.B, (packet.Buttons & (uint)Protocol.ButtonFlag.B) != 0);
                controller.SetButtonState(Xbox360Button.X, (packet.Buttons & (uint)Protocol.ButtonFlag.X) != 0);
                controller.SetButtonState(Xbox360Button.Y, (packet.Buttons & (uint)Protocol.ButtonFlag.Y) != 0);
                controller.SetButtonState(Xbox360Button.LeftShoulder, (packet.Buttons & (uint)Protocol.ButtonFlag.LeftBumper) != 0);
                controller.SetButtonState(Xbox360Button.RightShoulder, (packet.Buttons & (uint)Protocol.ButtonFlag.RightBumper) != 0);
                controller.SetButtonState(Xbox360Button.Back, (packet.Buttons & (uint)Protocol.ButtonFlag.Back) != 0);
                controller.SetButtonState(Xbox360Button.Start, (packet.Buttons & (uint)Protocol.ButtonFlag.Start) != 0);
                controller.SetButtonState(Xbox360Button.LeftThumb, (packet.Buttons & (uint)Protocol.ButtonFlag.LeftStick) != 0);
                controller.SetButtonState(Xbox360Button.RightThumb, (packet.Buttons & (uint)Protocol.ButtonFlag.RightStick) != 0);
                controller.SetButtonState(Xbox360Button.Up, (packet.Buttons & (uint)Protocol.ButtonFlag.DPadUp) != 0);
                controller.SetButtonState(Xbox360Button.Down, (packet.Buttons & (uint)Protocol.ButtonFlag.DPadDown) != 0);
                controller.SetButtonState(Xbox360Button.Left, (packet.Buttons & (uint)Protocol.ButtonFlag.DPadLeft) != 0);
                controller.SetButtonState(Xbox360Button.Right, (packet.Buttons & (uint)Protocol.ButtonFlag.DPadRight) != 0);
                controller.SetButtonState(Xbox360Button.Guide, (packet.Buttons & (uint)Protocol.ButtonFlag.Guide) != 0);

                // Map analog thumbsticks
                controller.SetAxisValue(Xbox360Axis.LeftThumbX, packet.LeftX);
                controller.SetAxisValue(Xbox360Axis.LeftThumbY, packet.LeftY);
                controller.SetAxisValue(Xbox360Axis.RightThumbX, packet.RightX);
                controller.SetAxisValue(Xbox360Axis.RightThumbY, packet.RightY);

                // Map analog triggers
                controller.SetSliderValue(Xbox360Slider.LeftTrigger, packet.LeftTrigger);
                controller.SetSliderValue(Xbox360Slider.RightTrigger, packet.RightTrigger);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ViGEm] Error updating controller #{slot + 1}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Resets all inputs to neutral — prevents "stick drift" on disconnected controllers.
    /// </summary>
    public void ZeroController(int slot)
    {
        if (slot < 0 || slot >= Protocol.MaxPlayers) return;

        lock (_locks[slot])
        {
            var controller = _controllers[slot];
            if (controller == null) return;

            try
            {
                controller.SetButtonState(Xbox360Button.A, false);
                controller.SetButtonState(Xbox360Button.B, false);
                controller.SetButtonState(Xbox360Button.X, false);
                controller.SetButtonState(Xbox360Button.Y, false);
                controller.SetButtonState(Xbox360Button.LeftShoulder, false);
                controller.SetButtonState(Xbox360Button.RightShoulder, false);
                controller.SetButtonState(Xbox360Button.Back, false);
                controller.SetButtonState(Xbox360Button.Start, false);
                controller.SetButtonState(Xbox360Button.Up, false);
                controller.SetButtonState(Xbox360Button.Down, false);
                controller.SetButtonState(Xbox360Button.Left, false);
                controller.SetButtonState(Xbox360Button.Right, false);
                controller.SetButtonState(Xbox360Button.LeftThumb, false);
                controller.SetButtonState(Xbox360Button.RightThumb, false);
                controller.SetButtonState(Xbox360Button.Guide, false);

                controller.SetAxisValue(Xbox360Axis.LeftThumbX, 0);
                controller.SetAxisValue(Xbox360Axis.LeftThumbY, 0);
                controller.SetAxisValue(Xbox360Axis.RightThumbX, 0);
                controller.SetAxisValue(Xbox360Axis.RightThumbY, 0);

                controller.SetSliderValue(Xbox360Slider.LeftTrigger, 0);
                controller.SetSliderValue(Xbox360Slider.RightTrigger, 0);
            }
            catch { /* Controller may be in bad state */ }
        }
    }

    public void DestroyController(int slot)
    {
        if (slot < 0 || slot >= Protocol.MaxPlayers) return;

        lock (_locks[slot])
        {
            try
            {
                _controllers[slot]?.Disconnect();
                _controllers[slot] = null;
                _active[slot] = false;

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[ViGEm] Xbox 360 Controller #{slot + 1} disconnected");
                Console.ResetColor();
            }
            catch { }
        }
    }

    /// <summary>
    /// Checks for timed-out controllers, zeros inputs, and disconnects them.
    /// Prevents runaway inputs from dropped connections.
    /// </summary>
    public void CheckDisconnections(TimeSpan timeout)
    {
        for (int i = 0; i < Protocol.MaxPlayers; i++)
        {
            if (_active[i] && (DateTime.UtcNow - _lastInput[i]) > timeout)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"[ViGEm] Player {i + 1} timed out — zeroing and disconnecting controller");
                Console.ResetColor();
                ZeroController(i);
                DestroyController(i);
            }
        }
    }

    public void Dispose()
    {
        for (int i = 0; i < Protocol.MaxPlayers; i++)
            DestroyController(i);
        _viGemClient?.Dispose();
    }
}
