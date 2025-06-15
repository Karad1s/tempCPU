// HotKeyManager.cs
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using WpfApp = System.Windows.Application;
using Forms = System.Windows.Forms;

namespace tempCPU
{
    public class HotKeyManager : IDisposable
    {
        private const int HOTKEY_ID = 9000;
        private const uint MOD_NOREPEAT = 0x4000;
        private const uint MOD_NONE = 0x0000;

        private uint _hotKey;
        private bool _hotKeyPressed = false;
        private Window _invisibleWindow = null!;
        private WindowInteropHelper _helper = null!;
        private HwndSource _source = null!;
        private bool _hotKeyRegistered = false;

        private readonly LibreHardwareMonitor.Hardware.Computer _computer;

        // private readonly Action _onHotKeyChanged;

        private Action _onHotKeyChanged;

        public HotKeyManager(Action onHotKeyChanged)
        {
            _onHotKeyChanged = onHotKeyChanged;
            _computer = new LibreHardwareMonitor.Hardware.Computer { IsCpuEnabled = true };
            _computer.Open();
        }

        public void SetOnHotKeyChanged(Action onHotKeyChanged)
        {
            _onHotKeyChanged = onHotKeyChanged;
        }

        public void Initialize()
        {
            _hotKey = Utils.LoadHotKeyFromRegistry();
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

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
            {
                if (_hotKeyPressed) return IntPtr.Zero;
                _hotKeyPressed = true;
                Logger.Info("HotKey нажат");
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    ((App)WpfApp.Current).TrayManager.ShowCpuTemperature();
;
                });
                handled = true;
                Task.Delay(200).ContinueWith(_ => _hotKeyPressed = false);
            }
            return IntPtr.Zero;
        }

        public void OpenHotKeyConfig()
        {
            var configWindow = new HotKeyConfigWindow(UpdateHotKey);
            configWindow.Owner = WpfApp.Current.MainWindow;
            configWindow.ShowDialog();
        }

        private void RegisterGlobalHotKey(uint vk)
        {
            if (_hotKeyRegistered)
                NativeMethods.UnregisterHotKey(_helper.Handle, HOTKEY_ID);

            if (NativeMethods.RegisterHotKey(_helper.Handle, HOTKEY_ID, MOD_NOREPEAT | MOD_NONE, vk))
            {
                _hotKey = vk;
                _hotKeyRegistered = true;
            }
        }

        private void UpdateHotKey(uint newVk)
        {
            RegisterGlobalHotKey(newVk);
            Utils.SaveHotKeyToRegistry(newVk);
            _onHotKeyChanged?.Invoke();
        }

        public string GetHotKeyName() => ((Forms.Keys)_hotKey).ToString();

        public float GetCpuTemperature()
        {
            float sum = 0;
            int count = 0;
            foreach (var hw in _computer.Hardware)
            {
                if (hw.HardwareType == LibreHardwareMonitor.Hardware.HardwareType.Cpu)
                {
                    hw.Update();
                    foreach (var sensor in hw.Sensors)
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

        public void Dispose()
        {
            if (_hotKeyRegistered)
                NativeMethods.UnregisterHotKey(_helper.Handle, HOTKEY_ID);
            _computer.Close();
        }
    }
}
