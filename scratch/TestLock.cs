using System;
using System.Runtime.InteropServices;
using System.Threading;

class Program
{
    [DllImport("user32.dll")]
    public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    public static extern short GetKeyState(int nVirtKey);

    const int VK_CAPITAL = 0x14;
    const int VK_NUMLOCK = 0x90;

    static void Main()
    {
        Console.WriteLine("Monitoring CapsLock and NumLock for 30 seconds. Try toggling them on OSK.");
        for (int i = 0; i < 300; i++)
        {
            if ((GetKeyState(VK_CAPITAL) & 0x0001) != 0)
            {
                // CapsLock is ON, turn it OFF
                keybd_event(VK_CAPITAL, 0x3A, 0, UIntPtr.Zero);
                keybd_event(VK_CAPITAL, 0x3A, 0x0002, UIntPtr.Zero);
                Console.WriteLine("Turned off CapsLock");
            }
            
            if ((GetKeyState(VK_NUMLOCK) & 0x0001) == 0)
            {
                // NumLock is OFF, turn it ON
                keybd_event(VK_NUMLOCK, 0x45, 0x0001, UIntPtr.Zero);
                keybd_event(VK_NUMLOCK, 0x45, 0x0001 | 0x0002, UIntPtr.Zero);
                Console.WriteLine("Turned on NumLock");
            }
            
            Thread.Sleep(100);
        }
    }
}
