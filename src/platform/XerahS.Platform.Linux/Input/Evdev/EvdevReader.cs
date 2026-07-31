#region License Information (GPL v3)

/*
    XerahS - The Avalonia UI implementation of ShareX
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using System.Runtime.InteropServices;
using System.Threading;
using XerahS.Common;

namespace XerahS.Platform.Linux.Input.Evdev;

/// <summary>
/// Opens a single <c>/dev/input/event*</c> device and raises an event for every
/// raw input event reported by the kernel. Modeled on the CrossMacro EvdevReader,
/// including SYN_DROPPED resynchronisation so key state is not lost after overflow.
/// </summary>
internal sealed class EvdevReader : IDisposable
{
    private readonly string _devicePath;
    private int _fd = -1;
    private CancellationTokenSource? _cts;
    private Thread? _readThread;
    private bool _syncing;
    private byte[]? _lastKeyState;

    public string DeviceName { get; }

    public event Action<EvdevReader, EvdevNative.InputEvent>? EventReceived;
    public event Action<Exception>? ErrorOccurred;

    public bool IsListening { get; private set; }

    public EvdevReader(string devicePath, string deviceName)
    {
        _devicePath = devicePath;
        DeviceName = string.IsNullOrWhiteSpace(deviceName) ? devicePath : deviceName;
    }

    public void Start()
    {
        if (IsListening)
        {
            return;
        }

        _fd = EvdevNative.open(_devicePath, EvdevNative.O_RDONLY);
        if (_fd < 0)
        {
            int errno = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"Failed to open evdev device {_devicePath} (errno {errno}). " +
                "The current user likely needs read access to input devices (input group / udev rule).");
        }

        _cts = new CancellationTokenSource();
        IsListening = true;
        _readThread = new Thread(() => ReadLoop(_cts.Token))
        {
            IsBackground = true,
            Name = $"XerahS-evdev-{Path.GetFileName(_devicePath)}"
        };
        _readThread.Start();

        DebugHelper.WriteLine($"EvdevReader: Listening on {DeviceName} ({_devicePath})");
    }

    public void Stop()
    {
        if (!IsListening)
        {
            return;
        }

        _cts?.Cancel();
        CloseDevice();

        try
        {
            _readThread?.Join(250);
        }
        catch (ThreadStateException)
        {
            // Thread never started; nothing to join.
        }

        IsListening = false;
        DebugHelper.WriteLine($"EvdevReader: Stopped listening on {DeviceName}");
    }

    private void CloseDevice()
    {
        if (_fd >= 0)
        {
            EvdevNative.close(_fd);
            _fd = -1;
        }
    }

    private void ReadLoop(CancellationToken token)
    {
        int eventSize = Marshal.SizeOf<EvdevNative.InputEvent>();
        IntPtr buffer = Marshal.AllocHGlobal(eventSize);

        try
        {
            while (!token.IsCancellationRequested && _fd >= 0)
            {
                IntPtr bytesRead = EvdevNative.read(_fd, buffer, (IntPtr)eventSize);
                long count = bytesRead.ToInt64();

                if (count == eventSize)
                {
                    var ev = Marshal.PtrToStructure<EvdevNative.InputEvent>(buffer);
                    DispatchEvent(ev);
                }
                else if (count < 0)
                {
                    int errno = Marshal.GetLastWin32Error();
                    if (errno == EvdevNative.EBADF)
                    {
                        break; // Device closed underneath us.
                    }

                    if (errno == EvdevNative.EINTR || errno == EvdevNative.EAGAIN)
                    {
                        continue;
                    }

                    throw new IOException($"evdev read error on {_devicePath} (errno {errno})");
                }
                else if (count == 0)
                {
                    break; // EOF, e.g. device unplugged.
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                ErrorOccurred?.Invoke(ex);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private void DispatchEvent(EvdevNative.InputEvent ev)
    {
        if (ev.Type == InputEventCodes.EV_SYN && ev.Code == InputEventCodes.SYN_DROPPED)
        {
            _syncing = true;
            DebugHelper.WriteLine($"EvdevReader: SYN_DROPPED on {DeviceName}; resyncing key state.");
            return;
        }

        if (ev.Type == InputEventCodes.EV_SYN && ev.Code == InputEventCodes.SYN_REPORT)
        {
            if (_syncing)
            {
                ResyncKeyState();
                _syncing = false;
            }

            EventReceived?.Invoke(this, ev);
            return;
        }

        if (_syncing)
        {
            return;
        }

        EventReceived?.Invoke(this, ev);
    }

    private void ResyncKeyState()
    {
        var currentKeyState = new byte[96];
        int result = EvdevNative.ioctl(_fd, EvdevNative.EVIOCGKEY_96, currentKeyState);
        if (result < 0)
        {
            DebugHelper.WriteLine($"EvdevReader: EVIOCGKEY failed during resync on {DeviceName} (errno {Marshal.GetLastWin32Error()}).");
            return;
        }

        if (_lastKeyState == null)
        {
            _lastKeyState = new byte[96];
            Array.Copy(currentKeyState, _lastKeyState, 96);
            return;
        }

        for (int keyCode = 0; keyCode <= InputEventCodes.KeyMax; keyCode++)
        {
            int byteIndex = keyCode / 8;
            int bitIndex = keyCode % 8;
            if (byteIndex >= currentKeyState.Length)
            {
                continue;
            }

            bool currentlyPressed = (currentKeyState[byteIndex] & (1 << bitIndex)) != 0;
            bool wasPressed = (_lastKeyState[byteIndex] & (1 << bitIndex)) != 0;
            if (currentlyPressed == wasPressed)
            {
                continue;
            }

            EventReceived?.Invoke(this, new EvdevNative.InputEvent
            {
                Type = InputEventCodes.EV_KEY,
                Code = (ushort)keyCode,
                Value = currentlyPressed ? 1 : 0
            });
        }

        Array.Copy(currentKeyState, _lastKeyState, 96);
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}
