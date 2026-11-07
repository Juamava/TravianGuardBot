using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.IO.Pipes;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Updater
{
    public sealed class UpdateSession
    {
        public const string AssetName = "TravianGuardBot-win-x64.zip";

        private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(30) };

        private readonly string _install;
        private readonly int _pid;
        private readonly string _pipeName;
        private readonly string _url;
        private readonly CancellationTokenSource _cancel = new();
        private Stream? _pipe;
        private bool _closing;

        private UpdateSession(string install, int pid, string version, string url, string pipeName)
        {
            _install = install;
            _pid = pid;
            Version = version;
            _url = url;
            _pipeName = pipeName;
        }

        public string Version { get; }

        public static UpdateSession? Parse(string[] args)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length) continue;
                map[args[i][2..]] = args[++i];
            }

            if (!map.TryGetValue("install", out var install) || install.Length == 0) return null;
            if (!map.TryGetValue("pid", out var pidText) || !int.TryParse(pidText, out var pid) || pid <= 0) return null;
            if (!map.TryGetValue("version", out var version) || version.Length == 0) return null;
            if (!map.TryGetValue("url", out var url) || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return null;
            if (!map.TryGetValue("pipe", out var pipe) || pipe.Length == 0 || pipe.Contains('/') || pipe.Contains('\\')) return null;
            return new UpdateSession(install, pid, version, url, pipe);
        }

        public Task Execute(Action<string, int> progress)
        {
            return Run(progress);
        }

        public bool Cancel()
        {
            if (_closing) return false;
            _cancel.Cancel();
            return true;
        }

        private async Task Run(Action<string, int> progress)
        {
            if (!File.Exists(Path.Combine(_install, "TravianBotSharp.exe")))
                throw new InvalidOperationException("No está el programa en la carpeta de instalación.");

            await using var pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            string? zipPath = null;
            try
            {
                try
                {
                    using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(_cancel.Token);
                    connectTimeout.CancelAfter(TimeSpan.FromSeconds(90));
                    await pipe.ConnectAsync(connectTimeout.Token);
                }
                catch (OperationCanceledException) when (!_cancel.IsCancellationRequested)
                {
                    throw new InvalidOperationException("No se pudo hablar con el programa.");
                }
                catch (TimeoutException)
                {
                    throw new InvalidOperationException("No se pudo hablar con el programa.");
                }

                _pipe = pipe;
                var root = UpdateRoot();
                Directory.CreateDirectory(root);
                zipPath = Path.Combine(root, AssetName);
                var staging = Path.Combine(root, "staging");
                if (Directory.Exists(staging)) Directory.Delete(staging, true);
                Directory.CreateDirectory(staging);

                await Report(progress, "downloading", 10, "Descargando " + Version + "...");
                await Download(zipPath, progress, _cancel.Token);
                _cancel.Token.ThrowIfCancellationRequested();
                await Report(progress, "extracting", 90, "Preparando los archivos...");
                Extract(zipPath, staging);
                _cancel.Token.ThrowIfCancellationRequested();
                if (!File.Exists(Path.Combine(staging, "TravianBotSharp.exe")))
                    throw new InvalidOperationException("El paquete no trae el programa.");

                await Report(progress, "installing", 98, "Cerrando el programa para instalar " + Version + ".");
                _closing = true;
                await Send(new { type = "shutdown" });
                Log("waiting " + _pid);
                await WaitForClient();
                await Task.Delay(2000);
                Install(staging);
            }
            catch (OperationCanceledException) when (_cancel.IsCancellationRequested)
            {
                if (zipPath != null) TryDeleteFile(zipPath);
                if (!_closing && _pipe != null)
                {
                    try
                    {
                        await Send(new { type = "progress", phase = "failed", percent = 0, message = "Actualización cancelada." });
                    }
                    catch
                    {
                        // The client sees the pipe close either way.
                    }
                }

                throw;
            }
            catch (Exception ex)
            {
                if (!_closing && _pipe != null)
                {
                    try
                    {
                        await Send(new { type = "progress", phase = "failed", percent = 0, message = ex.Message });
                    }
                    catch
                    {
                        // The client sees the pipe close either way.
                    }
                }

                throw;
            }
        }

        private void Install(string staging)
        {
            var root = UpdateRoot();
            var backup = Path.Combine(root, "backup");
            var ready = Path.Combine(root, "ready");
            if (!TryDelete(backup))
            {
                const string blocked = "No se pudo preparar la copia de seguridad.";
                WriteError(blocked);
                Launch("--rollback");
                throw new InvalidOperationException(blocked);
            }

            var saved = CopyTree(_install, backup);
            Log("robocopy " + saved + " " + _install);
            if (saved >= 8)
            {
                const string failed = "No se pudo guardar la versión actual.";
                WriteError(failed);
                Launch("--rollback");
                throw new InvalidOperationException(failed);
            }

            var applied = CopyTree(staging, _install);
            Log("robocopy " + applied + " " + staging);
            if (applied >= 8)
            {
                const string failed = "No se pudo instalar la versión nueva. Se vuelve a la anterior.";
                WriteError(failed);
                Restore();
                throw new InvalidOperationException(failed);
            }

            try
            {
                if (File.Exists(ready)) File.Delete(ready);
            }
            catch (Exception ex)
            {
                WriteError("No se pudo preparar el arranque.");
                Log(ex.Message);
                Restore();
                throw new InvalidOperationException("No se pudo preparar el arranque.");
            }

            Process proc;
            try
            {
                proc = Launch("--updated");
            }
            catch (Exception ex)
            {
                Log(ex.Message);
                const string unopened = "No se pudo abrir la versión nueva. Se vuelve a la anterior.";
                WriteError(unopened);
                Restore();
                throw new InvalidOperationException(unopened);
            }

            if (WaitReady(proc, ready, TimeSpan.FromSeconds(90)))
            {
                Log("ready");
                return;
            }

            Log("new version did not stay open, restoring");
            try
            {
                if (!proc.HasExited) proc.Kill(entireProcessTree: true);
            }
            catch
            {
                // The new process may already be gone.
            }

            const string unhealthy = "La versión nueva no llegó a abrir. Se vuelve a la anterior.";
            WriteError(unhealthy);
            Restore();
            throw new InvalidOperationException(unhealthy);
        }

        private void Restore()
        {
            var backup = Path.Combine(UpdateRoot(), "backup");
            if (!File.Exists(Path.Combine(backup, "TravianBotSharp.exe"))) return;
            var code = CopyTree(backup, _install);
            Log("restore " + code);
            Launch("--rollback");
        }

        private Process Launch(string args)
        {
            var exe = Path.Combine(_install, "TravianBotSharp.exe");
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = _install,
                Arguments = args,
                UseShellExecute = false,
            });
            if (process == null) throw new InvalidOperationException("No se pudo abrir el programa.");
            return process;
        }

        private static bool WaitReady(Process process, string ready, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (File.Exists(ready)) return true;
                try
                {
                    if (process.HasExited) return false;
                }
                catch
                {
                    return File.Exists(ready);
                }

                Thread.Sleep(500);
            }

            return File.Exists(ready);
        }

        private async Task WaitForClient()
        {
            using var cancel = new CancellationTokenSource();
            var bye = ReadBye(cancel.Token);
            var exited = await WaitForExit(TimeSpan.FromSeconds(20));
            if (!exited)
            {
                KillClient();
                await WaitForExit(TimeSpan.FromSeconds(10));
            }

            cancel.Cancel();
            try
            {
                await bye;
            }
            catch
            {
                // The process exit is what frees the files.
            }
        }

        private async Task ReadBye(CancellationToken cancellationToken)
        {
            if (_pipe == null) return;
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await ReadLineAsync(_pipe, cancellationToken);
                if (line == null) return;
                if (line.Length == 0) continue;
                using var document = JsonDocument.Parse(line);
                var type = document.RootElement.TryGetProperty("type", out var typeValue) ? typeValue.GetString() : "";
                if (type == "bye") return;
            }
        }

        private async Task<bool> WaitForExit(TimeSpan timeout)
        {
            try
            {
                using var process = Process.GetProcessById(_pid);
                using var cancel = new CancellationTokenSource(timeout);
                await process.WaitForExitAsync(cancel.Token);
                return true;
            }
            catch (ArgumentException)
            {
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private void KillClient()
        {
            try
            {
                using var process = Process.GetProcessById(_pid);
                process.Kill(entireProcessTree: true);
                Log("killed " + _pid);
            }
            catch (ArgumentException)
            {
                // It already exited.
            }
        }

        private async Task Download(string zipPath, Action<string, int> progress, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _url);
            request.Headers.UserAgent.ParseAdd("TravianGuardBot");
            if (_url.Contains("api.github.com", StringComparison.OrdinalIgnoreCase))
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException("GitHub respondió " + (int)response.StatusCode + ".");

            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var destination = File.Create(zipPath);
            var total = response.Content.Headers.ContentLength.GetValueOrDefault();
            var buffer = new byte[81920];
            var read = 0L;
            var last = 10;
            while (true)
            {
                var count = await source.ReadAsync(buffer, cancellationToken);
                if (count == 0) break;
                await destination.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                read += count;
                if (total <= 0) continue;
                var percent = 10 + (int)(read * 70 / total);
                if (percent == last) continue;
                last = percent;
                await Report(progress, "downloading", percent, "Descargando " + percent + "%");
            }
        }

        private static void Extract(string zipPath, string staging)
        {
            var root = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                var target = Path.GetFullPath(Path.Combine(staging, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                var sameFolder = string.Equals(target.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
                if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase) && !sameFolder) continue;
                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                {
                    Directory.CreateDirectory(target);
                    continue;
                }

                var parent = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                entry.ExtractToFile(target, true);
            }
        }

        private int CopyTree(string from, string to)
        {
            Directory.CreateDirectory(to);
            var code = 16;
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                var start = new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.SystemDirectory, "robocopy.exe"),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                start.ArgumentList.Add(from);
                start.ArgumentList.Add(to);
                start.ArgumentList.Add("/E");
                start.ArgumentList.Add("/NFL");
                start.ArgumentList.Add("/NDL");
                start.ArgumentList.Add("/NJH");
                start.ArgumentList.Add("/NJS");
                start.ArgumentList.Add("/XD");
                start.ArgumentList.Add("Data");
                start.ArgumentList.Add("Screenshots");
                start.ArgumentList.Add("Logs");
                start.ArgumentList.Add("/XF");
                start.ArgumentList.Add("*.pdb");
                using var process = Process.Start(start);
                if (process == null) return 16;
                process.WaitForExit();
                code = process.ExitCode;
                if (code < 8) return code;
                Log("robocopy retry " + attempt + " " + code);
                Thread.Sleep(1000);
            }

            return code;
        }

        private async Task Report(Action<string, int> progress, string phase, int percent, string message)
        {
            progress(message, percent);
            await Send(new { type = "progress", phase, percent, message });
        }

        private async Task Send(object message)
        {
            if (_pipe == null) return;
            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json + "\n");
            await _pipe.WriteAsync(bytes);
            await _pipe.FlushAsync();
        }

        private static async Task<string?> ReadLineAsync(Stream stream, CancellationToken cancellationToken = default)
        {
            using var data = new MemoryStream();
            var buffer = new byte[1];
            while (true)
            {
                var read = await stream.ReadAsync(buffer, cancellationToken);
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

        private static string UpdateRoot()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TravianGuardBot", "update");
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // A leftover download is replaced the next time.
            }
        }

        private static bool TryDelete(string path)
        {
            if (!Directory.Exists(path)) return true;
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    Directory.Delete(path, true);
                    return true;
                }
                catch
                {
                    Thread.Sleep(500);
                }
            }

            return !Directory.Exists(path);
        }

        private void WriteError(string message)
        {
            try
            {
                var root = UpdateRoot();
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "last-error"), message);
            }
            catch
            {
                // The log line still records the failure.
            }

            Log(message);
        }

        private void Log(string text)
        {
            var line = DateTimeOffset.Now.ToString("o") + " " + text + Environment.NewLine;
            try
            {
                var root = UpdateRoot();
                Directory.CreateDirectory(root);
                File.AppendAllText(Path.Combine(root, "apply.log"), line);
            }
            catch
            {
                // The install log is the next place.
            }

            try
            {
                var folder = Path.Combine(_install, "logs");
                Directory.CreateDirectory(folder);
                File.AppendAllText(Path.Combine(folder, "update.log"), line);
            }
            catch
            {
                // Logging cannot stop the install.
            }
        }
    }
}
