using System.Windows;

namespace Updater
{
    public partial class App : Application
    {
        private async void OnStartup(object sender, StartupEventArgs e)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var window = new ProgressWindow();
            MainWindow = window;
            window.Show();
            try
            {
                var session = UpdateSession.Parse(e.Args);
                if (session == null)
                {
                    window.ShowStatus("Este programa lo abre Travian Guard para instalar una versión.", 0);
                    window.AllowClose();
                    return;
                }

                window.ShowVersion(session.Version);
                await Task.Run(() => session.Execute(window.ShowStatus));
                window.AllowClose();
                Shutdown();
            }
            catch (Exception ex)
            {
                window.ShowStatus(ex.Message, 0);
                window.AllowClose();
            }
        }
    }
}
