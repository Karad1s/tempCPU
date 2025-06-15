// TemperatureMonitor.cs
using Forms = System.Windows.Forms;
using wpfApp = System.Windows.Application;

namespace tempCPU
{
    public class TemperatureMonitor
    {
        
        private readonly TrayManager _trayManager;
        private readonly Forms.Timer _timer;

        public TemperatureMonitor(TrayManager trayManager)
        {
            _trayManager = trayManager;
            _timer = new Forms.Timer { Interval = 5000 };
            _timer.Tick += (_, __) => CheckOverheat();
        }

        public void Start() => _timer.Start();

        private void CheckOverheat()
        {
            float temp = ((App)wpfApp.Current).HotKeyManager.GetCpuTemperature();
            if (temp >= 90)
            {
                Logger.Warning($"Обнаружен перегрев: {temp:F1}°C");
                _trayManager.ShowCpuTemperature();
            }
        }
    }
}
