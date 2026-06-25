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
        
        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vlc);

        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        
        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

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

        public TrayContext()
        {
            AppConfig.Load();
            
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
            
            ApplyHotkey();
            
            trayIcon.ShowBalloonTip(3000, "WuRuSwitch", "Program is running. Right click for settings.", ToolTipIcon.Info);
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

        public void ApplyHotkey()
        {
            UnregisterHotKey(hkWindow.Handle, hotkeyId);
            int modifiers = 0;
            if (AppConfig.Alt) modifiers |= MOD_ALT;
            if (AppConfig.Ctrl) modifiers |= MOD_CONTROL;
            if (AppConfig.Shift) modifiers |= MOD_SHIFT;
            if (AppConfig.Win) modifiers |= MOD_WIN;

            Keys vk = Keys.F6;
            Enum.TryParse(AppConfig.Key, out vk);
            
            bool success = RegisterHotKey(hkWindow.Handle, hotkeyId, modifiers, (int)vk);
            if (!success) {
                // Retry once
                Thread.Sleep(200);
                RegisterHotKey(hkWindow.Handle, hotkeyId, modifiers, (int)vk);
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
            UnregisterHotKey(hkWindow.Handle, hotkeyId);
            trayIcon.Visible = false;
            Application.Exit();
        }

        public void OnHotkeyPressed()
        {
            // Release modifiers
            const uint KEYEVENTF_KEYUP = 0x0002;
            keybd_event(0x11, 0, KEYEVENTF_KEYUP, UIntPtr.Zero); // ctrl
            keybd_event(0x12, 0, KEYEVENTF_KEYUP, UIntPtr.Zero); // alt
            keybd_event(0x10, 0, KEYEVENTF_KEYUP, UIntPtr.Zero); // shift
            keybd_event(0x5B, 0, KEYEVENTF_KEYUP, UIntPtr.Zero); // lwin
            keybd_event(0x5C, 0, KEYEVENTF_KEYUP, UIntPtr.Zero); // rwin
            
            Thread.Sleep(100);
            
            string oldClipboard = "";
            try { oldClipboard = Clipboard.GetText(); } catch { }
            
            Clipboard.Clear();
            SendKeys.SendWait("^c");
            
            // Wait for clipboard to populate
            Thread.Sleep(150);
            string text = "";
            try { text = Clipboard.GetText(); } catch { }
            
            if (string.IsNullOrEmpty(text))
            {
                // No text selected, attempt to select previous word
                SendKeys.SendWait("^+{LEFT}");
                Thread.Sleep(50);
                
                Clipboard.Clear();
                SendKeys.SendWait("^c");
                Thread.Sleep(150);
                
                try { text = Clipboard.GetText(); } catch { }
                
                if (string.IsNullOrEmpty(text))
                {
                    try { if (!string.IsNullOrEmpty(oldClipboard)) Clipboard.SetText(oldClipboard); } catch { }
                    return;
                }
            }

            string converted = ConvertText(text);
            try { Clipboard.SetText(converted); } catch { }
            
            SendKeys.SendWait("^v");
            Thread.Sleep(100);
            
            // Restore original clipboard text
            try { if (!string.IsNullOrEmpty(oldClipboard)) Clipboard.SetText(oldClipboard); else Clipboard.Clear(); } catch { }
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

    public static class AppConfig
    {
        public static string Key = "F6";
        public static bool Ctrl = false;
        public static bool Alt = false;
        public static bool Shift = false;
        public static bool Win = false;
        public static bool Startup = false;

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
                        Key = lines[0];
                        bool.TryParse(lines[1], out Ctrl);
                        bool.TryParse(lines[2], out Alt);
                        bool.TryParse(lines[3], out Shift);
                        bool.TryParse(lines[4], out Win);
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
                File.WriteAllLines(ConfigPath, new string[] { Key, Ctrl.ToString(), Alt.ToString(), Shift.ToString(), Win.ToString() });
            }
            catch { }
        }
    }

    public class SettingsForm : Form
    {
        private TrayContext context;
        private CheckBox cbCtrl, cbAlt, cbShift, cbWin, cbStartup;
        private ComboBox comboKey;
        private Button btnSave;

        public SettingsForm(TrayContext ctx)
        {
            context = ctx;
            this.Text = "WuRuSwitch Settings";
            this.Size = new Size(350, 250);
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

            cbStartup = new CheckBox() { Text = "Run on Windows Startup (เปิดพร้อมคอม)", Location = new Point(20, 125), Width = 250 };
            this.Controls.Add(cbStartup);

            btnSave = new Button() { Text = "Apply & Hide (บันทึกและซ่อน)", Location = new Point(20, 160), Width = 280, Height = 35, BackColor = Color.LightSkyBlue };
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
