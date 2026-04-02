using System.Runtime.InteropServices;
using SmartMouth.App.Models;

namespace SmartMouth.App.Services;

public sealed class GlobalTriggerListener : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;

    private const int WmKeyDown = 0x0100;
    private const int WmSysKeyDown = 0x0104;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyUp = 0x0105;

    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonUp = 0x0202;
    private const int WmRButtonDown = 0x0204;
    private const int WmRButtonUp = 0x0205;
    private const int WmMButtonDown = 0x0207;
    private const int WmMButtonUp = 0x0208;
    private const int WmXButtonDown = 0x020B;
    private const int WmXButtonUp = 0x020C;

    private readonly HookProc _keyboardProc;
    private readonly HookProc _mouseProc;
    private readonly object _syncRoot = new();

    private IntPtr _keyboardHook = IntPtr.Zero;
    private IntPtr _mouseHook = IntPtr.Zero;
    private bool _isStarted;
    private bool _triggerPressed;

    private TriggerInputType _triggerInputType;
    private int _triggerVirtualKey;
    private TriggerMouseButton _triggerMouseButton;
    private TriggerMode _triggerMode;

    public GlobalTriggerListener()
    {
        _keyboardProc = KeyboardHookCallback;
        _mouseProc = MouseHookCallback;
    }

    public event Action<bool>? HoldStateChanged;
    public event Action? ToggleRequested;

    public void UpdateBinding(TriggerInputType inputType, int virtualKey, TriggerMouseButton mouseButton, TriggerMode mode)
    {
        lock (_syncRoot)
        {
            _triggerInputType = inputType;
            _triggerVirtualKey = virtualKey;
            _triggerMouseButton = mouseButton;
            _triggerMode = mode;
            _triggerPressed = false;
        }
    }

    public void Start()
    {
        lock (_syncRoot)
        {
            if (_isStarted)
            {
                return;
            }

            _keyboardHook = SetWindowsHookEx(WhKeyboardLl, _keyboardProc, IntPtr.Zero, 0);
            _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, IntPtr.Zero, 0);
            _isStarted = _keyboardHook != IntPtr.Zero && _mouseHook != IntPtr.Zero;

            if (!_isStarted)
            {
                if (_keyboardHook != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_keyboardHook);
                    _keyboardHook = IntPtr.Zero;
                }

                if (_mouseHook != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_mouseHook);
                    _mouseHook = IntPtr.Zero;
                }

                throw new InvalidOperationException("Unable to install global keyboard/mouse hooks.");
            }
        }
    }

    public void Stop()
    {
        lock (_syncRoot)
        {
            if (!_isStarted)
            {
                return;
            }

            if (_keyboardHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_keyboardHook);
                _keyboardHook = IntPtr.Zero;
            }

            if (_mouseHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }

            _isStarted = false;
            if (_triggerMode == TriggerMode.Hold && _triggerPressed)
            {
                _triggerPressed = false;
                HoldStateChanged?.Invoke(false);
            }
        }
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            if (message is WmKeyDown or WmSysKeyDown or WmKeyUp or WmSysKeyUp)
            {
                var keyboardData = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);
                var isBound = IsKeyboardBound((int)keyboardData.VkCode);
                if (isBound)
                {
                    var isDown = message is WmKeyDown or WmSysKeyDown;
                    HandleTriggerTransition(isDown);
                }
            }
        }

        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            if (message is WmLButtonDown or WmLButtonUp or WmRButtonDown or WmRButtonUp or WmMButtonDown or WmMButtonUp or WmXButtonDown or WmXButtonUp)
            {
                var mouseData = Marshal.PtrToStructure<MsLlHookStruct>(lParam);
                var isBound = IsMouseBound(message, mouseData.MouseData);
                if (isBound)
                {
                    var isDown = message is WmLButtonDown or WmRButtonDown or WmMButtonDown or WmXButtonDown;
                    HandleTriggerTransition(isDown);
                }
            }
        }

        return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
    }

    private bool IsKeyboardBound(int vkCode)
    {
        lock (_syncRoot)
        {
            return _triggerInputType == TriggerInputType.Keyboard && vkCode == _triggerVirtualKey;
        }
    }

    private bool IsMouseBound(int message, uint mouseData)
    {
        lock (_syncRoot)
        {
            if (_triggerInputType != TriggerInputType.Mouse)
            {
                return false;
            }

            return _triggerMouseButton switch
            {
                TriggerMouseButton.Left => message is WmLButtonDown or WmLButtonUp,
                TriggerMouseButton.Right => message is WmRButtonDown or WmRButtonUp,
                TriggerMouseButton.Middle => message is WmMButtonDown or WmMButtonUp,
                TriggerMouseButton.XButton1 => message is WmXButtonDown or WmXButtonUp && HighWord(mouseData) == 1,
                TriggerMouseButton.XButton2 => message is WmXButtonDown or WmXButtonUp && HighWord(mouseData) == 2,
                _ => false
            };
        }
    }

    private void HandleTriggerTransition(bool isDown)
    {
        TriggerMode mode;
        lock (_syncRoot)
        {
            mode = _triggerMode;
            if (isDown)
            {
                if (_triggerPressed)
                {
                    return;
                }

                _triggerPressed = true;
            }
            else
            {
                if (!_triggerPressed)
                {
                    return;
                }

                _triggerPressed = false;
            }
        }

        if (mode == TriggerMode.Hold)
        {
            HoldStateChanged?.Invoke(isDown);
            return;
        }

        if (isDown)
        {
            ToggleRequested?.Invoke();
        }
    }

    private static uint HighWord(uint value) => (value >> 16) & 0xFFFF;

    public void Dispose()
    {
        Stop();
    }

    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint VkCode;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr DwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MsLlHookStruct
    {
        public Point Pt;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr DwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hmod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
}
