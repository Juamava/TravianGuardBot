using System.ComponentModel;
using System.Windows;

namespace WPFUI.Views
{
    public partial class UpdateSplashWindow : Window
    {
        private bool _allowClose;

        public UpdateSplashWindow()
        {
            InitializeComponent();
        }

        public void Apply(string version, string status, int percent, string latest)
        {
            VersionText.Text = "Versión actual " + version;
            if (latest != null)
            {
                LatestText.Text = latest.Length == 0 ? "" : "Nueva versión " + latest;
                LatestText.Visibility = latest.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
            }

            if (status != null) StatusText.Text = status;
            if (percent < 0)
            {
                Bar.IsIndeterminate = true;
                return;
            }

            Bar.IsIndeterminate = false;
            Bar.Value = percent;
        }

        public void AllowClose()
        {
            _allowClose = true;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_allowClose) e.Cancel = true;
            base.OnClosing(e);
        }
    }
}
