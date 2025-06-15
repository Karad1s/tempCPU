using System.Windows;
using wpfApp = System.Windows.Application;
using System.Threading;

namespace tempCPU
{
    public partial class App : wpfApp
    {
        private Mutex? _mutex;
        private const string MutexName = "CPU_TEMP_TRAY_MUTEX";
        private const string EventName = "CPU_TEMP_TRAY_EVENT";
        public HotKeyManager HotKeyManager { get; private set; } = null!;
        public TrayManager TrayManager { get; private set; } = null!;

        private TemperatureMonitor _temperatureMonitor = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            Logger.Info("=== Запуск приложения ===");
            _mutex = new Mutex(true, MutexName, out bool isNewInstance);

            if (!isNewInstance)
            {
                using var waitHandle = EventWaitHandle.OpenExisting(EventName);
                waitHandle.Set();
                Shutdown();
                return;
            }
            _mutex.WaitOne();

            if (!Utils.IsRunAsAdmin())
            {
                Utils.RelaunchAsAdmin();
                Shutdown();
                return;
            }

            base.OnStartup(e);
            
            HotKeyManager = new HotKeyManager(() => { });
            TrayManager = new TrayManager(HotKeyManager);

            HotKeyManager.SetOnHotKeyChanged(TrayManager.UpdateShowTemperatureItemText);

            _temperatureMonitor = new TemperatureMonitor(TrayManager);


            TrayManager.Initialize();
            HotKeyManager.Initialize();
            _temperatureMonitor.Start();

            Utils.AddToStartup();

            Logger.Info("=== Приложение полностью готово ===");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            HotKeyManager.Dispose();
            TrayManager.Dispose();
            Logger.Info("=== Завершение приложения ===");
            base.OnExit(e);
        }
    }
}
