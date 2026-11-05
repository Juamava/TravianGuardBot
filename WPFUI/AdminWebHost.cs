using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
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
            AppDomain.CurrentDomain.ProcessExit += (_, _) => CloseWindow();
            var wwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            AppLog.Write("arranque", DescribeInterface(wwwroot));
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
                catch (Exception ex) when (IsAddressInUse(ex))
                {
                    AppLog.Write("arranque", "El puerto " + port + " ya está en uso.");
                    _app = null;
                }
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                AppLog.Write("arranque", "No quedó ningún puerto libre para la administración.");
                ShowDesktop("No pude abrir la administración.");
                return;
            }

            AppLog.Write("arranque", "Administración en " + url);

            if (!Debugger.IsAttached)
            {
                try
                {
                    UpdateSplash.Report("Comprobando la versión...", -1, "");
                    var info = await AppUpdate.Check();
                    if (info.Available)
                    {
                        UpdateSplash.Report("Hay una versión " + info.Latest + ".", 5, info.Latest);
                        await AppUpdate.Apply(services, (status, percent) => UpdateSplash.Report(status, percent, info.Latest));
                        return;
                    }

                    UpdateSplash.Report("Versión al día.", 100, "");
                }
                catch (Exception ex)
                {
                    AppLog.Write("actualización", "No se pudo comprobar la versión.", ex);
                    UpdateSplash.Report("No se pudo actualizar. Se abre esta versión. " + ex.Message, 100, "");
                }
            }
            else
            {
                UpdateSplash.Report("Listo.", 100, "");
            }

            try
            {
                LaunchWindow(url);
                AppUpdate.MarkReady();
                await Task.Delay(700);
                UpdateSplash.Close();
            }
            catch (Exception ex)
            {
                AppLog.Write("ventana", "No se pudo abrir Electron.", ex);
                ShowDesktop("No pude abrir la ventana. " + ex.Message);
                if (StartedAfterUpdate())
                {
                    UpdateSplash.Report("No se pudo abrir. Se vuelve a la versión anterior.", 0, "");
                    Environment.Exit(1);
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            Interlocked.Exchange(ref _exit, 1);
            CloseWindow();
            if (_app != null) await _app.StopAsync(cancellationToken);
        }

        private void CloseWindow()
        {
            try
            {
                if (_window != null && !_window.HasExited) _window.Kill(entireProcessTree: true);
            }
            catch
            {
                // The window may already be closed.
            }
        }

        private static bool StartedAfterUpdate()
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == "--updated") return true;
            }

            return false;
        }

        private void LaunchWindow(string url)
        {
            var directory = Path.Combine(AppContext.BaseDirectory, "electron");
            var executable = Path.Combine(directory, "electron.exe");
            var script = Path.Combine(directory, "main.cjs");
            if (!File.Exists(executable) || !File.Exists(script))
            {
                AppLog.Write("ventana", "Falta Electron en " + directory);
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
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                },
                EnableRaisingEvents = true,
            };
            process.StartInfo.Environment["TBS_LOG_DIR"] = AppLog.DirectoryPath;
            process.OutputDataReceived += (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data)) AppLog.Write("electron", args.Data);
            };
            process.ErrorDataReceived += (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data)) AppLog.Write("electron", args.Data);
            };
            process.Exited += (_, _) =>
            {
                var code = -1;
                try { code = process.ExitCode; } catch { /* The process can disappear before the code is read. */ }
                AppLog.Write("ventana", "Electron se cerró con código " + code);
                RequestExit();
            };
            if (!process.Start()) throw new InvalidOperationException("No se pudo abrir la ventana.");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _window = process;
            AppLog.Write("ventana", "Electron " + process.Id + " abrió " + url);
        }

        private static string DescribeInterface(string wwwroot)
        {
            var index = Path.Combine(wwwroot, "index.html");
            if (!File.Exists(index)) return "No está wwwroot/index.html";
            var html = File.ReadAllText(index);
            var asset = "";
            var marker = "src=\"";
            var start = html.IndexOf(marker, StringComparison.Ordinal);
            if (start >= 0)
            {
                start += marker.Length;
                var end = html.IndexOf('"', start);
                if (end > start) asset = html.Substring(start, end - start);
            }

            if (asset.StartsWith('/')) asset = asset.Substring(1);
            var assetPath = string.IsNullOrWhiteSpace(asset) ? "" : Path.Combine(wwwroot, asset.Replace('/', Path.DirectorySeparatorChar));
            var assetState = assetPath.Length == 0 ? "sin script" : File.Exists(assetPath) ? "script listo" : "falta " + asset;
            return "Interfaz " + assetState;
        }

        private static bool IsAddressInUse(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is SocketException socket && socket.SocketErrorCode == SocketError.AddressAlreadyInUse) return true;
                if (current.GetType().Name == "AddressInUseException") return true;
            }

            return false;
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
