using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

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
        private Process _window;
        private int _exit;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            var url = "";
            for (var port = 17891; port < 17910; port++)
            {
                try
                {
                    _app = Build(wwwroot, port);
                    await _app.StartAsync(cancellationToken);
                    url = $"http://127.0.0.1:{port}";
                    break;
                }
                catch (IOException)
                {
                    _app = null;
                }
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                ShowDesktop("No pude abrir la administración.");
                return;
            }

            if (!Debugger.IsAttached)
            {
                try
                {
                    var info = await AppUpdate.Check();
                    if (info.Available)
                    {
                        ShowDesktop("Hay una versión " + info.Latest + ". Se instala ahora y el programa se vuelve a abrir solo.");
                        await AppUpdate.Apply(services);
                        return;
                    }
                }
                catch
                {
                    // If GitHub does not answer, the current version still opens.
                }
            }

            try
            {
                LaunchWindow(url);
            }
            catch (Exception ex)
            {
                ShowDesktop("No pude abrir la ventana. " + ex.Message);
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            Interlocked.Exchange(ref _exit, 1);
            try
            {
                if (_window != null && !_window.HasExited) _window.Kill(entireProcessTree: true);
            }
            catch
            {
                // The window may already be closed.
            }

            if (_app != null) await _app.StopAsync(cancellationToken);
        }

        private void LaunchWindow(string url)
        {
            var directory = Path.Combine(AppContext.BaseDirectory, "electron");
            var executable = Path.Combine(directory, "electron.exe");
            var script = Path.Combine(directory, "main.cjs");
            if (!File.Exists(executable) || !File.Exists(script))
            {
                throw new FileNotFoundException("No está la ventana de Electron al lado del programa.");
            }

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    Arguments = $"\"{script}\" \"{url}\"",
                    WorkingDirectory = directory,
                    UseShellExecute = false,
                },
                EnableRaisingEvents = true,
            };
            process.Exited += (_, _) => RequestExit();
            if (!process.Start()) throw new InvalidOperationException("No se pudo abrir la ventana.");
            _window = process;
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
