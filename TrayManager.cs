using System;
using System.Drawing;
using System.IO;
using Forms = System.Windows.Forms;

namespace tempCPU
{
    public class TrayManager : IDisposable
    {
        private readonly HotKeyManager _hotKeyManager;
        private Forms.NotifyIcon _trayIcon = null!;
        private Forms.ToolStripMenuItem _showTempMenuItem = null!;
        private readonly Forms.ToolStripMenuItem _showTemperatureItem;

        public TrayManager(HotKeyManager hotKeyManager)
        {
            _hotKeyManager = hotKeyManager;

            _showTemperatureItem = new Forms.ToolStripMenuItem();
            UpdateShowTemperatureItemText();

            _showTemperatureItem.Click += (s, e) => ShowCpuAndGpuTemperature();
        }



        public void Initialize()
        {
            _trayIcon = new Forms.NotifyIcon
            {
                Icon = Utils.LoadAppIconSafe(),
                Visible = true,
                Text = "CPU Temp"
            };

            var menu = new Forms.ContextMenuStrip();

            _showTempMenuItem = new Forms.ToolStripMenuItem($"Показать температуру ({_hotKeyManager.GetHotKeyName()})");
            _showTempMenuItem.Click += (_, __) => ShowCpuAndGpuTemperature();

            menu.Items.Add(_showTempMenuItem);
            menu.Items.Add("Настроить клавишу", null, (_, __) => _hotKeyManager.OpenHotKeyConfig());
            menu.Items.Add("Выход", null, (_, __) => System.Windows.Application.Current.Shutdown());

            // _trayIcon.ContextMenuStrip = menu;
            _trayIcon.MouseClick += TrayIcon_MouseClick;
        }

        public void UpdateShowTemperatureItemText()
        {
            _showTemperatureItem.Text = $"Показать температуру CPU/GPU ({_hotKeyManager.GetHotKeyName()})";
        }


        // public void ShowCpuTemperature()
        // {
        //     float temp = _hotKeyManager.GetCpuTemperature();
        //     Logger.Info($"Текущая температура ЦП: {temp:F1} °C");
        //     _trayIcon.ShowBalloonTip(3000, "Температура CPU", $"{temp:F1} °C", Forms.ToolTipIcon.Info);
        // }

        // public void ShowGpuTemperature()
        // {
        //     float gpuTemp = _hotKeyManager.GetGpuTemperature();
        //     Logger.Info($"Текущая температура GPU: {gpuTemp:F1} °C");
        //     _trayIcon.ShowBalloonTip(3000, "Температура GPU", $"{gpuTemp:F1} °C", Forms.ToolTipIcon.Info);
        // }

        public void ShowCpuAndGpuTemperature()
        {
            float cpuTemp = _hotKeyManager.GetCpuTemperature();
            float gpuTemp = _hotKeyManager.GetGpuTemperature();

            string message = $"CPU: {cpuTemp:F1} °C | GPU: {gpuTemp:F1} °C";
            Logger.Info($"Температуры — {message}");

            ShowCustomNotification($"CPU: {cpuTemp:F1} °C | GPU: {gpuTemp:F1} °C");

        }

        public void OnHotKeyChanged()
        {
            UpdateShowTemperatureItemText();
        }

        public void Dispose()
        {
            _trayIcon.Visible = false;
        }

        private void ShowCustomNotification(string message)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var window = new NotificationWindow(message);
                window.Show();
            });
        }


        private void TrayIcon_MouseClick(object? sender, Forms.MouseEventArgs e)
        {
            if (e.Button == Forms.MouseButtons.Right)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    var menuWindow = new CustomTrayMenuWindow();
                    menuWindow.Show();
                });
            }
        }
    }
}
