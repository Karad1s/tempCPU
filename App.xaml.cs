using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;
using WpfApp = System.Windows.Application;
using Microsoft.Win32;
using LibreHardwareMonitor.Hardware;

namespace tempCPU
{
    public partial class App : WpfApp
    {
        private Forms.NotifyIcon _trayIcon = null!;
        private Computer _computer = null!;
        private Window? _invisibleWindow;

        private const int HOTKEY_ID = 9000;
        private const uint MOD_NOREPEAT = 0x4000;
        private const uint MOD_NONE = 0x0000;
        private const uint VK_ADD = 0x6B;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        protected override void OnStartup(StartupEventArgs e)
        {
            // Проверка прав админа
            if (!IsRunAsAdmin())
            {
                RelaunchAsAdmin();
                Shutdown();
                return;
            }

            base.OnStartup(e);

            // Инициализация LibreHardwareMonitor
            _computer = new Computer { IsCpuEnabled = true };
            _computer.Open();

            // Трей-иконка + контекстное меню
            _trayIcon = new Forms.NotifyIcon
            {
                Icon = SystemIcons.Information,
                Visible = true,
                Text = "CPU Temp"
            };
            _trayIcon.ContextMenuStrip = new Forms.ContextMenuStrip();
            _trayIcon.ContextMenuStrip.Items.Add("Показать температуру", null, (s, _) => ShowCpuTemperature());
            _trayIcon.ContextMenuStrip.Items.Add("Выход", null, OnExitClicked);
            _trayIcon.Icon = new System.Drawing.Icon("assets/Icon.ico");

            // Невидимое окно — нужно для HotKey
            var window = new Window
            {
                Width = 0,
                Height = 0,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Visibility = Visibility.Hidden
            };
            window.SourceInitialized += (s, args) =>
            {
                var helper = new WindowInteropHelper(window);
                RegisterHotKey(helper.Handle, HOTKEY_ID, MOD_NOREPEAT | MOD_NONE, VK_ADD);
                HwndSource source = HwndSource.FromHwnd(helper.Handle);
                source.AddHook(HwndHook);
            };

            // Таймер для проверки перегрева
            var timer = new Forms.Timer();
            timer.Interval = 5000; // каждые 5 сек
            timer.Tick += (s, _) =>
            {
                double temp = GetCpuTemperature();
                if (temp >= 90)
                {
                    _trayIcon.ShowBalloonTip(5000, "Внимание", $"ЦП Нагрелся! {temp:F1}°C", Forms.ToolTipIcon.Warning);
                }
            };
            timer.Start();

            window.Hide();
            _invisibleWindow = window;

            // Автозагрузка
            AddToStartup();
        }

        private bool IsRunAsAdmin()
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        private void RelaunchAsAdmin()
        {
            var exeName = Process.GetCurrentProcess().MainModule?.FileName;
            var startInfo = new ProcessStartInfo(exeName!)
            {
                Verb = "runas"
            };
            try
            {
                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                _trayIcon.ShowBalloonTip(5000, "Ошибка", "Не удалось перезапустить программу в качестве администратора: " + ex.Message, Forms.ToolTipIcon.Error);
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (id == HOTKEY_ID)
                {
                    ShowCpuTemperature();
                    handled = true;
                }
            }

            return IntPtr.Zero;
        }

        private void ShowCpuTemperature()
        {
            float temp = GetCpuTemperature();
            _trayIcon.ShowBalloonTip(3000, "Температура CPU", $"{temp:F1} °C", Forms.ToolTipIcon.Info);

        }

        private float GetCpuTemperature()
        {
            float sum = 0;
            int count = 0;

            foreach (var hardwareItem in _computer.Hardware)
            {
                if (hardwareItem.HardwareType == LibreHardwareMonitor.Hardware.HardwareType.Cpu)
                {
                    hardwareItem.Update();
                    foreach (var sensor in hardwareItem.Sensors)
                    {
                        if (sensor.SensorType == LibreHardwareMonitor.Hardware.SensorType.Temperature && sensor.Value.HasValue)
                        {
                            sum += sensor.Value.Value;
                            count++;
                        }
                    }
                }
            }

            return count > 0 ? sum / count : 0;
        }

        private void OnExitClicked(object? sender, EventArgs e)
        {
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_invisibleWindow is not null)
            {
                var helper = new WindowInteropHelper(_invisibleWindow);
                UnregisterHotKey(helper.Handle, HOTKEY_ID);
            }

            _computer.Close();
            _trayIcon.Visible = false;

            base.OnExit(e);
        }

        private void AddToStartup()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                RegistryKey rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true)!;
                rk.SetValue("CpuTempTrayWpf", exePath);
            }
            catch (Exception ex)
            {
                _trayIcon.ShowBalloonTip(5000, "Ошибка", "Не удалось запустить: " + ex.Message, Forms.ToolTipIcon.Error);
            }
        }
    }
}
