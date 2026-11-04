using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using MainCore.Services;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace WPFUI
{
    public sealed class AdminWebHost(IServiceProvider services) : IHostedService
    {
        private WebApplication _app;
        private int _exit;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            for (var port = 17891; port < 17910; port++)
            {
                try
                {
                    _app = Build(wwwroot, port);
                    await _app.StartAsync(cancellationToken);
                    AdminPage.Url = $"http://127.0.0.1:{port}";
                    break;
                }
                catch (IOException)
                {
                    _app = null;
                }
            }

            if (string.IsNullOrWhiteSpace(AdminPage.Url))
            {
                ShowDesktop("No pude abrir la web de administración.");
                return;
            }

            try
            {
                await AdminBrowser.Open(AdminPage.Url, RequestExit);
            }
            catch (Exception ex)
            {
                ShowDesktop("No pude abrir Chrome. " + ex.Message);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            Interlocked.Exchange(ref _exit, 1);
            await AdminBrowser.Close();
            if (_app != null) await _app.StopAsync(cancellationToken);
        }

        private WebApplication Build(string wwwroot, int port)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = [],
                ContentRootPath = AppContext.BaseDirectory,
            });
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
            builder.WebHost.UseUrls($"http://127.0.0.1:{port}");

            var app = builder.Build();
            if (Directory.Exists(wwwroot))
            {
                var files = new PhysicalFileProvider(wwwroot);
                app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
                app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
            }

            AdminApi.Map(app, services);
            app.MapFallback(async context =>
            {
                var index = Path.Combine(wwwroot, "index.html");
                if (!File.Exists(index))
                {
                    context.Response.StatusCode = 503;
                    await context.Response.WriteAsync("La interfaz todavía no está compilada.");
                    return;
                }

                context.Response.ContentType = "text/html";
                await context.Response.SendFileAsync(index);
            });
            return app;
        }

        private void RequestExit()
        {
            if (Interlocked.Exchange(ref _exit, 1) == 1) return;
            Application.Current?.Dispatcher.BeginInvoke(() => Application.Current.Shutdown());
        }

        private static void ShowDesktop(string message)
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                foreach (Window window in Application.Current.Windows)
                {
                    window.Opacity = 1;
                    window.ShowInTaskbar = true;
                    window.ShowActivated = true;
                    window.Left = 80;
                    window.Top = 80;
                    window.Width = 1100;
                    window.Height = 720;
                    window.WindowState = WindowState.Normal;
                }

                MessageBox.Show(message, "Travian Guard");
            });
        }
    }
}
