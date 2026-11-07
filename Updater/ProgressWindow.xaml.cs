using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Updater
{
    public enum CancelChoice
    {
        Close,
        Started,
        Busy,
    }

    public partial class ProgressWindow : Window
    {
        private bool _allowClose;
        private bool _closeRequested;

        public ProgressWindow()
        {
            InitializeComponent();
        }

        public event Func<CancelChoice>? CancelRequested;

        public void ShowVersion(string version)
        {
            Dispatch(() => VersionText.Text = "Nueva versión " + version);
        }

        public void ShowStatus(string message, int percent)
        {
            Dispatch(() =>
            {
                StatusText.Text = message;
                if (percent < 0)
                {
                    Bar.IsIndeterminate = true;
                    return;
                }

                Bar.IsIndeterminate = false;
                Bar.Value = Math.Clamp(percent, 0, 100);
            });
        }

        public void AllowClose()
        {
            Dispatch(() => _allowClose = true);
        }

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed) return;
            if (IsButton(e.OriginalSource as DependencyObject)) return;
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // The button was released before the drag started.
            }
        }

        private void MinimizeClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (_allowClose)
            {
                base.OnClosing(e);
                return;
            }

            e.Cancel = true;
            if (_closeRequested) return;
            _closeRequested = true;
            switch (CancelRequested?.Invoke() ?? CancelChoice.Close)
            {
                case CancelChoice.Busy:
                    _closeRequested = false;
                    ShowStatus("Ya se está instalando.", 98);
                    return;
                case CancelChoice.Started:
                    Hide();
                    return;
                default:
                    _allowClose = true;
                    Dispatcher.BeginInvoke(Close);
                    return;
            }
        }

        private static bool IsButton(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is System.Windows.Controls.Button) return true;
                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }

        private void Dispatch(Action action)
        {
            if (Dispatcher.HasShutdownStarted) return;
            if (Dispatcher.CheckAccess()) action();
            else Dispatcher.BeginInvoke(action);
        }
    }
}
