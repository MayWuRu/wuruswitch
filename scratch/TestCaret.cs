using System;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Threading;

class Program
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GUITHREADINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public RECT rcCaret;
    }

    [DllImport("user32.dll")]
    public static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
    
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ClientToScreen(IntPtr hWnd, ref Point lpPoint);

    static void Main()
    {
        for (int i = 0; i < 10; i++)
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd != IntPtr.Zero)
            {
                uint pid;
                uint threadId = GetWindowThreadProcessId(hwnd, out pid);
                
                GUITHREADINFO gti = new GUITHREADINFO();
                gti.cbSize = Marshal.SizeOf(gti);
                
                if (GetGUIThreadInfo(threadId, ref gti))
                {
                    if (gti.hwndCaret != IntPtr.Zero)
                    {
                        Point pt = new Point(gti.rcCaret.Left, gti.rcCaret.Bottom);
                        ClientToScreen(gti.hwndCaret, ref pt);
                        Console.WriteLine($"Caret Position: X={pt.X}, Y={pt.Y}");
                    }
                    else
                    {
                        Console.WriteLine("No caret active in focused element.");
                    }
                }
            }
            Thread.Sleep(1000);
        }
    }
}
