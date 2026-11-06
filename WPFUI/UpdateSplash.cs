using System;
using System.Windows;

namespace WPFUI
{
    public static class UpdateSplash
    {
        private static Views.UpdateSplashWindow _window;

        public static void Show(string version, string status = null, int percent = -1, string latest = "")
        {
            var app = Application.Current;
            if (app == null) return;
            if (app.Dispatcher.CheckAccess()) ShowCore(version, status, percent, latest);
            else app.Dispatcher.Invoke(() => ShowCore(version, status, percent, latest));
        }

        public static void Report(string status, int percent, string latest)
        {
            var app = Application.Current;
            if (app == null) return;
            app.Dispatcher.BeginInvoke(() =>
            {
                if (_window == null) ShowCore(AppUpdate.CurrentVersion(), status, percent, latest);
                _window.Apply(AppUpdate.CurrentVersion(), status, percent, latest);
                _window.Activate();
            });
        }

        public static void Close()
        {
            var app = Application.Current;
            if (app == null) return;
            if (app.Dispatcher.CheckAccess()) CloseCore();
            else app.Dispatcher.Invoke(CloseCore);
        }

        private static void ShowCore(string version, string status, int percent, string latest)
        {
            var text = string.IsNullOrWhiteSpace(status) ? "Comprobando la versión..." : status;
            if (_window != null)
            {
                _window.Apply(version, text, percent, latest ?? "");
                _window.Activate();
                return;
            }

            _window = new Views.UpdateSplashWindow();
            _window.Apply(version, text, percent, latest ?? "");
            _window.Show();
        }

        private static void CloseCore()
        {
            if (_window == null) return;
            _window.AllowClose();
            _window.Close();
            _window = null;
        }
    }
}
