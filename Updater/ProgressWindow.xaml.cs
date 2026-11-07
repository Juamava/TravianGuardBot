using System.ComponentModel;
using System.Windows;

namespace Updater
{
    public partial class ProgressWindow : Window
    {
        private bool _allowClose;

        public ProgressWindow()
        {
            InitializeComponent();
        }

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

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_allowClose) e.Cancel = true;
            base.OnClosing(e);
        }

        private void Dispatch(Action action)
        {
            if (Dispatcher.HasShutdownStarted) return;
            if (Dispatcher.CheckAccess()) action();
            else Dispatcher.BeginInvoke(action);
        }
    }
}
