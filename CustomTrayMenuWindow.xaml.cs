using System.Windows;

namespace tempCPU
{
    public partial class CustomTrayMenuWindow : Window
    {
        public CustomTrayMenuWindow()
        {
            InitializeComponent();

            // Располагаем окно рядом с курсором мыши
            var position = System.Windows.Forms.Cursor.Position;
            Left = position.X - Width;
            Top = position.Y - Height;
        }

        private void ShowTemp_Click(object sender, RoutedEventArgs e)
        {
            ((App)System.Windows.Application.Current).TrayManager.ShowCpuAndGpuTemperature();
            Close();
        }

        private void ConfigHotKey_Click(object sender, RoutedEventArgs e)
        {
            ((App)System.Windows.Application.Current).HotKeyManager.OpenHotKeyConfig();
            Close();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            Close(); // Автозакрытие при уводе мыши
        }
    }
}
