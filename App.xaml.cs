using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using WpfApp = System.Windows.Application;

namespace tempCPU
{
    public partial class App : WpfApp
    {
        private const int HOTKEY_ID = 9000;
        private const uint MOD_NOREPEAT = 0x4000;
        private const uint MOD_NONE = 0x0000;

        private Forms.NotifyIcon _trayIcon = null!;
        private LibreHardwareMonitor.Hardware.Computer _computer = null!;
        private Window _invisibleWindow = null!;
        private uint _hotKey;
        private bool _hotKeyPressed = false;
        private WindowInteropHelper _helper = null!;
        private HwndSource _source = null!;
        private bool _hotKeyRegistered = false;


        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        protected override void OnStartup(StartupEventArgs e)
        {
            Logger.Info("=== Запуск приложения ===");

            // Проверка прав администратора
            if (!IsRunAsAdmin())
            {
                Logger.Warning("Нет прав администратора, перезапускаю с повышенными правами...");
                RelaunchAsAdmin();
                Shutdown();
                return;
            }

            base.OnStartup(e);

            // Загружаем HotKey из реестра или дефолт
            _hotKey = LoadHotKeyFromRegistry();

            // Инициализация датчиков
            _computer = new LibreHardwareMonitor.Hardware.Computer { IsCpuEnabled = true };
            _computer.Open();

            // Инициализация трея
            InitializeTrayIcon();

            // Создаем невидимое окно для глобального хоткея
            InitializeInvisibleWindow();

            // Запускаем мониторинг температуры
            StartTemperatureMonitor();

            // Гарантируем автозапуск
            AddToStartup();

            Logger.Info("=== Приложение полностью готово ===");
        }

        #region Tray & HotKey

        /// <summary>
        /// Инициализация иконки в трее и контекстного меню
        /// </summary>
        private void InitializeTrayIcon()
        {
            _trayIcon = new Forms.NotifyIcon
            {
                Icon = LoadAppIconSafe(),
                Visible = true,
                Text = "CPU Temp"
            };

            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("Показать температуру", null, (_, __) => ShowCpuTemperature());
            menu.Items.Add("Настроить клавишу", null, (_, __) => OpenHotKeyConfig());
            menu.Items.Add("Выход", null, OnExitClicked);

            _trayIcon.ContextMenuStrip = menu;
        }

        /// <summary>
        /// Создает скрытое окно и регистрирует глобальный HotKey
        /// </summary>
        private void InitializeInvisibleWindow()
        {
            _invisibleWindow = new Window
            {
                Width = 0,
                Height = 0,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
                Visibility = Visibility.Hidden
            };

            _invisibleWindow.SourceInitialized += (_, __) =>
            {
                _helper = new WindowInteropHelper(_invisibleWindow);
                _source = HwndSource.FromHwnd(_helper.Handle);
                _source.AddHook(HwndHook);

                RegisterGlobalHotKey(_hotKey);
            };

            _invisibleWindow.Show();
            _invisibleWindow.Hide();
        }

        private void RegisterGlobalHotKey(uint vk)
        {
            if (_hotKeyRegistered)
            {
                UnregisterHotKey(_helper.Handle, HOTKEY_ID);
                Logger.Info($"HotKey VK = {_hotKey} отписан перед повторной регистрацией");
                _hotKeyRegistered = false;
            }

            if (RegisterHotKey(_helper.Handle, HOTKEY_ID, MOD_NOREPEAT | MOD_NONE, vk))
            {
                _hotKey = vk;
                _hotKeyRegistered = true;
                Logger.Info($"HotKey VK = {_hotKey} успешно зарегистрирован");
            }
            else
            {
                Logger.Error($"Не удалось зарегистрировать HotKey VK = {vk}");
            }
        }


        /// <summary>
        /// Перехватчик сообщений окна для обработки HotKey
        /// </summary>
        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                if (_hotKeyPressed)
                {
                    Logger.Info("Дубликат HotKey проигнорирован");
                    return IntPtr.Zero;
                }

                _hotKeyPressed = true;
                Logger.Info("HotKey нажат — показываю температуру CPU");
                ShowCpuTemperature();
                handled = true;

                Task.Delay(200).ContinueWith(_ => _hotKeyPressed = false);
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// Открывает окно конфигурации HotKey
        /// </summary>
        private void OpenHotKeyConfig()
        {
            Logger.Info("Открываю окно настройки горячей клавиши...");
            var configWindow = new HotKeyConfigWindow(UpdateHotKey);
            configWindow.Owner = Current.MainWindow;
            configWindow.ShowDialog();
        }

        /// <summary>
        /// Обновляет HotKey и сохраняет его в реестр
        /// </summary>
        private void UpdateHotKey(uint newVk)
        {
            RegisterGlobalHotKey(newVk);
            SaveHotKeyToRegistry(newVk);
        }


        #endregion

        #region Utils

        /// <summary>
        /// Безопасная загрузка иконки приложения
        /// </summary>
        private Icon LoadAppIconSafe()
        {
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "Icon.ico");
                if (File.Exists(iconPath))
                {
                    return new Icon(iconPath);
                }
                Logger.Warning("Icon.ico не найден, использую стандартную иконку");
            }
            catch (Exception ex)
            {
                Logger.Error($"Ошибка загрузки иконки: {ex.Message}");
            }

            return SystemIcons.Information;
        }

        /// <summary>
        /// Проверка запуска от имени администратора
        /// </summary>
        private bool IsRunAsAdmin()
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        /// <summary>
        /// Перезапуск приложения с правами администратора
        /// </summary>
        private void RelaunchAsAdmin()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                Process.Start(new ProcessStartInfo(exePath) { Verb = "runas" });
                Logger.Info("Перезапущено с правами администратора");
            }
            catch (Exception ex)
            {
                Logger.Error($"Не удалось перезапустить: {ex.Message}");
                _trayIcon?.ShowBalloonTip(5000, "Ошибка", "Не удалось запустить с правами администратора.", Forms.ToolTipIcon.Error);
            }
        }

        /// <summary>
        /// Добавление в автозагрузку
        /// </summary>
        private void AddToStartup()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                using var rk = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
                rk?.SetValue("CpuTempTrayWpf", exePath);
                Logger.Info("Добавлен в автозагрузку");
            }
            catch (Exception ex)
            {
                Logger.Error($"Ошибка автозагрузки: {ex.Message}");
                _trayIcon?.ShowBalloonTip(5000, "Ошибка", "Не удалось добавить в автозагрузку.", Forms.ToolTipIcon.Error);
            }
        }

        /// <summary>
        /// Сохранение HotKey в реестр
        /// </summary>
        private void SaveHotKeyToRegistry(uint vk)
        {
            try
            {
                using var rk = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\CpuTempTrayWpf");
                rk?.SetValue("HotKey", vk, RegistryValueKind.DWord);
                _hotKey = vk;
                Logger.Info($"HotKey VK = {vk} сохранен в реестр");
            }
            catch (Exception ex)
            {
                Logger.Error($"Ошибка сохранения HotKey: {ex.Message}");
            }
        }

        /// <summary>
        /// Загрузка HotKey из реестра
        /// </summary>
        private uint LoadHotKeyFromRegistry()
        {
            try
            {
                using var rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\CpuTempTrayWpf");
                var value = rk?.GetValue("HotKey");
                if (value != null && uint.TryParse(value.ToString(), out uint vk))
                {
                    return vk;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Ошибка загрузки HotKey: {ex.Message}");
            }

            Logger.Info("HotKey не найден, используется дефолт VK_ADD (0x6B)");
            return 0x6B; // VK_ADD
        }

        /// <summary>
        /// Показывает температуру ЦП
        /// </summary>
        private void ShowCpuTemperature()
        {
            float temp = GetCpuTemperature();
            Logger.Info($"Текущая температура ЦП: {temp:F1} °C");
            _trayIcon.ShowBalloonTip(3000, "Температура CPU", $"{temp:F1} °C", Forms.ToolTipIcon.Info);
        }

        /// <summary>
        /// Получает среднюю температуру ЦП
        /// </summary>
        private float GetCpuTemperature()
        {
            float sum = 0;
            int count = 0;

            foreach (var hardware in _computer.Hardware)
            {
                if (hardware.HardwareType == LibreHardwareMonitor.Hardware.HardwareType.Cpu)
                {
                    hardware.Update();
                    foreach (var sensor in hardware.Sensors)
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

        /// <summary>
        /// Запускает таймер для мониторинга перегрева
        /// </summary>
        private void StartTemperatureMonitor()
        {
            var timer = new Forms.Timer { Interval = 5000 };
            timer.Tick += (_, __) =>
            {
                double temp = GetCpuTemperature();
                if (temp >= 90)
                {
                    _trayIcon.ShowBalloonTip(5000, "Внимание", $"ЦП перегрелся: {temp:F1}°C", Forms.ToolTipIcon.Warning);
                    Logger.Warning($"Обнаружен перегрев: {temp:F1}°C");
                }
            };
            timer.Start();
        }

        #endregion

        #region App Lifecycle

        private void OnExitClicked(object? sender, EventArgs e)
        {
            Logger.Info("Выход по команде из трея");
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_hotKeyRegistered)
            {
                UnregisterHotKey(_helper.Handle, HOTKEY_ID);
                Logger.Info("HotKey успешно отписан при выходе");
            }

            _computer.Close();
            _trayIcon.Visible = false;

            Logger.Info("=== Завершение приложения ===");
            base.OnExit(e);
        }

        #endregion
    }
}
