using System;
using System.Windows;

namespace WPFUI
{
    public static class UpdateSplash
    {
        private static Views.UpdateSplashWindow _window;

        public static void Show(string version)
        {
            var app = Application.Current;
            if (app == null) return;
            if (app.Dispatcher.CheckAccess()) ShowCore(version);
            else app.Dispatcher.Invoke(() => ShowCore(version));
        }

        public static void Report(string status, int percent, string latest)
        {
            var app = Application.Current;
            if (app == null) return;
            app.Dispatcher.BeginInvoke(() =>
            {
                if (_window == null) ShowCore(AppUpdate.CurrentVersion());
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

        private static void ShowCore(string version)
        {
            if (_window != null)
            {
                _window.Activate();
                return;
            }

            _window = new Views.UpdateSplashWindow();
            _window.Apply(version, "Comprobando la versión...", -1, "");
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
