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
            float CPUtemp = ((App)wpfApp.Current).HotKeyManager.GetCpuTemperature();
            float GPUTemp = ((App)wpfApp.Current).HotKeyManager.GetCpuTemperature();
            if (CPUtemp >= 90)
            {
                Logger.Warning($"Обнаружен перегрев: {CPUtemp:F1}°C");
                _trayManager.ShowCpuAndGpuTemperature();
            }

            if (GPUTemp >= 85)
            {
                Logger.Warning($"Перегрев GPU: {GPUTemp:F1}°C");
                _trayManager.ShowCpuAndGpuTemperature();
            }
        }
    }
}
