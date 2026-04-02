using System.Runtime.InteropServices;

namespace SmartMouth.App.Services;

public sealed class MouseController
{
    public void MoveBy(int deltaX, int deltaY)
    {
        if (!GetCursorPos(out var current))
        {
            return;
        }

        SetCursorPos(current.X + deltaX, current.Y + deltaY);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out Point lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetCursorPos(int x, int y);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }
}
