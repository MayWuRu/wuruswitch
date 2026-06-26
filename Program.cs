using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Microsoft.Win32;

namespace LangSwitch
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new TrayContext());
        }
    }

    public class TrayContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        private SettingsForm settingsForm;
        
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        
        // --- Low Level Keyboard Hook API ---
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        // -----------------------------------
        
        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public InputUnion U;
            public static int Size { get { return Marshal.SizeOf(typeof(INPUT)); } }
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)]
            public MOUSEINPUT mi;
            [FieldOffset(0)]
            public KEYBDINPUT ki;
            [FieldOffset(0)]
            public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_SCANCODE = 0x0008;

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);



        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        public static extern IntPtr LoadKeyboardLayout(string pwszKLID, uint Flags);

        [DllImport("user32.dll")]
        public static extern IntPtr GetKeyboardLayout(uint idThread);

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
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ClientToScreen(IntPtr hWnd, ref Point lpPoint);

        [DllImport("user32.dll")]
        public static extern short GetKeyState(int nVirtKey);




        private const int VK_CAPITAL = 0x14;
        private const int VK_NUMLOCK = 0x90;
        private const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;

        private const int MOD_ALT = 0x0001;
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_SHIFT = 0x0004;
        private const int MOD_WIN = 0x0008;

        private int hotkeyId = 1;
        
        // Dummy form for catching hotkeys
        private HotkeyWindow hkWindow;

        private class HotkeyWindow : NativeWindow, IDisposable
        {
            private TrayContext context;
            public HotkeyWindow(TrayContext ctx)
            {
                context = ctx;
                CreateHandle(new CreateParams());
            }

            protected override void WndProc(ref Message m)
            {
                const int WM_HOTKEY = 0x0312;
                if (m.Msg == WM_HOTKEY)
                {
                    context.OnHotkeyPressed();
                }
                base.WndProc(ref m);
            }

            public void Dispose()
            {
                DestroyHandle();
            }
        }

        private IndicatorForm indicatorForm;
        private System.Windows.Forms.Timer indicatorTimer;

        private LowLevelKeyboardProc _proc;
        private IntPtr _hookID = IntPtr.Zero;

        public TrayContext()
        {
            AppConfig.Load();

            _proc = HookCallback;
            _hookID = SetHook(_proc);
            
            trayIcon = new NotifyIcon()
            {
                Icon = CreateIcon(),
                ContextMenu = new ContextMenu(new MenuItem[] {
                    new MenuItem("Settings", ShowSettings),
                    new MenuItem("Exit", Exit)
                }),
                Visible = true,
                Text = "WuRuSwitch"
            };
            
            trayIcon.DoubleClick += ShowSettings;

            hkWindow = new HotkeyWindow(this);
            settingsForm = new SettingsForm(this);
            indicatorForm = new IndicatorForm();
            
            indicatorTimer = new System.Windows.Forms.Timer();
            indicatorTimer.Interval = 100;
            indicatorTimer.Tick += IndicatorTimer_Tick;
            indicatorTimer.Start();
            
            ApplyHotkey();
            
            trayIcon.ShowBalloonTip(3000, "WuRuSwitch", "Program is running. Right click for settings.", ToolTipIcon.Info);
            IndicatorTimer_Tick(null, null);
        }

        private IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public int vkCode;
            public int scanCode;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                KBDLLHOOKSTRUCT kbdStruct = (KBDLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(KBDLLHOOKSTRUCT));
                bool isInjected = (kbdStruct.flags & 0x10) != 0;
                
                if (!isInjected)
                {
                    if (kbdStruct.vkCode == VK_CAPITAL && AppConfig.AlwaysDisableCapsLock)
                    {
                        return (IntPtr)1; // Block CapsLock
                    }
                    if (kbdStruct.vkCode == VK_NUMLOCK && AppConfig.AlwaysEnableNumLock)
                    {
                        return (IntPtr)1; // Block NumLock
                    }
                }
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void IndicatorTimer_Tick(object sender, EventArgs e)
        {
            // Enforce Lock Keys
            if (AppConfig.AlwaysDisableCapsLock)
            {
                if ((GetKeyState(VK_CAPITAL) & 0x0001) != 0)
                {
                    keybd_event((byte)VK_CAPITAL, 0x3A, 0, UIntPtr.Zero);
                    keybd_event((byte)VK_CAPITAL, 0x3A, KEYEVENTF_KEYUP, UIntPtr.Zero);
                }
            }

            if (AppConfig.AlwaysEnableNumLock)
            {
                if ((GetKeyState(VK_NUMLOCK) & 0x0001) == 0)
                {
                    keybd_event((byte)VK_NUMLOCK, 0x45, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
                    keybd_event((byte)VK_NUMLOCK, 0x45, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
                }
            }

            if (!AppConfig.ShowLangIndicator)
            {
                if (indicatorForm.Visible) indicatorForm.Hide();
                return;
            }

            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                if (indicatorForm.Visible) indicatorForm.Hide();
                return;
            }

            uint pid;
            uint threadId = GetWindowThreadProcessId(hwnd, out pid);
            
            GUITHREADINFO gti = new GUITHREADINFO();
            gti.cbSize = Marshal.SizeOf(gti);
            
            if (GetGUIThreadInfo(threadId, ref gti) && gti.hwndCaret != IntPtr.Zero)
            {
                Point pt = new Point(gti.rcCaret.Left, gti.rcCaret.Bottom);
                ClientToScreen(gti.hwndCaret, ref pt);
                
                IntPtr hkl = GetKeyboardLayout(threadId);
                long langId = hkl.ToInt64() & 0xFFFF;
                bool isThai = (langId == 0x041E);
                
                bool capsLock = (GetKeyState(VK_CAPITAL) & 0x0001) != 0;
                
                if (indicatorForm.IsThai != isThai || indicatorForm.IsCapsLock != capsLock)
                {
                    indicatorForm.IsThai = isThai;
                    indicatorForm.IsCapsLock = capsLock;
                    indicatorForm.Invalidate();
                }
                
                pt.X += 5;
                pt.Y += 5;
                
                if (indicatorForm.Location != pt)
                {
                    indicatorForm.Location = pt;
                }
                
                if (!indicatorForm.Visible)
                {
                    indicatorForm.Show();
                }
            }
            else
            {
                if (indicatorForm.Visible) indicatorForm.Hide();
            }
        }


        private Icon CreateIcon()
        {
            try {
                if (File.Exists("logo.ico")) return new Icon("logo.ico");
                return Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            } catch {
                Bitmap bmp = new Bitmap(32, 32);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.FromArgb(41, 128, 185));
                    g.DrawString("WS", new Font("Arial", 11, FontStyle.Bold), Brushes.White, new PointF(1, 6));
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        private System.Windows.Forms.Timer retryTimer;

        public void ApplyHotkey()
        {
            if (retryTimer != null) {
                retryTimer.Stop();
                retryTimer.Dispose();
                retryTimer = null;
            }

            UnregisterHotKey(hkWindow.Handle, hotkeyId);
            uint modifiers = 0;
            if (AppConfig.Alt) modifiers |= MOD_ALT;
            if (AppConfig.Ctrl) modifiers |= MOD_CONTROL;
            if (AppConfig.Shift) modifiers |= MOD_SHIFT;
            if (AppConfig.Win) modifiers |= MOD_WIN;

            Keys vk = Keys.F6;
            if (!Enum.TryParse(AppConfig.Key, true, out vk))
            {
                vk = Keys.F6;
            }
            
            TryRegisterHotKey(modifiers, (uint)vk, 15);
        }

        private void TryRegisterHotKey(uint modifiers, uint vk, int attemptsLeft)
        {
            bool success = RegisterHotKey(hkWindow.Handle, hotkeyId, modifiers, vk);
            if (!success && attemptsLeft > 0)
            {
                retryTimer = new System.Windows.Forms.Timer();
                retryTimer.Interval = 1000;
                retryTimer.Tick += (s, e) => {
                    if (retryTimer != null) {
                        retryTimer.Stop();
                        retryTimer.Dispose();
                        retryTimer = null;
                    }
                    TryRegisterHotKey(modifiers, vk, attemptsLeft - 1);
                };
                retryTimer.Start();
            }
        }

        public void ShowSettings(object sender, EventArgs e)
        {
            if (settingsForm.Visible)
            {
                settingsForm.Activate();
            }
            else
            {
                settingsForm.ShowDialog();
            }
        }

        public void Exit(object sender, EventArgs e)
        {
            if (retryTimer != null) {
                retryTimer.Stop();
                retryTimer.Dispose();
                retryTimer = null;
            }
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
            UnregisterHotKey(hkWindow.Handle, hotkeyId);
            trayIcon.Visible = false;
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (indicatorForm != null) indicatorForm.Dispose();
                if (settingsForm != null) settingsForm.Dispose();
                if (indicatorTimer != null) indicatorTimer.Dispose();
                if (hkWindow != null) hkWindow.Dispose();
                if (trayIcon != null) trayIcon.Dispose();
            }
            if (_hookID != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookID);
                _hookID = IntPtr.Zero;
            }
            base.Dispose(disposing);
        }

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        private void SendKeyWithModifier(byte modifier, byte key, bool isExtendedKey = false)
        {
            uint ext = isExtendedKey ? 0x0001u : 0u;
            keybd_event(modifier, 0, 0, UIntPtr.Zero);
            keybd_event(key, 0, ext, UIntPtr.Zero);
            Thread.Sleep(10);
            keybd_event(key, 0, ext | 0x0002u, UIntPtr.Zero);
            keybd_event(modifier, 0, 0x0002, UIntPtr.Zero);
        }

        private void SendExtendedKey(byte key)
        {
            keybd_event(key, 0, 0x0001, UIntPtr.Zero);
            Thread.Sleep(10);
            keybd_event(key, 0, 0x0001 | 0x0002, UIntPtr.Zero);
        }

        public void OnHotkeyPressed()
        {
            // Release modifiers if they are held down
            const uint KEYEVENTF_KEYUP = 0x0002;
            if ((GetAsyncKeyState(0x11) & 0x8000) != 0) keybd_event(0x11, 0, KEYEVENTF_KEYUP, UIntPtr.Zero); // ctrl
            if ((GetAsyncKeyState(0x12) & 0x8000) != 0) keybd_event(0x12, 0, KEYEVENTF_KEYUP, UIntPtr.Zero); // alt
            if ((GetAsyncKeyState(0x10) & 0x8000) != 0) keybd_event(0x10, 0, KEYEVENTF_KEYUP, UIntPtr.Zero); // shift
            if ((GetAsyncKeyState(0x5B) & 0x8000) != 0) keybd_event(0x5B, 0, KEYEVENTF_KEYUP, UIntPtr.Zero); // lwin
            if ((GetAsyncKeyState(0x5C) & 0x8000) != 0) keybd_event(0x5C, 0, KEYEVENTF_KEYUP, UIntPtr.Zero); // rwin
            
            // Release the hotkey itself
            Keys vk = Keys.F6;
            Enum.TryParse(AppConfig.Key, true, out vk);
            keybd_event((byte)vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

            Thread.Sleep(50);
            
            string oldClipboard = GetClipboardText(100);
            
            for (int i = 0; i < 3; i++) { try { Clipboard.Clear(); break; } catch { Thread.Sleep(50); } }
            
            SendKeyWithModifier(0x11, 0x43); // Ctrl+C
            
            string text = GetClipboardText(300);
            
            if (string.IsNullOrEmpty(text))
            {
                // No text selected, try to select the current line to find the last word
                SendKeyWithModifier(0x10, 0x24, true); // Shift+Home
                Thread.Sleep(50);
                
                for (int i = 0; i < 3; i++) { try { Clipboard.Clear(); break; } catch { Thread.Sleep(50); } }
                
                SendKeyWithModifier(0x11, 0x43); // Ctrl+C
                
                string lineText = GetClipboardText(300);
                
                if (string.IsNullOrEmpty(lineText))
                {
                    SendExtendedKey(0x27); // Right
                    RestoreClipboard(oldClipboard);
                    return;
                }

                string trimmed = lineText.TrimEnd();
                if (trimmed.Length == 0)
                {
                    SendExtendedKey(0x27); // Right
                    RestoreClipboard(oldClipboard);
                    return;
                }

                int lastSpace = trimmed.LastIndexOfAny(new char[] { ' ', '\t', '\n', '\r' });
                int charsToSelect = lineText.Length - (lastSpace + 1);
                
                text = lineText.Substring(lineText.Length - charsToSelect);
                string prefix = lineText.Substring(0, lineText.Length - charsToSelect);
                
                string convertedLine = prefix + ConvertText(text);
                SetClipboardText(convertedLine);
                
                SendKeyWithModifier(0x11, 0x56); // Ctrl+V
                Thread.Sleep(100);
                
                RestoreClipboard(oldClipboard);
                return;
            }

            string converted = ConvertText(text);
            SetClipboardText(converted);
            
            SendKeyWithModifier(0x11, 0x56); // Ctrl+V
            Thread.Sleep(100);
            
            RestoreClipboard(oldClipboard);
        }

        private string GetClipboardText(int maxWaitMs)
        {
            int waited = 0;
            while (waited < maxWaitMs)
            {
                try 
                { 
                    if (Clipboard.ContainsText()) 
                    {
                        string txt = Clipboard.GetText();
                        if (!string.IsNullOrEmpty(txt)) return txt;
                    }
                }
                catch { }
                Thread.Sleep(20);
                waited += 20;
            }
            return "";
        }

        private void SetClipboardText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            for (int i = 0; i < 5; i++)
            {
                try { Clipboard.SetText(text); return; }
                catch { Thread.Sleep(50); }
            }
        }

        private void RestoreClipboard(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                for (int i = 0; i < 5; i++) { try { Clipboard.Clear(); return; } catch { Thread.Sleep(50); } }
            }
            else
            {
                SetClipboardText(text);
            }
        }

        private string eng_layout = "`1234567890-=qwertyuiop[]\\asdfghjkl;'zxcvbnm,./~!@#$%^&*()_+QWERTYUIOP{}|ASDFGHJKL:\"ZXCVBNM<>?";
        private string tha_layout = "_ๅ/-ภถุึคตจขชๆไำพะัีรนยบลฃฟหกดเ้่าสวงผปแอิืทมใฝ%+๑๒๓๔ู฿๕๖๗๘๙๐\"ฎฑธํ๊ณฯญฐ,ฅฤฆฏโฌ็๋ษศซ.()ฉฮฺ์?ฒฬฦ";

        private string ConvertText(string text)
        {
            HashSet<char> eng_exc = new HashSet<char>(eng_layout);
            eng_exc.ExceptWith(tha_layout);
            
            HashSet<char> tha_exc = new HashSet<char>(tha_layout);
            tha_exc.ExceptWith(eng_layout);

            int engCount = 0;
            int thaCount = 0;

            foreach (char c in text)
            {
                if (eng_exc.Contains(c)) engCount++;
                if (tha_exc.Contains(c)) thaCount++;
            }

            bool primaryIsEng = engCount >= thaCount;

            if (AppConfig.AutoSwitchKeyboard)
            {
                IntPtr hwnd = GetForegroundWindow();
                if (primaryIsEng)
                {
                    IntPtr hklThai = LoadKeyboardLayout("0000041E", 1);
                    PostMessage(hwnd, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, hklThai);
                }
                else
                {
                    IntPtr hklEng = LoadKeyboardLayout("00000409", 1);
                    PostMessage(hwnd, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, hklEng);
                }
            }

            StringBuilder sb = new StringBuilder();
            foreach (char c in text)
            {
                if (primaryIsEng)
                {
                    int idx = eng_layout.IndexOf(c);
                    if (idx >= 0) sb.Append(tha_layout[idx]);
                    else
                    {
                        idx = tha_layout.IndexOf(c);
                        if (idx >= 0) sb.Append(eng_layout[idx]);
                        else sb.Append(c);
                    }
                }
                else
                {
                    int idx = tha_layout.IndexOf(c);
                    if (idx >= 0) sb.Append(eng_layout[idx]);
                    else
                    {
                        idx = eng_layout.IndexOf(c);
                        if (idx >= 0) sb.Append(tha_layout[idx]);
                        else sb.Append(c);
                    }
                }
            }
            return sb.ToString();
        }
    }

    public class IndicatorForm : Form
    {
        private Bitmap thFlag;
        private Bitmap usFlag;
        
        public bool IsThai { get; set; }
        public bool IsCapsLock { get; set; }
        
        public IndicatorForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.BackColor = Color.Magenta;
            this.TransparencyKey = Color.Magenta;
            this.Size = new Size(50, 20);
            
            this.DoubleBuffered = true;
            CreateFlags();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x80000;    // WS_EX_LAYERED
                cp.ExStyle |= 0x20;       // WS_EX_TRANSPARENT
                cp.ExStyle |= 0x80;       // WS_EX_TOOLWINDOW
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        private void CreateFlags()
        {
            thFlag = new Bitmap(16, 11);
            using (Graphics g = Graphics.FromImage(thFlag))
            {
                g.FillRectangle(Brushes.Red, 0, 0, 16, 2);
                g.FillRectangle(Brushes.White, 0, 2, 16, 2);
                g.FillRectangle(Brushes.Blue, 0, 4, 16, 3);
                g.FillRectangle(Brushes.White, 0, 7, 16, 2);
                g.FillRectangle(Brushes.Red, 0, 9, 16, 2);
            }
            
            usFlag = new Bitmap(16, 11);
            using (Graphics g = Graphics.FromImage(usFlag))
            {
                for (int i = 0; i < 11; i++)
                    g.FillRectangle(i % 2 == 0 ? Brushes.Red : Brushes.White, 0, i, 16, 1);
                g.FillRectangle(Brushes.DarkBlue, 0, 0, 7, 6);
            }
        }
        
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.Magenta);
            
            int x = 0;
            if (IsThai) e.Graphics.DrawImageUnscaled(thFlag, x, 4);
            else e.Graphics.DrawImageUnscaled(usFlag, x, 4);
            
            x += 18;
            
            if (IsCapsLock)
            {
                using (Font f = new Font("Arial", 7, FontStyle.Bold))
                {
                    e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;
                    e.Graphics.DrawString("[A]", f, Brushes.Black, x-1, 3);
                    e.Graphics.DrawString("[A]", f, Brushes.Black, x+1, 3);
                    e.Graphics.DrawString("[A]", f, Brushes.Black, x, 2);
                    e.Graphics.DrawString("[A]", f, Brushes.Black, x, 4);
                    e.Graphics.DrawString("[A]", f, Brushes.White, x, 3);
                }
            }
        }
    }

    public static class AppConfig
    {
        public static string Key = "F6";
        public static bool Ctrl = false;
        public static bool Alt = false;
        public static bool Shift = false;
        public static bool Win = false;
        public static bool Startup = false;
        public static bool AutoSwitchKeyboard = false;
        public static bool ShowLangIndicator = false;
        public static bool AlwaysDisableCapsLock = false;
        public static bool AlwaysEnableNumLock = false;

        private static string ConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WuRuSwitch", "config.txt");

        public static void Load()
        {
            try
            {
                using (RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false))
                {
                    Startup = (rk.GetValue("WuRuSwitch") != null);
                }
            }
            catch { }

            try
            {
                if (File.Exists(ConfigPath))
                {
                    string[] lines = File.ReadAllLines(ConfigPath);
                    if (lines.Length >= 5)
                    {
                        Key = lines[0].Trim();
                        bool.TryParse(lines[1].Trim(), out Ctrl);
                        bool.TryParse(lines[2].Trim(), out Alt);
                        bool.TryParse(lines[3].Trim(), out Shift);
                        bool.TryParse(lines[4].Trim(), out Win);
                        if (lines.Length >= 6) bool.TryParse(lines[5].Trim(), out AutoSwitchKeyboard);
                        if (lines.Length >= 7) bool.TryParse(lines[6].Trim(), out ShowLangIndicator);
                        if (lines.Length >= 8) bool.TryParse(lines[7].Trim(), out AlwaysDisableCapsLock);
                        if (lines.Length >= 9) bool.TryParse(lines[8].Trim(), out AlwaysEnableNumLock);
                    }
                }
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                using (RegistryKey rk = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
                {
                    if (Startup)
                        rk.SetValue("WuRuSwitch", Application.ExecutablePath);
                    else
                        rk.DeleteValue("WuRuSwitch", false);
                }
            }
            catch { }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                File.WriteAllLines(ConfigPath, new string[] { Key, Ctrl.ToString(), Alt.ToString(), Shift.ToString(), Win.ToString(), AutoSwitchKeyboard.ToString(), ShowLangIndicator.ToString(), AlwaysDisableCapsLock.ToString(), AlwaysEnableNumLock.ToString() });
            }
            catch { }
        }
    }

    public class SettingsForm : Form
    {
        private TrayContext context;
        private CheckBox cbCtrl, cbAlt, cbShift, cbWin, cbStartup, cbAutoSwitch, cbShowLangIndicator;
        private CheckBox cbAlwaysDisableCapsLock, cbAlwaysEnableNumLock;
        private ComboBox comboKey;
        private Button btnSave;

        public SettingsForm(TrayContext ctx)
        {
            context = ctx;
            this.Text = "WuRuSwitch Settings";
            this.Size = new Size(350, 340);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;

            Label lblTitle = new Label() { Text = "Hotkey Configuration (ตั้งค่าปุ่มลัด)", Location = new Point(10, 15), AutoSize = true, Font = new Font("Arial", 10, FontStyle.Bold) };
            this.Controls.Add(lblTitle);

            cbCtrl = new CheckBox() { Text = "Ctrl", Location = new Point(20, 50), Width = 60 };
            cbAlt = new CheckBox() { Text = "Alt", Location = new Point(80, 50), Width = 60 };
            cbShift = new CheckBox() { Text = "Shift", Location = new Point(140, 50), Width = 60 };
            cbWin = new CheckBox() { Text = "Win", Location = new Point(200, 50), Width = 60 };
            
            this.Controls.Add(cbCtrl);
            this.Controls.Add(cbAlt);
            this.Controls.Add(cbShift);
            this.Controls.Add(cbWin);

            Label lblKey = new Label() { Text = "Key:", Location = new Point(20, 90), AutoSize = true };
            this.Controls.Add(lblKey);

            comboKey = new ComboBox() { Location = new Point(60, 87), Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
            
            // Add F1-F12
            for (int i = 1; i <= 12; i++) comboKey.Items.Add("F" + i);
            // Add A-Z
            for (char c = 'A'; c <= 'Z'; c++) comboKey.Items.Add(c.ToString());
            // Add 0-9
            for (int i = 0; i <= 9; i++) comboKey.Items.Add(i.ToString());

            this.Controls.Add(comboKey);

            cbStartup = new CheckBox() { Text = "Run on Windows Startup (เปิดพร้อมคอม)", Location = new Point(20, 120), Width = 250 };
            this.Controls.Add(cbStartup);

            cbAutoSwitch = new CheckBox() { Text = "Auto Switch Keyboard (สลับภาษาแป้นพิมพ์อัตโนมัติ)", Location = new Point(20, 145), Width = 300 };
            this.Controls.Add(cbAutoSwitch);

            cbShowLangIndicator = new CheckBox() { Text = "Show Lang Indicator at Cursor (แสดงธงที่เคอร์เซอร์)", Location = new Point(20, 170), Width = 300 };
            this.Controls.Add(cbShowLangIndicator);

            cbAlwaysDisableCapsLock = new CheckBox() { Text = "Always Disable CapsLock (บังคับปิด CapsLock เสมอ)", Location = new Point(20, 195), Width = 300 };
            this.Controls.Add(cbAlwaysDisableCapsLock);

            cbAlwaysEnableNumLock = new CheckBox() { Text = "Always Enable NumLock (บังคับเปิด NumLock เสมอ)", Location = new Point(20, 220), Width = 300 };
            this.Controls.Add(cbAlwaysEnableNumLock);

            btnSave = new Button() { Text = "Apply & Hide (บันทึกและซ่อน)", Location = new Point(20, 250), Width = 280, Height = 35, BackColor = Color.LightSkyBlue };
            btnSave.Click += BtnSave_Click;
            this.Controls.Add(btnSave);

            LoadData();
        }

        private void LoadData()
        {
            cbCtrl.Checked = AppConfig.Ctrl;
            cbAlt.Checked = AppConfig.Alt;
            cbShift.Checked = AppConfig.Shift;
            cbWin.Checked = AppConfig.Win;
            cbStartup.Checked = AppConfig.Startup;
            cbAutoSwitch.Checked = AppConfig.AutoSwitchKeyboard;
            cbShowLangIndicator.Checked = AppConfig.ShowLangIndicator;
            cbAlwaysDisableCapsLock.Checked = AppConfig.AlwaysDisableCapsLock;
            cbAlwaysEnableNumLock.Checked = AppConfig.AlwaysEnableNumLock;
            
            if (comboKey.Items.Contains(AppConfig.Key))
                comboKey.SelectedItem = AppConfig.Key;
            else
                comboKey.SelectedItem = "F6";
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            AppConfig.Ctrl = cbCtrl.Checked;
            AppConfig.Alt = cbAlt.Checked;
            AppConfig.Shift = cbShift.Checked;
            AppConfig.Win = cbWin.Checked;
            AppConfig.Startup = cbStartup.Checked;
            AppConfig.AutoSwitchKeyboard = cbAutoSwitch.Checked;
            AppConfig.ShowLangIndicator = cbShowLangIndicator.Checked;
            AppConfig.AlwaysDisableCapsLock = cbAlwaysDisableCapsLock.Checked;
            AppConfig.AlwaysEnableNumLock = cbAlwaysEnableNumLock.Checked;
            AppConfig.Key = comboKey.SelectedItem.ToString();
            AppConfig.Save();
            
            context.ApplyHotkey();
            this.Close();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            if (this.Visible) LoadData();
            base.OnVisibleChanged(e);
        }
    }
}
