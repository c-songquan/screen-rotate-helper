using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using System.IO;

namespace ScreenRotateHelper
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApp());
        }
    }

    class AppConfig
    {
        public string WatchExe { get; set; }
        public bool DevMode { get; set; }
        public bool AutoStart { get; set; }
        public int ScreenIndex { get; set; }
    }

    class TrayApp : Form
    {
        const string VERSION = "v1.0.0";
        const string AUTHOR = "SongQuan";
        readonly string ConfigPath =
            Path.Combine(Application.StartupPath, "config.json");

        NotifyIcon tray;
        AppConfig cfg = new AppConfig();

        const int HOTKEY_BASE = 7000;
        const int MOD_CTRL = 0x2;
        const int MOD_ALT = 0x1;
        const int WM_HOTKEY = 0x0312;

        public TrayApp()
        {
            LoadConfig();

            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            Opacity = 0;

            tray = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                Visible = true,
                Text = $"Screen Rotate Helper {VERSION}"
            };

            var menu = new ContextMenuStrip();
            menu.Items.Add("设置", null, OpenSettings);
            menu.Items.Add($"关于 {VERSION}", null, ShowAbout);
            menu.Items.Add("退出", null, (_, __) => Application.Exit());
            tray.ContextMenuStrip = menu;

            RegisterHotKeys();

            if (cfg.AutoStart)
                SetAutoStart(true);
        }

        protected override void SetVisibleCore(bool value)
        {
            base.SetVisibleCore(false);
        }

        void RegisterHotKeys()
        {
            RegisterHotKey(Handle, HOTKEY_BASE + 0, MOD_CTRL | MOD_ALT, (int)Keys.Up);
            RegisterHotKey(Handle, HOTKEY_BASE + 1, MOD_CTRL | MOD_ALT, (int)Keys.Right);
            RegisterHotKey(Handle, HOTKEY_BASE + 2, MOD_CTRL | MOD_ALT, (int)Keys.Down);
            RegisterHotKey(Handle, HOTKEY_BASE + 3, MOD_CTRL | MOD_ALT, (int)Keys.Left);
            RegisterHotKey(Handle, HOTKEY_BASE + 4, MOD_CTRL | MOD_ALT, (int)Keys.D0);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                if (!cfg.DevMode && !IsTargetRunning())
                    return;

                int id = m.WParam.ToInt32() - HOTKEY_BASE;
                int screen = cfg.ScreenIndex;

                switch (id)
                {
                    case 0: Rotate(screen, 0); break;
                    case 1: Rotate(screen, 90); break;
                    case 2: Rotate(screen, 180); break;
                    case 3: Rotate(screen, 270); break;
                    case 4: RotateWithCountdown(screen); break;
                }
            }
            base.WndProc(ref m);
        }

        void Rotate(int screen, int angle)
        {
            ScreenRotate.Set(screen, angle);
            ShowAnglePopup(angle);
        }

        async void RotateWithCountdown(int screen)
        {
            ScreenRotate.Set(screen, 90);
            ShowAnglePopup(90);

            for (int i = 30; i >= 1; i--)
                await Task.Delay(1000);

            ScreenRotate.Set(screen, 0);
            ShowAnglePopup(0);
        }

        bool IsTargetRunning()
        {
            if (string.IsNullOrEmpty(cfg.WatchExe)) return false;
            return Process.GetProcessesByName(
                cfg.WatchExe.Replace(".exe", "")
            ).Any();
        }

        void ShowAnglePopup(int angle)
        {
            Form f = new Form
            {
                Width = 180,
                Height = 90,
                FormBorderStyle = FormBorderStyle.FixedToolWindow,
                StartPosition = FormStartPosition.CenterScreen,
                TopMost = true,
                Text = "屏幕旋转"
            };

            Label l = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter,
                Font = new System.Drawing.Font("Segoe UI", 16),
                Text = $"当前角度\n{angle}°"
            };

            f.Controls.Add(l);
            f.Show();

            Task.Delay(5000).ContinueWith(_ =>
            {
                try { f.Invoke(f.Close); } catch { }
            });
        }

        void OpenSettings(object sender, EventArgs e)
        {
            var f = new Form
            {
                Text = "设置",
                Width = 360,
                Height = 320,
                StartPosition = FormStartPosition.CenterScreen
            };

            var exeBox = new ComboBox
            {
                Left = 20,
                Top = 20,
                Width = 300,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            exeBox.Items.Add("none");
            foreach (var p in Process.GetProcesses().OrderBy(p => p.ProcessName))
            {
                try { exeBox.Items.Add(p.ProcessName + ".exe"); } catch { }
            }
            exeBox.SelectedItem = cfg.WatchExe ?? "none";

            var screenBox = new ComboBox
            {
                Left = 20,
                Top = 60,
                Width = 300,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            for (int i = 0; i < Screen.AllScreens.Length; i++)
                screenBox.Items.Add($"屏幕 {i + 1}");

            screenBox.SelectedIndex =
                cfg.ScreenIndex < Screen.AllScreens.Length
                ? cfg.ScreenIndex : 0;

            var dev = new CheckBox
            {
                Left = 20,
                Top = 100,
                Width = 300,
                Text = "Dev Mode（忽略程序检测）",
                Checked = cfg.DevMode
            };

            var auto = new CheckBox
            {
                Left = 20,
                Top = 130,
                Width = 300,
                Text = "开机自动启动",
                Checked = cfg.AutoStart
            };

            var save = new Button
            {
                Left = 20,
                Top = 180,
                Width = 80,
                Text = "保存"
            };

            save.Click += (_, __) =>
            {
                cfg.WatchExe = exeBox.SelectedItem?.ToString();
                if (cfg.WatchExe == "none") cfg.WatchExe = null;

                cfg.ScreenIndex = screenBox.SelectedIndex;
                cfg.DevMode = dev.Checked;
                cfg.AutoStart = auto.Checked;

                SaveConfig();
                SetAutoStart(cfg.AutoStart);

                f.Close();
            };

            f.Controls.AddRange(new Control[]
            {
                exeBox, screenBox, dev, auto, save
            });

            f.ShowDialog();
        }

        void ShowAbout(object sender, EventArgs e)
        {
            MessageBox.Show(
                $"Screen Rotate Helper\n{VERSION}\n\nAuthor: {AUTHOR}",
                "关于",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        void SaveConfig()
        {
            File.WriteAllText(ConfigPath,
                JsonSerializer.Serialize(cfg));
        }

        void LoadConfig()
        {
            if (File.Exists(ConfigPath))
            {
                try
                {
                    cfg = JsonSerializer.Deserialize<AppConfig>(
                        File.ReadAllText(ConfigPath));
                }
                catch { cfg = new AppConfig(); }
            }
        }

        void SetAutoStart(bool enable)
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true);

            if (enable)
                key.SetValue("ScreenRotateHelper", Application.ExecutablePath);
            else
                key.DeleteValue("ScreenRotateHelper", false);
        }

        [DllImport("user32.dll")]
        static extern bool RegisterHotKey(
            IntPtr hWnd, int id, int fsModifiers, int vk);
    }

    static class ScreenRotate
    {
        const int ENUM_CURRENT_SETTINGS = -1;
        const int CDS_UPDATEREGISTRY = 0x01;

        [DllImport("user32.dll")]
        static extern int ChangeDisplaySettingsEx(
            string deviceName, ref DEVMODE dm,
            IntPtr hwnd, int flags, IntPtr lParam);

        [DllImport("user32.dll")]
        static extern bool EnumDisplaySettings(
            string deviceName, int mode, ref DEVMODE dm);

        public static void Set(int screenIndex, int angle)
        {
            var screen = Screen.AllScreens[screenIndex];
            DEVMODE dm = new DEVMODE();
            dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));

            if (!EnumDisplaySettings(screen.DeviceName, ENUM_CURRENT_SETTINGS, ref dm))
                return;

            int current = dm.dmDisplayOrientation * 90;
            bool swap =
                (current == 0 || current == 180) && (angle == 90 || angle == 270) ||
                (current == 90 || current == 270) && (angle == 0 || angle == 180);

            if (swap)
            {
                int t = dm.dmPelsWidth;
                dm.dmPelsWidth = dm.dmPelsHeight;
                dm.dmPelsHeight = t;
            }

            dm.dmDisplayOrientation = angle / 90;
            ChangeDisplaySettingsEx(screen.DeviceName, ref dm, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields, dmPositionX, dmPositionY;
            public int dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight;
            public int dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType;
            public int dmReserved1, dmReserved2;
            public int dmPanningWidth, dmPanningHeight;
        }
    }
}
