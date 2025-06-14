using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;
using WpfApp = System.Windows.Application;
using Microsoft.Win32;

namespace tempCPU
{
    public partial class App : WpfApp
    {
        private Forms.NotifyIcon _trayIcon = null!;
        private LibreHardwareMonitor.Hardware.Computer _computer = null!;
        private Window? _invisibleWindow;

        private const int HOTKEY_ID = 9000;
        private const uint MOD_NOREPEAT = 0x4000;
        private const uint MOD_NONE = 0x0000;
        private uint _hotKey;


        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        protected override void OnStartup(StartupEventArgs e)
        {
            Logger.Info("Приложение запускается...");

            // Проверка прав администратора
            if (!IsRunAsAdmin())
            {
                Logger.Info("Нет прав администратора. Перезапускаюсь с правами администратора...");
                RelaunchAsAdmin();
                Shutdown();
                return;
            }

            _hotKey = LoadHotKeyFromRegistry();
            Logger.Info($"Хоткей при старте: VK = {_hotKey}");

            base.OnStartup(e);

            Logger.Info("Запуск LibreHardwareMonitor...");
            _computer = new LibreHardwareMonitor.Hardware.Computer { IsCpuEnabled = true };
            _computer.Open();

            Logger.Info("Инициализация трея...");
            _trayIcon = new Forms.NotifyIcon
            {
                Icon = SystemIcons.Information, // безопасная иконка
                Visible = true,
                Text = "CPU Temp"
            };

            // Безопасная загрузка иконки
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "Icon.ico");
                Logger.Info($"Загрузка иконки: {iconPath}");
                if (File.Exists(iconPath))
                {
                    _trayIcon.Icon = new Icon(iconPath);
                    Logger.Info("Иконка успешно загружена.");
                }
                else
                {
                    Logger.Warning("Иконка не найдена! Использую SystemIcons.Information");
                    _trayIcon.Icon = SystemIcons.Information;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Ошибка при загрузке иконки: {ex.Message}");
                _trayIcon.Icon = SystemIcons.Information;
            }

            // Контекстное меню
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Показать температуру", null, (s, _) => ShowCpuTemperature());
            menu.Items.Add("Настройка клавиши", null, (s, _) => OpenHotKeyConfig());
            menu.Items.Add("Выход", null, OnExitClicked);
            _trayIcon.ContextMenuStrip = menu;

            _trayIcon.Visible = true;
            Logger.Info("Трей-иконка готова и видима.");

            // Невидимое окно для HotKey
            _invisibleWindow = new Window
            {
                Width = 0,
                Height = 0,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Visibility = Visibility.Hidden
            };
            _invisibleWindow.SourceInitialized += (s, args) =>
            {
                var helper = new WindowInteropHelper(_invisibleWindow);
                Logger.Info($"Регистрация HotKey VK={_hotKey}");
                RegisterHotKey(helper.Handle, HOTKEY_ID, MOD_NOREPEAT | MOD_NONE, _hotKey);
                HwndSource source = HwndSource.FromHwnd(helper.Handle);
                source.AddHook(HwndHook);
            };
            _invisibleWindow.Show();
            _invisibleWindow.Hide();
            

            Logger.Info("Настройка таймера мониторинга температуры...");
            var timer = new Forms.Timer();
            timer.Interval = 5000; // каждые 5 сек
            timer.Tick += (s3, _) =>
            {
                double temp = GetCpuTemperature();
                if (temp >= 90)
                {
                    _trayIcon.ShowBalloonTip(5000, "Внимание", $"ЦП нагрелся: {temp:F1}°C", Forms.ToolTipIcon.Warning);
                    Logger.Warning($"Обнаружен перегрев: {temp:F1}°C");
                }
            };
            timer.Start();

            Logger.Info("Добавление автозагрузки в реестр...");
            AddToStartup();

            Logger.Info("Приложение полностью запущено.");
        }

        private void OpenHotKeyConfig()
        {
            Logger.Info("Открытие окна настройки горячей клавиши...");

            var configWindow = new HotKeyConfigWindow(UpdateHotKey);

            configWindow.Owner = Current.MainWindow; // если есть главное окно
            configWindow.ShowDialog();
        }

        private void UpdateHotKey(uint newKey)
        {
            Logger.Info($"Обновление горячей клавиши: VK = {newKey}");

            if (_invisibleWindow != null)
            {
                var helper = new WindowInteropHelper(_invisibleWindow);

                // Разрегистрируем старый
                UnregisterHotKey(helper.Handle, HOTKEY_ID);

                // Регистрируем новый
                RegisterHotKey(helper.Handle, HOTKEY_ID, MOD_NOREPEAT | MOD_NONE, newKey);

                // Сохраняем в реестр
                SaveHotKeyToRegistry(newKey);
            }
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
            if (string.IsNullOrWhiteSpace(exeName)) return;

            try
            {
                Process.Start(new ProcessStartInfo(exeName)
                {
                    Verb = "runas"
                });
                Logger.Info("Перезапуск с правами администратора запущен.");
            }
            catch (Exception ex)
            {
                Logger.Error("Не удалось перезапустить программу в качестве администратора: " + ex.Message);
                _trayIcon.ShowBalloonTip(5000, "Ошибка", "Не удалось запустить с правами администратора.", Forms.ToolTipIcon.Error);
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
                    Logger.Info("Глобальный HotKey сработал!");
                    ShowCpuTemperature();
                    handled = true;
                }
            }

            return IntPtr.Zero;
        }


        private void ShowCpuTemperature()
        {
            float temp = GetCpuTemperature();
            Logger.Info($"Текущая температура ЦП: {temp:F1} °C");
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

        private void ShowHotKeyConfigWindow()
        {
            Logger.Info("Открываю окно настройки горячей клавиши...");
            var hotkeyWindow = new HotKeyConfigWindow(UpdateHotKey); // ⬅️ Только этот метод
            hotkeyWindow.Show();
            Logger.Info("Окно настройки горячей клавиши открыто.");
        }

        

        private void OnExitClicked(object? sender, EventArgs e)
        {
            Logger.Info("Выход по команде из трея...");
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_invisibleWindow is not null)
            {
                var helper = new WindowInteropHelper(_invisibleWindow);
                UnregisterHotKey(helper.Handle, HOTKEY_ID);
                Logger.Info("HotKey успешно отписан.");
            }

            _computer.Close();
            _trayIcon.Visible = false;

            Logger.Info("Приложение завершено.");
            base.OnExit(e);
        }

        private void SaveHotKeyToRegistry(uint vk)
        {
            try
            {
                using var rk = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\CpuTempTrayWpf");
                rk?.SetValue("HotKey", vk, RegistryValueKind.DWord);
                _hotKey = vk; // 🗝️ обновляем локально тоже!
                Logger.Info($"Горячая клавиша сохранена и обновлена локально: VK = {vk}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Не удалось сохранить хоткей в реестр: {ex.Message}");
            }
        }

        private uint LoadHotKeyFromRegistry()
        {
            try
            {
                using var rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\CpuTempTrayWpf");
                object? value = rk?.GetValue("HotKey");
                if (value != null && uint.TryParse(value.ToString(), out uint vk))
                {
                    Logger.Info($"Загружен хоткей из реестра: VK = {vk}");
                    return vk;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Не удалось загрузить хоткей из реестра: {ex.Message}");
            }

            Logger.Info("В реестре не найден хоткей, используется дефолт VK_ADD (0x6B).");
            return 0x6B; // дефолт VK_ADD
        }


        private void AddToStartup()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                RegistryKey rk = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
                rk.SetValue("CpuTempTrayWpf", exePath);
                Logger.Info("Добавлен в автозагрузку.");
            }
            catch (Exception ex)
            {
                Logger.Error($"Не удалось добавить в автозагрузку: {ex.Message}");
                _trayIcon.ShowBalloonTip(5000, "Ошибка", "Не удалось добавить в автозагрузку.", Forms.ToolTipIcon.Error);
            }
        }
    }
}
