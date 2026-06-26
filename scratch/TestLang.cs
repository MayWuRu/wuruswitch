using System;
using System.Runtime.InteropServices;
using System.Threading;

class Program
{
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);

    const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;

    static void Main()
    {
        Console.WriteLine("Switching to Thai in 2 seconds...");
        Thread.Sleep(2000);
        IntPtr hwnd = GetForegroundWindow();
        IntPtr hklThai = LoadKeyboardLayout("0000041E", 1);
        PostMessage(hwnd, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, hklThai);
        Console.WriteLine("Done.");
        
        Thread.Sleep(2000);
        Console.WriteLine("Switching to English in 2 seconds...");
        hwnd = GetForegroundWindow();
        IntPtr hklEng = LoadKeyboardLayout("00000409", 1);
        PostMessage(hwnd, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, hklEng);
        Console.WriteLine("Done.");
    }
}
