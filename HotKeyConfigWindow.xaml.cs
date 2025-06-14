using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using MessageBox = System.Windows.MessageBox;

namespace tempCPU
{
    public partial class HotKeyConfigWindow : Window
    {
        private readonly Action<uint> _onKeySelected;
        private Key? _selectedKey;

        public HotKeyConfigWindow(Action<uint> onKeySelected)
        {
            InitializeComponent();
            _onKeySelected = onKeySelected;
        }

        private void KeyBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            e.Handled = true;
            _selectedKey = e.Key;
            KeyBox.Text = _selectedKey.ToString();
        }

            

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedKey.HasValue)
            {
                uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(_selectedKey.Value);
                _onKeySelected(virtualKey);
                this.Close();
            }
            else
            {
                System.Windows.MessageBox.Show(
                    "Сначала выберите клавишу!",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }

    }
}
