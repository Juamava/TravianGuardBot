using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using MainCore.Services;

using Microsoft.Extensions.DependencyInjection;

namespace WPFUI
{
    public static class AppUpdate
    {
        public const string AssetName = "TravianGuardBot-win-x64.zip";
        private const string Repo = "Juamava/TravianGuardBot";

        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        private static readonly object Gate = new object();
        private static DateTime _checkedAt;
        private static UpdateInfo _cached = new UpdateInfo();
        private static int _applying;
        private static string _phase = "idle";
        private static int _percent;
        private static string _progress = "";

        public static bool SkipRemoteCheck { get; private set; }
        public static bool SkipAutoInstall { get; private set; }
        public static bool IsExiting { get; private set; }

        public static string CurrentVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version == null) return "0.0.0";
            var build = version.Build < 0 ? 0 : version.Build;
            return version.Major + "." + version.Minor + "." + build;
        }

        public static void CaptureLaunch()
        {
            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (arg == "--updated") SkipRemoteCheck = true;
                else if (arg == "--rollback") SkipAutoInstall = true;
            }
        }

        public static string StartupStatus()
        {
            if (SkipRemoteCheck) return "Abriendo la versión " + CurrentVersion() + "...";
            if (!SkipAutoInstall) return "";
            var error = LastError();
            return error.Length > 0 ? error : "Se abre la versión anterior.";
        }

        public static int StartupPercent()
        {
            if (SkipRemoteCheck) return 100;
            if (!SkipAutoInstall) return -1;
            return LastError().Length > 0 ? 0 : 100;
        }

        public static async Task<UpdateInfo> Check(bool force = false)
        {
            if (!force && SkipRemoteCheck)
            {
                lock (Gate)
                {
                    if (_checkedAt == DateTime.MinValue)
                        _cached = new UpdateInfo { Current = CurrentVersion() };
                    return _cached;
                }
            }

            lock (Gate)
            {
                if (!force && _checkedAt != DateTime.MinValue && DateTime.UtcNow - _checkedAt < TimeSpan.FromMinutes(30))
                    return _cached;
            }

            var info = await CheckRemote();
            lock (Gate)
            {
                _cached = info;
                _checkedAt = DateTime.UtcNow;
            }

            return info;
        }

        public static void MarkReady()
        {
            var path = ReadyPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, CurrentVersion());
            if (!SkipRemoteCheck) return;
            try
            {
                var error = Path.Combine(UpdateRoot(), "last-error");
                if (File.Exists(error)) File.Delete(error);
            }
            catch
            {
                // The new version is already open.
            }
        }

        public static string LastError()
        {
            try
            {
                var path = Path.Combine(UpdateRoot(), "last-error");
                return File.Exists(path) ? File.ReadAllText(path).Trim() : "";
            }
            catch
            {
                return "";
            }
        }

        public static UpdateInfo Peek()
        {
            lock (Gate)
            {
                return _cached;
            }
        }

        public static (string Phase, int Percent, string Message) Progress()
        {
            lock (Gate)
            {
                return (_phase, _percent, _progress);
            }
        }

        public static string Begin(IServiceProvider services)
        {
            if (System.Threading.Interlocked.Exchange(ref _applying, 1) == 1)
                return "La actualización ya se está descargando.";

            SetProgress("downloading", 10, "Descargando...");
            _ = Task.Run(async () =>
            {
                try
                {
                    var info = Peek();
                    if (!info.Available || string.IsNullOrWhiteSpace(info.AssetUrl))
                        info = await Check(true);
                    await HandOff(services, info, null);
                }
                catch (Exception ex)
                {
                    SetProgress("failed", 0, ex.Message);
                    System.Threading.Interlocked.Exchange(ref _applying, 0);
                    AppLog.Write("actualización", "No se pudo actualizar.", ex);
                }
            });
            return "Se descarga mientras el bot sigue. Cuando termine se cierra para instalar y se vuelve a abrir.";
        }

        public static async Task<string> Apply(IServiceProvider services, UpdateInfo info, Action<string, int> progress = null)
        {
            if (System.Threading.Interlocked.Exchange(ref _applying, 1) == 1)
                throw new InvalidOperationException("La actualización ya está en curso.");
            try
            {
                return await HandOff(services, info, progress);
            }
            catch
            {
                System.Threading.Interlocked.Exchange(ref _applying, 0);
                throw;
            }
        }

        private static async Task<string> HandOff(IServiceProvider services, UpdateInfo info, Action<string, int> progress)
        {
            if (info == null || !info.Available || string.IsNullOrWhiteSpace(info.AssetUrl))
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(info?.Detail) ? "Ya está al día." : info.Detail);
            if (!info.AssetUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("La descarga no es válida.");

            var source = Path.Combine(AppContext.BaseDirectory, "TravianGuardUpdater.exe");
            if (!File.Exists(source))
                throw new InvalidOperationException("No está TravianGuardUpdater.exe al lado del programa.");

            var destination = CopyUpdater(source);
            var pipeName = "TravianGuardBot.Update." + Environment.ProcessId;
            var install = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            SetProgress("downloading", 10, "Descargando " + info.Latest + "...");
            Report(progress, "Descargando " + info.Latest + "...", 10);

            await using var server = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            using var connectTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            var connection = server.WaitForConnectionAsync(connectTimeout.Token);
            try
            {
                StartUpdater(destination, install, info.Latest, info.AssetUrl, pipeName);
                await connection;
            }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException("El actualizador no llegó a conectarse.");
            }
            catch (Exception)
            {
                connectTimeout.Cancel();
                try { await connection; } catch { }
                throw;
            }

            AppLog.Write("actualización", "El actualizador de " + info.Latest + " está conectado.");
            return await Listen(server, services, progress, info.Latest);
        }

        private static async Task<string> Listen(NamedPipeServerStream server, IServiceProvider services, Action<string, int> progress, string latest)
        {
            while (true)
            {
                var line = await ReadLineAsync(server);
                if (line == null) throw new InvalidOperationException("El actualizador se desconectó.");
                if (line.Length == 0) continue;
                using var document = JsonDocument.Parse(line);
                var type = document.RootElement.TryGetProperty("type", out var typeValue) ? typeValue.GetString() : "";
                if (type == "progress")
                {
                    var phase = document.RootElement.TryGetProperty("phase", out var phaseValue) ? phaseValue.GetString() ?? "downloading" : "downloading";
                    var percent = document.RootElement.TryGetProperty("percent", out var percentValue) && percentValue.TryGetInt32(out var parsed) ? parsed : 0;
                    var message = document.RootElement.TryGetProperty("message", out var messageValue) ? messageValue.GetString() ?? "" : "";
                    SetProgress(phase, percent, message);
                    Report(progress, message, percent);
                    if (phase == "failed")
                        throw new InvalidOperationException(message.Length == 0 ? "No se pudo actualizar." : message);
                    continue;
                }

                if (type != "shutdown") continue;
                var bye = Encoding.UTF8.GetBytes("{\"type\":\"bye\"}\n");
                try
                {
                    await server.WriteAsync(bye);
                    await server.FlushAsync();
                }
                catch
                {
                    // The updater also watches the process.
                }

                await ExitForInstall(services);
                return "Se cierra para instalar " + latest + ". Se vuelve a abrir solo.";
            }
        }

        private static async Task ExitForInstall(IServiceProvider services)
        {
            IsExiting = true;
            try
            {
                var chrome = services.GetService<ChromeManager>();
                if (chrome != null) await chrome.Shutdown();
            }
            catch (Exception ex)
            {
                AppLog.Write("actualización", "No se pudieron cerrar los navegadores.", ex);
            }

            var app = System.Windows.Application.Current;
            if (app == null)
            {
                Environment.Exit(0);
                return;
            }

            _ = app.Dispatcher.BeginInvoke(() =>
            {
                UpdateSplash.Close();
                app.Shutdown();
            });
        }

        private static string CopyUpdater(string source)
        {
            var root = UpdateRoot();
            Directory.CreateDirectory(root);
            var destination = Path.Combine(root, "TravianGuardUpdater.exe");
            try
            {
                File.Copy(source, destination, true);
                return destination;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                destination = Path.Combine(root, "TravianGuardUpdater-" + Environment.ProcessId + ".exe");
                File.Copy(source, destination, true);
                return destination;
            }
        }

        private static void StartUpdater(string exe, string install, string version, string url, string pipeName)
        {
            // cmd start leaves the updater outside this process tree, so closing the client does not stop the install.
            var command = "/c start \"\" "
                + QuoteForCmd(exe)
                + " --install " + QuoteForCmd(install)
                + " --pid " + Environment.ProcessId
                + " --version " + QuoteForCmd(version)
                + " --url " + QuoteForCmd(url)
                + " --pipe " + QuoteForCmd(pipeName);
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                Arguments = command,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(exe),
            });
            if (process == null) throw new InvalidOperationException("No se pudo abrir el actualizador.");
        }

        private static string QuoteForCmd(string value)
        {
            return "\"" + value.Replace("\"", "").Replace("%", "%%") + "\"";
        }

        private static async Task<string> ReadLineAsync(Stream stream)
        {
            using var data = new MemoryStream();
            var buffer = new byte[1];
            while (true)
            {
                var read = await stream.ReadAsync(buffer);
                if (read == 0)
                {
                    if (data.Length == 0) return null;
                    break;
                }

                if (buffer[0] == '\n') break;
                if (buffer[0] != '\r') data.WriteByte(buffer[0]);
            }

            return Encoding.UTF8.GetString(data.ToArray());
        }

        private static void SetProgress(string phase, int percent, string message)
        {
            lock (Gate)
            {
                _phase = phase;
                _percent = percent;
                _progress = message;
            }
        }

        private static async Task<UpdateInfo> CheckRemote()
        {
            var current = CurrentVersion();
            using var response = await SendLatest();
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateInfo
                {
                    Current = current,
                    Detail = "GitHub respondió " + (int)response.StatusCode + ".",
                };
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = document.RootElement;
            var tag = root.TryGetProperty("tag_name", out var tagValue) ? tagValue.GetString() ?? "" : "";
            var latest = tag.Trim().TrimStart('v', 'V');
            var publishedAt = root.TryGetProperty("published_at", out var publishedValue) ? publishedValue.GetString() ?? "" : "";
            var assetUrl = "";
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var nameValue) ? nameValue.GetString() : "";
                    if (!string.Equals(name, AssetName, StringComparison.OrdinalIgnoreCase)) continue;
                    var browserUrl = asset.TryGetProperty("browser_download_url", out var browserValue) ? browserValue.GetString() ?? "" : "";
                    var apiUrl = asset.TryGetProperty("url", out var urlValue) ? urlValue.GetString() ?? "" : "";
                    assetUrl = browserUrl.Length > 0 ? browserUrl : apiUrl;
                    break;
                }
            }

            DateTime published;
            var knownDate = DateTime.TryParse(publishedAt, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out published);
            var available = assetUrl.Length > 0 && IsNewer(latest, current);
            var mandatory = available && knownDate && DateTime.UtcNow - published.ToUniversalTime() >= TimeSpan.FromDays(1);
            return new UpdateInfo
            {
                Current = current,
                Latest = latest,
                Available = available,
                Mandatory = mandatory,
                PublishedAt = publishedAt,
                AssetUrl = assetUrl,
                Detail = available ? "" : "Ya está al día.",
            };
        }

        private static async Task<HttpResponseMessage> SendLatest()
        {
            var response = await SendLatest("", TimeSpan.FromSeconds(20));
            if (response.IsSuccessStatusCode) return response;
            var token = ReadToken();
            if (token.Length == 0) return response;
            response.Dispose();
            return await SendLatest(token, TimeSpan.FromSeconds(20));
        }

        private static async Task<HttpResponseMessage> SendLatest(string token, TimeSpan timeout)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/" + Repo + "/releases/latest");
            request.Headers.UserAgent.ParseAdd("TravianGuardBot");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            if (token.Length > 0) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var cancel = new CancellationTokenSource(timeout);
            try
            {
                return await Http.SendAsync(request, cancel.Token);
            }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException("GitHub no respondió a tiempo.");
            }
        }

        private static void Report(Action<string, int> progress, string status, int percent)
        {
            if (progress != null) progress(status, percent);
        }

        private static string UpdateRoot()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TravianGuardBot", "update");
        }

        private static string ReadyPath()
        {
            return Path.Combine(UpdateRoot(), "ready");
        }

        private static string ReadToken()
        {
            var fromEnv = Environment.GetEnvironmentVariable("TRAVIAN_GITHUB_TOKEN");
            if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv.Trim();

            var candidates = new[]
            {
                "gh",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GitHub CLI", "gh.exe"),
            };
            foreach (var candidate in candidates)
            {
                try
                {
                    var start = new ProcessStartInfo
                    {
                        FileName = candidate,
                        Arguments = "auth token",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };
                    using var process = Process.Start(start);
                    if (process == null) continue;
                    if (!process.WaitForExit(4000))
                    {
                        try { process.Kill(true); } catch { }
                        continue;
                    }
                    var output = process.StandardOutput.ReadToEnd().Trim();
                    if (process.ExitCode == 0 && output.Length > 0 && !output.Contains(' ')) return output;
                }
                catch
                {
                    // The next location may have the CLI.
                }
            }

            return "";
        }

        private static bool IsNewer(string latest, string current)
        {
            Version left;
            Version right;
            if (!Version.TryParse(latest, out left) || !Version.TryParse(current, out right)) return false;
            return left > right;
        }
    }

    public sealed class UpdateInfo
    {
        public string Current { get; set; } = "";
        public string Latest { get; set; } = "";
        public bool Available { get; set; }
        public bool Mandatory { get; set; }
        public string PublishedAt { get; set; } = "";
        public string Detail { get; set; } = "";
        public string AssetUrl { get; set; } = "";
    }
}
