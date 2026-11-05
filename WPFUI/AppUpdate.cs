using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using MainCore.Entities;
using MainCore.Enums;
using MainCore.Infrasturecture.Persistence;
using MainCore.Services;

using Microsoft.EntityFrameworkCore;
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

        public static string CurrentVersion()
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version == null) return "0.0.0";
            var build = version.Build < 0 ? 0 : version.Build;
            return version.Major + "." + version.Minor + "." + build;
        }

        public static async Task<UpdateInfo> Check()
        {
            lock (Gate)
            {
                if (_checkedAt != DateTime.MinValue && DateTime.UtcNow - _checkedAt < TimeSpan.FromMinutes(30))
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
        }

        public static async Task<string> Apply(IServiceProvider services, Action<string, int> progress = null)
        {
            if (System.Threading.Interlocked.Exchange(ref _applying, 1) == 1)
                throw new InvalidOperationException("La actualización ya está en curso.");
            try
            {
                UpdateSplash.Show(CurrentVersion());
                if (AccountIsOpen(services))
                    throw new InvalidOperationException("Desconectá la cuenta antes de actualizar.");

                var info = await CheckRemote();
                if (!info.Available)
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(info.Detail) ? "Ya está al día." : info.Detail);

                Report(progress, "Descargando " + info.Latest + "...", 10);
                var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TravianGuardBot", "update");
                Directory.CreateDirectory(root);
                var zipPath = Path.Combine(root, AssetName);
                var staging = Path.Combine(root, "staging");
                if (Directory.Exists(staging)) Directory.Delete(staging, true);
                Directory.CreateDirectory(staging);

                await Download(info.AssetUrl, zipPath, progress, info.Latest);
                Report(progress, "Preparando los archivos...", 88);
                Extract(zipPath, staging);
                if (!File.Exists(Path.Combine(staging, "TravianBotSharp.exe")))
                    throw new InvalidOperationException("El paquete no trae el programa.");

                Report(progress, "Se instala " + info.Latest + ". Si algo falla, vuelve sola a la versión " + info.Current + ".", 96);
                var install = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var script = Path.Combine(root, "apply.ps1");
                File.WriteAllText(script, ApplyScript(), new UTF8Encoding(true));
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\" -Install \"" + install + "\" -Staging \"" + staging + "\" -TargetPid " + Environment.ProcessId,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });

                _ = Task.Run(async () =>
                {
                    await Task.Delay(800);
                    System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => System.Windows.Application.Current.Shutdown());
                });

                return "Se cierra el bot para instalar " + info.Latest + ". Se vuelve a abrir solo. Las cuentas y el navegador quedan como están.";
            }
            catch
            {
                System.Threading.Interlocked.Exchange(ref _applying, 0);
                throw;
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
            var response = await SendLatest("");
            if (response.IsSuccessStatusCode) return response;
            var token = ReadToken();
            if (token.Length == 0) return response;
            response.Dispose();
            return await SendLatest(token);
        }

        private static Task<HttpResponseMessage> SendLatest(string token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/" + Repo + "/releases/latest");
            request.Headers.UserAgent.ParseAdd("TravianGuardBot");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            if (token.Length > 0) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return Http.SendAsync(request);
        }

        private static async Task Download(string assetUrl, string zipPath, Action<string, int> progress, string latest)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, assetUrl);
            request.Headers.UserAgent.ParseAdd("TravianGuardBot");
            if (assetUrl.Contains("api.github.com", StringComparison.OrdinalIgnoreCase))
            {
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
                var token = ReadToken();
                if (token.Length > 0) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync();
            await using var destination = File.Create(zipPath);
            var total = response.Content.Headers.ContentLength.GetValueOrDefault();
            var buffer = new byte[81920];
            var read = 0L;
            var last = 10;
            while (true)
            {
                var count = await source.ReadAsync(buffer, 0, buffer.Length);
                if (count == 0) break;
                await destination.WriteAsync(buffer, 0, count);
                read += count;
                if (total <= 0) continue;
                var percent = 10 + (int)(read * 70 / total);
                if (percent == last) continue;
                last = percent;
                Report(progress, "Descargando " + latest + "...", percent);
            }
        }

        private static void Report(Action<string, int> progress, string status, int percent)
        {
            if (progress != null) progress(status, percent);
        }

        private static string ReadyPath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TravianGuardBot", "update", "ready");
        }

        private static void Extract(string zipPath, string staging)
        {
            var root = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
            using var archive = ZipFile.OpenRead(zipPath);
            foreach (var entry in archive.Entries)
            {
                var target = Path.GetFullPath(Path.Combine(staging, entry.FullName.Replace('/', Path.DirectorySeparatorChar)));
                if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase) && !string.Equals(target.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                    continue;
                if (entry.FullName.EndsWith("/", StringComparison.Ordinal) || entry.FullName.EndsWith("\\", StringComparison.Ordinal))
                {
                    Directory.CreateDirectory(target);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target));
                entry.ExtractToFile(target, true);
            }
        }

        private static bool AccountIsOpen(IServiceProvider services)
        {
            var tasks = services.GetRequiredService<TaskManager>();
            using var context = services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
            return context.Accounts.AsEnumerable().Any(account => tasks.GetStatus(new AccountId(account.Id)) != StatusEnums.Offline);
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
                    var output = process.StandardOutput.ReadToEnd().Trim();
                    if (!process.WaitForExit(8000)) continue;
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

        private static string ApplyScript()
        {
            return """
                param(
                  [string]$Install,
                  [string]$Staging,
                  [int]$TargetPid
                )
                $root = Join-Path $env:LOCALAPPDATA 'TravianGuardBot\update'
                $log = Join-Path $root 'apply.log'
                $backup = Join-Path $root 'backup'
                $ready = Join-Path $root 'ready'
                $exe = Join-Path $Install 'TravianBotSharp.exe'
                function Write-Log($text) { Add-Content -Path $log -Value ((Get-Date -Format o) + ' ' + $text) }
                function Copy-Tree($from, $to) {
                  New-Item -ItemType Directory -Force -Path $to | Out-Null
                  & robocopy $from $to /E /NFL /NDL /NJH /NJS /XD Data Screenshots Logs /XF *.pdb > $null
                  $code = $LASTEXITCODE
                  Write-Log ("robocopy " + $code + " " + $from)
                  return $code
                }
                function Start-Current {
                  Start-Process -FilePath $exe -WorkingDirectory $Install
                }
                function Restore-Previous {
                  if (-not (Test-Path (Join-Path $backup 'TravianBotSharp.exe'))) { return }
                  Copy-Tree $backup $Install | Out-Null
                  Start-Current
                }
                try {
                  Write-Log ("waiting " + $TargetPid)
                  Wait-Process -Id $TargetPid -ErrorAction SilentlyContinue
                  Start-Sleep -Seconds 2
                  if (Test-Path $backup) { Remove-Item $backup -Recurse -Force }
                  $saved = Copy-Tree $Install $backup
                  if ($saved -ge 8) {
                    Write-Log 'backup failed, current version stays'
                    Start-Current
                    exit 1
                  }
                  $applied = Copy-Tree $Staging $Install
                  if ($applied -ge 8) {
                    Write-Log 'install failed, restoring'
                    Restore-Previous
                    exit $applied
                  }
                  if (Test-Path $ready) { Remove-Item $ready -Force }
                  $proc = Start-Process -FilePath $exe -WorkingDirectory $Install -ArgumentList '--updated' -PassThru
                  $deadline = (Get-Date).AddSeconds(90)
                  $healthy = $false
                  while ((Get-Date) -lt $deadline) {
                    if (Test-Path $ready) { $healthy = $true; break }
                    if ($proc.HasExited) { break }
                    Start-Sleep -Milliseconds 500
                  }
                  if ($healthy) {
                    Write-Log 'ready'
                    exit 0
                  }
                  Write-Log 'new version did not stay open, restoring'
                  if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
                  Restore-Previous
                  exit 1
                } catch {
                  Write-Log $_.Exception.Message
                  Restore-Previous
                  exit 1
                }
                """;
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
