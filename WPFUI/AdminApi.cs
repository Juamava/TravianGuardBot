using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using FluentValidation;

using Humanizer;

using MainCore.Commands.Misc;
using MainCore.DTO;
using MainCore.Entities;
using MainCore.Enums;
using MainCore.Infrasturecture.Extensions;
using MainCore.Infrasturecture.Persistence;
using MainCore.Models;
using MainCore.Notifications;
using MainCore.Parsers;
using MainCore.Services;
using MainCore.Tasks;
using MainCore.Tasks.Base;
using MainCore.UI.Models.Input;
using MainCore.UI.Models.Output;
using MainCore.UI.ViewModels.Tabs;
using MainCore.UI.ViewModels.Tabs.Villages;
using MainCore.UI.ViewModels.UserControls;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WPFUI
{
    public static class AdminApi
    {
        public static void Map(WebApplication app, IServiceProvider services)
        {
            app.Use(async (context, next) =>
            {
                if (NeedsConnection(context.Request.Path, out var accountId))
                {
                    var blocked = RequireConnected(services, accountId);
                    if (blocked != null)
                    {
                        await blocked.ExecuteAsync(context);
                        return;
                    }
                }

                await next();
            });

            app.MapGet("/api/health", () => Ok("listo"));
            app.MapGet("/api/meta", Meta);
            app.MapGet("/api/overview", () => Overview(services));
            app.MapGet("/api/update", (bool force = false) => CheckUpdate(force));
            app.MapPost("/api/update", () => ApplyUpdate(services));
            app.MapPost("/api/shutdown", () =>
            {
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => System.Windows.Application.Current.Shutdown());
                return Ok("Cerrando");
            });

            app.MapPost("/api/accounts", (AccountBody body) => AddAccount(services, body));
            app.MapPost("/api/accounts/bulk", (BulkBody body) => AddAccounts(services, body.Text));
            app.MapGet("/api/accounts/{id:int}", (int id) => GetAccount(services, id));
            app.MapPut("/api/accounts/{id:int}", (int id, AccountBody body) => EditAccount(services, id, body));
            app.MapDelete("/api/accounts/{id:int}", (int id) => DeleteAccount(services, id));
            app.MapPost("/api/accounts/{id:int}/login", (int id) => Login(services, id));
            app.MapPost("/api/accounts/{id:int}/logout", (int id) => Logout(services, id));
            app.MapPost("/api/accounts/{id:int}/pause", (int id) => Pause(services, id));
            app.MapPost("/api/accounts/{id:int}/restart", (int id) => Restart(services, id));

            app.MapGet("/api/accounts/{id:int}/settings", (int id) => GetAccountSettings(services, id));
            app.MapPut("/api/accounts/{id:int}/settings", (int id, Dictionary<string, int> body) => SaveAccountSettings(services, id, body));
            app.MapGet("/api/accounts/{id:int}/villages", (int id) => GetVillages(services, id));
            app.MapPost("/api/accounts/{id:int}/villages/reload-missing", (int id) => ReloadVillages(services, id, false));
            app.MapPost("/api/accounts/{id:int}/villages/reload-all", (int id) => ReloadVillages(services, id, true));
            app.MapPost("/api/accounts/{id:int}/villages/{villageId:int}/reload", (int id, int villageId) => ReloadVillage(services, id, villageId));
            app.MapGet("/api/accounts/{id:int}/villages/{villageId:int}/build", (int id, int villageId) => GetBuild(services, id, villageId));
            app.MapGet("/api/accounts/{id:int}/villages/{villageId:int}/build/options", (int id, int villageId, int location) => GetBuildOptions(services, id, villageId, location));
            app.MapPost("/api/accounts/{id:int}/villages/{villageId:int}/jobs/normal", (int id, int villageId, NormalJobBody body) => AddNormalJob(services, id, villageId, body));
            app.MapPost("/api/accounts/{id:int}/villages/{villageId:int}/jobs/resource", (int id, int villageId, ResourceJobBody body) => AddResourceJob(services, id, villageId, body));
            app.MapPost("/api/accounts/{id:int}/villages/{villageId:int}/jobs/upgrade", (int id, int villageId, UpgradeBody body) => UpgradeBuilding(services, id, villageId, body));
            app.MapPost("/api/accounts/{id:int}/villages/{villageId:int}/jobs/{jobId:int}/move", (int id, int villageId, int jobId, MoveBody body) => MoveJob(services, id, villageId, jobId, body.Direction));
            app.MapDelete("/api/accounts/{id:int}/villages/{villageId:int}/jobs/{jobId:int}", (int id, int villageId, int jobId) => DeleteJob(services, id, villageId, jobId));
            app.MapDelete("/api/accounts/{id:int}/villages/{villageId:int}/jobs", (int id, int villageId) => DeleteJobs(services, id, villageId));
            app.MapPost("/api/accounts/{id:int}/villages/{villageId:int}/jobs/import", (int id, int villageId, ImportJobsBody body) => ImportJobs(services, id, villageId, body));
            app.MapGet("/api/accounts/{id:int}/villages/{villageId:int}/jobs/export", (int id, int villageId) => ExportJobs(services, id, villageId));
            app.MapGet("/api/accounts/{id:int}/villages/{villageId:int}/settings", (int id, int villageId) => GetVillageSettings(services, id, villageId));
            app.MapPut("/api/accounts/{id:int}/villages/{villageId:int}/settings", (int id, int villageId, Dictionary<string, int> body) => SaveVillageSettings(services, id, villageId, body));

            app.MapGet("/api/accounts/{id:int}/farming", (int id, int villageId) => GetFarming(services, id, villageId));
            app.MapPost("/api/accounts/{id:int}/farming/lists", (int id, CowListBody body) => CreateCowList(services, id, body));
            app.MapPut("/api/accounts/{id:int}/farming/lists/{listId:int}", (int id, int listId, CowListBody body) => UpdateCowList(services, id, listId, body));
            app.MapDelete("/api/accounts/{id:int}/farming/lists/{listId:int}", (int id, int listId) => DeleteCowList(services, id, listId));
            app.MapPost("/api/accounts/{id:int}/farming/lists/{listId:int}/villages", (int id, int listId, CowVillageBody body) => AddCowVillage(services, id, listId, body));
            app.MapPost("/api/accounts/{id:int}/farming/lists/{listId:int}/bulk", (int id, int listId, BulkBody body) => BulkEditCowList(services, id, listId, body.Text));
            app.MapPut("/api/accounts/{id:int}/farming/villages/{cowId:int}", (int id, int cowId, CowVillageBody body) => UpdateCowVillage(services, id, cowId, body));
            app.MapDelete("/api/accounts/{id:int}/farming/villages/{cowId:int}", (int id, int cowId) => DeleteCowVillage(services, id, cowId));
            app.MapPost("/api/accounts/{id:int}/farming/lists/{listId:int}/sync", (int id, int listId, HttpContext http) => SyncVillageNames(services, id, listId, http.RequestAborted));
            app.MapPost("/api/accounts/{id:int}/farming/lists/{listId:int}/start", (int id, int listId, CowListBody body) => StartCowList(services, id, listId, body));
            app.MapPost("/api/accounts/{id:int}/farming/lists/{listId:int}/stop", (int id, int listId) => StopCowList(services, id, listId));
            app.MapPost("/api/accounts/{id:int}/farming/lists/{listId:int}/search", (int id, int listId, SearchBody body) => SearchOasis(services, id, listId, body));
            app.MapPost("/api/accounts/{id:int}/farming/lists/{listId:int}/search/apply", (int id, int listId, ApplySearchBody body) => ApplyOasis(services, id, listId, body));
            app.MapPost("/api/accounts/{id:int}/farming/game/refresh", (int id) => RefreshGameFarms(services, id));
            app.MapPost("/api/accounts/{id:int}/farming/game/start", (int id) => StartGameFarms(services, id));
            app.MapPost("/api/accounts/{id:int}/farming/game/stop", (int id) => StopGameFarms(services, id));
            app.MapPost("/api/accounts/{id:int}/farming/game/{farmId:int}/active", (int id, int farmId) => ToggleGameFarm(services, id, farmId));
            app.MapPut("/api/accounts/{id:int}/farming/game/options", (int id, GameFarmBody body) => SaveGameFarmOptions(services, id, body));

            app.MapGet("/api/accounts/{id:int}/hero", (int id) => GetHero(services, id));

            app.MapGet("/api/accounts/{id:int}/debug", (int id) => GetDebug(services, id));
            app.MapPost("/api/accounts/{id:int}/debug/front", (int id) => BringToFront(services, id));
            app.MapPost("/api/logs/open", () => OpenLogs());
            app.MapPost("/api/templates/{kind}", (string kind, TemplateBody body) => SetTemplate(services, kind, body.Path));
            app.MapDelete("/api/templates/{kind}", (string kind) => ClearTemplate(services, kind));
            app.MapPost("/api/templates/{kind}/upload", async (string kind, HttpRequest request) => await UploadTemplate(services, kind, request));
        }

        private static async Task<IResult> CheckUpdate(bool force)
        {
            var info = await AppUpdate.Check(force);
            return Ok(info.Detail ?? "", new { current = info.Current, latest = info.Latest, available = info.Available, mandatory = info.Mandatory, publishedAt = info.PublishedAt, detail = info.Detail });
        }

        private static async Task<IResult> ApplyUpdate(IServiceProvider services)
        {
            try
            {
                return Ok(await AppUpdate.Apply(services));
            }
            catch (Exception ex)
            {
                return Fail(ex.Message);
            }
        }

        private static IResult Meta()
        {
            return Ok("", new
            {
                tribes = Enum.GetValues<TribeEnums>().Select(x => new { name = x.ToString(), value = (int)x }),
                troops = Enum.GetValues<TroopEnums>().Select(x => new { name = x.ToString(), value = (int)x }),
                resourcePlans = Enum.GetValues<ResourcePlanEnums>().Select(x => new { name = x.Humanize(), value = x.ToString() }),
            });
        }

        private static IResult Overview(IServiceProvider services)
        {
            using var context = Context(services);
            var tasks = services.GetRequiredService<TaskManager>();
            var templates = services.GetRequiredService<DefaultTemplatePathStore>().Get();
            var versionAssembly = typeof(AppDbContext).Assembly.GetName().Version;
            var version = versionAssembly is null ? "" : $"{versionAssembly.Major}.{versionAssembly.Minor}.{versionAssembly.Build}";
            var accounts = context.Accounts.AsEnumerable().Select(account =>
            {
                var status = tasks.GetStatus(new AccountId(account.Id));
                return new
                {
                    id = account.Id,
                    username = account.Username,
                    server = account.Server,
                    host = SafeHost(account.Server),
                    status = status.ToString(),
                    pause = PauseLabel(status),
                };
            }).ToList();

            return Ok("", new
            {
                version,
                templates = new { account = templates.AccountSettingsPath, village = templates.VillageSettingsPath },
                accounts,
            });
        }

        private static IResult AddAccount(IServiceProvider services, AccountBody body)
        {
            var input = ToInput(body);
            var error = ValidateAccount(services, input);
            if (error is not null) return Fail(error);

            var dto = input.ToDto();
            using var context = Context(services);
            if (context.Accounts.Any(x => x.Username == dto.Username && x.Server == dto.Server)) return Fail("La cuenta ya existe.");

            InsertAccounts(services, context, [dto]);
            services.GetRequiredService<RxQueue>().Enqueue(new AccountsModified());
            return Ok("Cuenta agregada.");
        }

        private static IResult AddAccounts(IServiceProvider services, string text)
        {
            var parsed = (text ?? "").Trim().Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(ParseLine)
                .Where(x => x is not null)
                .Select(x => x.ToDto())
                .ToList();
            if (parsed.Count == 0) return Fail("No encontré cuentas. Cada línea es: url usuario contraseña");

            using var context = Context(services);
            var existing = context.Accounts.Select(x => new { x.Username, x.Server }).ToList();
            var fresh = parsed.Where(dto => !existing.Exists(x => x.Username == dto.Username && x.Server == dto.Server)).ToList();
            if (fresh.Count == 0) return Fail("Todas las cuentas ya existen.");

            InsertAccounts(services, context, fresh);
            services.GetRequiredService<RxQueue>().Enqueue(new AccountsModified());
            return Ok($"Se agregaron {fresh.Count} cuentas.");
        }

        private static IResult GetAccount(IServiceProvider services, int id)
        {
            using var context = Context(services);
            var account = context.Accounts.Where(x => x.Id == id).Include(x => x.Accesses).ToDto().FirstOrDefault();
            if (account is null) return Fail("No se encontró la cuenta.");
            return Ok("", new
            {
                id = account.Id.Value,
                username = account.Username,
                server = account.Server,
                accesses = account.Accesses.Select(access => new
                {
                    id = access.Id.Value,
                    username = access.Username,
                    password = access.Password,
                    proxyHost = access.ProxyHost,
                    proxyPort = access.ProxyPort,
                    proxyUsername = access.ProxyUsername,
                    proxyPassword = access.ProxyPassword,
                    useragent = access.Useragent,
                }),
            });
        }

        private static IResult EditAccount(IServiceProvider services, int id, AccountBody body)
        {
            var input = ToInput(body);
            input.Id = new AccountId(id);
            var error = ValidateAccount(services, input);
            if (error is not null) return Fail(error);

            var dto = input.ToDto();
            dto.Id = new AccountId(id);
            var account = dto.ToEntity();
            using var context = Context(services);
            var ids = dto.Accesses.Select(x => x.Id.Value).ToList();
            context.Accesses.Where(x => x.AccountId == id && !ids.Contains(x.Id)).ExecuteDelete();
            context.Update(account);
            context.SaveChanges();
            services.GetRequiredService<RxQueue>().Enqueue(new AccountsModified());
            return Ok("Cuenta guardada.");
        }

        private static IResult DeleteAccount(IServiceProvider services, int id)
        {
            var accountId = new AccountId(id);
            var tasks = services.GetRequiredService<TaskManager>();
            if (tasks.GetStatus(accountId) != StatusEnums.Offline) return Fail("La cuenta tiene que estar desconectada.");

            using var context = Context(services);
            var cowListIds = context.CowLists.Where(x => x.AccountId == id).Select(x => x.Id).ToList();
            if (cowListIds.Count > 0)
            {
                context.CowVillages.Where(x => cowListIds.Contains(x.CowListId)).ExecuteDelete();
                context.CowLists.Where(x => x.AccountId == id).ExecuteDelete();
            }

            var deleted = context.Accounts.Where(x => x.Id == id).ExecuteDelete();
            if (deleted == 0) return Fail("No se encontró la cuenta.");
            services.GetRequiredService<RxQueue>().Enqueue(new AccountsModified());
            return Ok("Cuenta borrada.");
        }

        private static async Task<IResult> Login(IServiceProvider services, int id)
        {
            var accountId = new AccountId(id);
            var tasks = services.GetRequiredService<TaskManager>();
            if (tasks.GetStatus(accountId) != StatusEnums.Offline) return Fail("La cuenta tiene que estar desconectada.");

            using var context = Context(services);
            var otherOpen = context.Accounts.AsEnumerable().Any(account => account.Id != id && tasks.GetStatus(new AccountId(account.Id)) != StatusEnums.Offline);
            if (otherOpen) return Fail("Esta instancia ya tiene una cuenta conectada. Abrí otro bot para administrar la otra.");

            var access = await context.GetValidAccess(accountId);
            if (access.IsFailed) return Fail(string.Join(Environment.NewLine, access.Errors.Select(x => x.Message)));

            tasks.SetStatus(accountId, StatusEnums.Starting);
            try
            {
                using var scope = services.GetRequiredService<CustomServiceScopeFactory>().CreateScope(accountId);
                var handler = scope.ServiceProvider.GetRequiredService<OpenBrowserCommand.Handler>();
                await handler.HandleAsync(new OpenBrowserCommand.Command(accountId, access.Value));
            }
            catch (Exception ex)
            {
                tasks.SetStatus(accountId, StatusEnums.Offline);
                return Fail("No se pudo abrir el navegador. " + ex.Message);
            }

            services.GetRequiredService<TimerManager>().Start(accountId);
            tasks.SetStatus(accountId, StatusEnums.Online);
            services.GetRequiredService<RxQueue>().Enqueue(new AccountInit(accountId));
            return Ok("Sesión abierta.");
        }

        private static async Task<IResult> Logout(IServiceProvider services, int id)
        {
            var accountId = new AccountId(id);
            var tasks = services.GetRequiredService<TaskManager>();
            var status = tasks.GetStatus(accountId);
            if (status == StatusEnums.Offline) return Fail("El navegador ya está cerrado.");
            if (status is StatusEnums.Starting or StatusEnums.Pausing or StatusEnums.Stopping) return Fail($"Hay que esperar: {status}.");

            var browser = services.GetRequiredService<ChromeManager>().Get(accountId);
            tasks.SetStatus(accountId, StatusEnums.Stopping);
            await tasks.StopCurrentTask(accountId);
            await browser.Shutdown();
            tasks.SetStatus(accountId, StatusEnums.Offline);
            return Ok("Sesión cerrada.");
        }

        private static async Task<IResult> Pause(IServiceProvider services, int id)
        {
            var accountId = new AccountId(id);
            var tasks = services.GetRequiredService<TaskManager>();
            var status = tasks.GetStatus(accountId);
            switch (status)
            {
                case StatusEnums.Paused:
                    tasks.SetStatus(accountId, StatusEnums.Online);
                    return Ok("Reanudada.");
                case StatusEnums.Online:
                    await tasks.StopCurrentTask(accountId);
                    return Ok("En pausa.");
                default:
                    return Fail($"La cuenta está {status}.");
            }
        }

        private static async Task<IResult> Restart(IServiceProvider services, int id)
        {
            var accountId = new AccountId(id);
            var tasks = services.GetRequiredService<TaskManager>();
            var status = tasks.GetStatus(accountId);
            switch (status)
            {
                case StatusEnums.Online:
                    return Fail("Primero hay que pausar la cuenta.");
                case StatusEnums.Paused:
                    tasks.SetStatus(accountId, StatusEnums.Starting);
                    await Task.Delay(300);
                    tasks.Clear(accountId);
                    services.GetRequiredService<RxQueue>().Enqueue(new AccountInit(accountId));
                    tasks.SetStatus(accountId, StatusEnums.Online);
                    return Ok("Reiniciada.");
                default:
                    return Fail($"La cuenta está {status}.");
            }
        }

        private static IResult GetAccountSettings(IServiceProvider services, int id)
        {
            using var context = Context(services);
            var input = ReadAccountSettings(context, id);
            return Ok("", input.Get().ToDictionary(x => x.Key.ToString(), x => x.Value));
        }

        private static IResult SaveAccountSettings(IServiceProvider services, int id, Dictionary<string, int> body)
        {
            var settings = ParseSettings<AccountSettingEnums>(body);
            var input = ApplyAccountSettings(settings);
            var validation = services.GetRequiredService<IValidator<AccountSettingInput>>().Validate(input);
            if (!validation.IsValid) return Fail(string.Join(Environment.NewLine, validation.Errors.Select(x => x.ErrorMessage)));

            var accountId = new AccountId(id);
            using var context = Context(services);
            var wasGold = context.BooleanByName(accountId, AccountSettingEnums.Gold);
            var saved = input.Get();
            context.SaveAccountSetting(accountId, saved);
            context.TriggerTask(services.GetRequiredService<TaskManager>(), accountId, saved);
            var isGold = saved.GetValueOrDefault(AccountSettingEnums.Gold) == 1;
            if (wasGold != isGold) SwitchFarmMode(services, id, isGold);
            return Ok("Ajustes guardados.");
        }

        private static IResult GetVillages(IServiceProvider services, int id)
        {
            using var context = Context(services);
            var villages = context.Villages.Where(x => x.AccountId == id).OrderBy(x => x.Name)
                .Select(x => new { id = x.Id, name = x.Name, x = x.X, y = x.Y })
                .ToList();
            return Ok("", villages);
        }

        private static IResult ReloadVillage(IServiceProvider services, int id, int villageId)
        {
            services.GetRequiredService<TaskManager>().AddOrUpdate<UpdateBuildingTask.Task>(new(new AccountId(id), new VillageId(villageId)));
            return Ok("Se agregó la actualización de esta aldea.");
        }

        private static IResult ReloadVillages(IServiceProvider services, int id, bool all)
        {
            using var context = Context(services);
            var query = context.Villages.Where(x => x.AccountId == id);
            if (!all) query = query.Where(x => x.Buildings.Count < 40);
            var villages = query.Select(x => x.Id).ToList();
            var tasks = services.GetRequiredService<TaskManager>();
            foreach (var villageId in villages)
            {
                tasks.AddOrUpdate<UpdateBuildingTask.Task>(new(new AccountId(id), new VillageId(villageId)));
            }

            return Ok(all ? "Se agregó la actualización de todas las aldeas." : "Se agregó la actualización de las aldeas incompletas.");
        }

        private static IResult GetBuild(IServiceProvider services, int accountId, int villageId)
        {
            if (!OwnsVillage(services, accountId, villageId)) return Fail("La aldea no pertenece a esta cuenta.");
            using var context = Context(services);
            var id = new VillageId(villageId);
            var buildings = context.GetLayoutBuildings(id).Select(building => new
            {
                id = building.Id.Value,
                location = building.Location,
                type = building.Type.ToString(),
                name = building.Type.Humanize(),
                level = building.CurrentLevel,
                queueLevel = building.QueueLevel,
                jobLevel = building.JobLevel,
            }).ToList();
            var queue = context.QueueBuildings.Where(x => x.VillageId == villageId).OrderBy(x => x.CompleteTime)
                .AsEnumerable()
                .Select(x => new { id = x.Id, text = $"{x.Type.Humanize()} al nivel {x.Level} · {x.CompleteTime}" })
                .ToList();
            var jobs = context.Jobs.Where(x => x.VillageId == villageId).OrderBy(x => x.Position).ToDto()
                .AsEnumerable()
                .Select(x => new { id = x.Id.Value, text = x.ToString() })
                .ToList();
            return Ok("", new { buildings, queue, jobs });
        }

        private static IResult GetBuildOptions(IServiceProvider services, int accountId, int villageId, int location)
        {
            if (!OwnsVillage(services, accountId, villageId)) return Fail("La aldea no pertenece a esta cuenta.");
            using var context = Context(services);
            var buildings = context.GetLayoutBuildings(new VillageId(villageId));
            var current = buildings.FirstOrDefault(x => x.Location == location);
            IEnumerable<BuildingEnums> types;
            if (current is not null && current.Type != BuildingEnums.Site)
            {
                types = [current.Type];
            }
            else
            {
                var taken = buildings.Select(x => x.Type).Where(x => !MultipleBuildings.Contains(x)).ToHashSet();
                types = AvailableBuildings.Where(x => !taken.Contains(x));
            }

            return Ok("", types.Select(x => new { type = x.ToString(), name = x.Humanize(), maxLevel = x.GetMaxLevel() }));
        }

        private static IResult AddNormalJob(IServiceProvider services, int accountId, int villageId, NormalJobBody body)
        {
            var paused = EnsurePaused(services, accountId);
            if (paused is not null) return paused;
            if (body.Level <= 0) return Fail("El nivel tiene que ser mayor que 0.");
            if (!Enum.TryParse<BuildingEnums>(body.Type, true, out var type)) return Fail("Edificio desconocido.");
            if (!OwnsVillage(services, accountId, villageId)) return Fail("La aldea no pertenece a esta cuenta.");

            var plan = new NormalBuildPlan { Location = body.Location, Type = type, Level = body.Level };
            using var context = Context(services);
            var id = new VillageId(villageId);
            var buildings = context.GetLayoutBuildings(id);
            var building = buildings.Find(x => x.Location == plan.Location);
            if (building is null || building.Type == BuildingEnums.Site)
            {
                var check = plan.Type.CheckRequirements(buildings);
                if (check.IsFailed) return Fail(string.Join(Environment.NewLine, check.Errors.Select(x => x.Message)));
                plan.FixLocation(buildings);
            }

            context.AddJob(id, plan);
            ScheduleUpgrade(services, accountId, villageId);
            return Ok("Trabajo agregado.");
        }

        private static IResult AddResourceJob(IServiceProvider services, int accountId, int villageId, ResourceJobBody body)
        {
            var paused = EnsurePaused(services, accountId);
            if (paused is not null) return paused;
            if (!Enum.TryParse<ResourcePlanEnums>(body.Plan, true, out var plan)) return Fail("Plan desconocido.");
            if (!OwnsVillage(services, accountId, villageId)) return Fail("La aldea no pertenece a esta cuenta.");

            var input = new ResourceBuildInput { Level = body.Level };
            input.SelectedPlan = input.Plans.First(x => x.Content == plan);
            var validation = services.GetRequiredService<IValidator<ResourceBuildInput>>().Validate(input);
            if (!validation.IsValid) return Fail(string.Join(Environment.NewLine, validation.Errors.Select(x => x.ErrorMessage)));

            using var context = Context(services);
            context.AddJob(new VillageId(villageId), input);
            ScheduleUpgrade(services, accountId, villageId);
            return Ok("Trabajo de recursos agregado.");
        }

        private static IResult UpgradeBuilding(IServiceProvider services, int accountId, int villageId, UpgradeBody body)
        {
            var paused = EnsurePaused(services, accountId);
            if (paused is not null) return paused;
            if (!OwnsVillage(services, accountId, villageId)) return Fail("La aldea no pertenece a esta cuenta.");
            using var context = Context(services);
            context.Upgrade(new VillageId(villageId), body.Location, body.Max);
            ScheduleUpgrade(services, accountId, villageId);
            return Ok(body.Max ? "Se agregó la subida al máximo." : "Se agregó un nivel.");
        }

        private static IResult MoveJob(IServiceProvider services, int accountId, int villageId, int jobId, string direction)
        {
            var paused = EnsurePaused(services, accountId);
            if (paused is not null) return paused;
            if (!Enum.TryParse<MoveEnums>(direction, true, out var move)) return Fail("Movimiento desconocido.");
            using var context = Context(services);
            var id = new JobId(jobId);
            if (move is MoveEnums.Up or MoveEnums.Down) context.SwapJob(id, move);
            else context.MoveJob(id, move);
            ScheduleUpgrade(services, accountId, villageId);
            return Ok("Orden actualizado.");
        }

        private static IResult DeleteJob(IServiceProvider services, int accountId, int villageId, int jobId)
        {
            var paused = EnsurePaused(services, accountId);
            if (paused is not null) return paused;
            using var context = Context(services);
            context.DeleteJobById(new JobId(jobId));
            ScheduleUpgrade(services, accountId, villageId);
            return Ok("Trabajo borrado.");
        }

        private static IResult DeleteJobs(IServiceProvider services, int accountId, int villageId)
        {
            var paused = EnsurePaused(services, accountId);
            if (paused is not null) return paused;
            using var context = Context(services);
            context.Jobs.Where(x => x.VillageId == villageId).ExecuteDelete();
            ScheduleUpgrade(services, accountId, villageId);
            return Ok("Se borraron los trabajos.");
        }

        private static IResult ImportJobs(IServiceProvider services, int accountId, int villageId, ImportJobsBody body)
        {
            var paused = EnsurePaused(services, accountId);
            if (paused is not null) return paused;
            if (!OwnsVillage(services, accountId, villageId)) return Fail("La aldea no pertenece a esta cuenta.");
            var jobs = new List<JobDto>();
            foreach (var job in body.Jobs ?? [])
            {
                if (!Enum.TryParse<JobTypeEnums>(job.Type, true, out var type)) return Fail("Hay un trabajo con un tipo desconocido.");
                jobs.Add(new JobDto { Type = type, Content = job.Content ?? "" });
            }

            if (jobs.Count == 0) return Fail("El archivo no tiene trabajos.");
            using var context = Context(services);
            var id = new VillageId(villageId);
            var fixedJobs = context.FixJobs(id, jobs, body.Shuffle);
            var count = context.Jobs.Count(x => x.VillageId == villageId);
            var addition = fixedJobs.Select((job, index) => new Job
            {
                Position = count + index,
                VillageId = villageId,
                Type = job.Type,
                Content = job.Content,
            }).ToList();
            context.AddRange(addition);
            context.SaveChanges();
            ScheduleUpgrade(services, accountId, villageId);
            return Ok($"Se importaron {addition.Count} trabajos.");
        }

        private static IResult ExportJobs(IServiceProvider services, int accountId, int villageId)
        {
            if (!OwnsVillage(services, accountId, villageId)) return Fail("La aldea no pertenece a esta cuenta.");
            var paused = EnsurePaused(services, accountId);
            if (paused is not null) return paused;
            using var context = Context(services);
            var jobs = context.Jobs.Where(x => x.VillageId == villageId).OrderBy(x => x.Position).ToDto()
                .Select(x => new { type = x.Type.ToString(), content = x.Content })
                .ToList();
            return Ok("", jobs);
        }

        private static IResult GetVillageSettings(IServiceProvider services, int accountId, int villageId)
        {
            if (!OwnsVillage(services, accountId, villageId)) return Fail("La aldea no pertenece a esta cuenta.");
            using var context = Context(services);
            var input = ReadVillageSettings(context, villageId);
            return Ok("", input.Get().ToDictionary(x => x.Key.ToString(), x => x.Value));
        }

        private static IResult SaveVillageSettings(IServiceProvider services, int accountId, int villageId, Dictionary<string, int> body)
        {
            if (!OwnsVillage(services, accountId, villageId)) return Fail("La aldea no pertenece a esta cuenta.");
            var input = ApplyVillageSettings(ParseSettings<VillageSettingEnums>(body));
            var validation = services.GetRequiredService<IValidator<VillageSettingInput>>().Validate(input);
            if (!validation.IsValid) return Fail(string.Join(Environment.NewLine, validation.Errors.Select(x => x.ErrorMessage)));

            var saved = input.Get();
            using var context = Context(services);
            var village = new VillageId(villageId);
            context.SaveVillageSetting(village, saved);
            context.TriggerTask(services.GetRequiredService<TaskManager>(), new AccountId(accountId), village, saved);
            return Ok("Ajustes de la aldea guardados.");
        }

        private static IResult GetFarming(IServiceProvider services, int accountId, int villageId)
        {
            using var context = Context(services);
            var villages = context.Villages.Where(x => x.AccountId == accountId).OrderBy(x => x.Name)
                .Select(x => new { id = x.Id, name = x.Name, x = x.X, y = x.Y }).ToList();
            var raidTasks = services.GetRequiredService<TaskManager>().GetTaskList(new AccountId(accountId)).OfType<RaidCowListTask.Task>().ToList();
            var lists = context.CowLists.Where(x => x.AccountId == accountId && x.VillageId == villageId).OrderBy(x => x.Name)
                .Select(x => new { x.Id, x.Name, x.MinAttackMinutes, x.RepeatMinutes, x.LevelHero, x.AutoStart })
                .AsEnumerable()
                .Select(x =>
                {
                    var raid = raidTasks.FirstOrDefault(task => task.CowListId == x.Id);
                    return new
                    {
                        id = x.Id,
                        name = x.Name,
                        minMinutes = x.MinAttackMinutes,
                        repeatMinutes = x.RepeatMinutes,
                        levelHero = x.LevelHero,
                        autoStart = x.AutoStart,
                        running = raid is not null,
                        countdown = FormatCountdown(raid),
                    };
                })
                .ToList();
            var listIds = lists.Select(x => x.id).ToList();
            var home = context.Villages.Where(x => x.Id == villageId).Select(x => new { x.X, x.Y }).FirstOrDefault();
            var cows = context.CowVillages.Where(x => listIds.Contains(x.CowListId)).OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.CowListId, x.Name, x.Url, x.TroopCount, x.TroopPlan, x.LastAttackedAt, x.LastSkipReason, x.Kind, x.Owner, x.Tribe, x.Alliance, x.Population, x.Distance, x.CoordX, x.CoordY })
                .AsEnumerable()
                .Select(x =>
                {
                    var coordX = x.CoordX;
                    var coordY = x.CoordY;
                    if (coordX is null || coordY is null)
                    {
                        if (CowOasisParser.TryReadCoordinates(x.Url, out var parsedX, out var parsedY))
                        {
                            coordX = parsedX;
                            coordY = parsedY;
                        }
                    }

                    var distance = x.Distance;
                    if (distance < 0 && coordX is int targetX && coordY is int targetY && home is not null)
                        distance = CowOasisParser.TravelDistance(home.X, home.Y, targetX, targetY);
                    return new
                    {
                        id = x.Id,
                        listId = x.CowListId,
                        name = VillageLabel(x.Name),
                        url = x.Url,
                        troops = CowTroopPlan.Describe(x.TroopPlan, x.TroopCount),
                        status = FormatStatus(x.LastAttackedAt, x.LastSkipReason),
                        kind = x.Kind ?? "",
                        owner = x.Owner ?? "",
                        tribe = x.Tribe ?? "",
                        alliance = x.Alliance ?? "",
                        population = x.Population,
                        distance,
                        x = coordX,
                        y = coordY,
                    };
                }).ToList();
            var account = new AccountId(accountId);
            var gold = context.BooleanByName(account, AccountSettingEnums.Gold);
            var gameLists = context.FarmLists.Where(x => x.AccountId == accountId).OrderBy(x => x.Name)
                .Select(x => new { id = x.Id, name = x.Name, active = x.IsActive })
                .ToList();
            var interval = context.AccountsSetting
                .Where(x => x.AccountId == accountId && (x.Setting == AccountSettingEnums.FarmIntervalMin || x.Setting == AccountSettingEnums.FarmIntervalMax))
                .ToList();
            var gameTask = services.GetRequiredService<TaskManager>().GetTaskList(account).OfType<StartFarmListTask.Task>().FirstOrDefault();
            return Ok("", new
            {
                villages,
                lists,
                cows,
                countdown = "",
                gold,
                gameLists,
                useStartAll = context.BooleanByName(account, AccountSettingEnums.UseStartAllButton),
                farmIntervalMin = interval.FirstOrDefault(x => x.Setting == AccountSettingEnums.FarmIntervalMin)?.Value ?? 540,
                farmIntervalMax = interval.FirstOrDefault(x => x.Setting == AccountSettingEnums.FarmIntervalMax)?.Value ?? 660,
                gameRunning = gameTask is not null,
                gameCountdown = FormatCountdown(gameTask),
            });
        }

        private static IResult CreateCowList(IServiceProvider services, int accountId, CowListBody body)
        {
            var name = (body.Name ?? "").Trim();
            if (name.Length == 0) return Fail("Escribí un nombre para la lista.");
            if (!OwnsVillage(services, accountId, body.VillageId)) return Fail("Seleccioná una aldea. La lista queda asociada a esa aldea.");
            using var context = Context(services);
            if (context.CowLists.Any(x => x.AccountId == accountId && x.VillageId == body.VillageId && x.Name == name))
                return Fail("Ya existe una lista con ese nombre en esta aldea.");

            context.CowLists.Add(new CowList { AccountId = accountId, VillageId = body.VillageId, Name = name });
            context.SaveChanges();
            return Ok("Lista creada.");
        }

        private static IResult UpdateCowList(IServiceProvider services, int accountId, int listId, CowListBody body)
        {
            var name = (body.Name ?? "").Trim();
            if (name.Length == 0) return Fail("Escribí un nombre para la lista.");
            if (body.MinMinutes < 0 || body.RepeatMinutes < 0) return Fail("Los minutos tienen que ser 0 o más.");
            if (body.AutoStart && (body.MinMinutes < 1 || body.RepeatMinutes < 1))
                return Fail("Para el inicio automático, el mínimo por aldea y la revisión tienen que ser de 1 minuto o más.");
            using var context = Context(services);
            var current = context.CowLists.Where(x => x.Id == listId && x.AccountId == accountId)
                .Select(x => new { x.VillageId, x.AutoStart })
                .FirstOrDefault();
            if (current is null) return Fail("No se encontró la lista.");
            var villageId = current.VillageId;
            if (context.CowLists.Any(x => x.AccountId == accountId && x.VillageId == villageId && x.Id != listId && x.Name == name))
                return Fail("Ya existe una lista con ese nombre en esta aldea.");
            if (body.AutoStart && !context.CowVillages.Any(x => x.CowListId == listId))
                return Fail("La lista no tiene aldeas.");

            context.CowLists.Where(x => x.Id == listId).ExecuteUpdate(x => x
                .SetProperty(list => list.Name, name)
                .SetProperty(list => list.MinAttackMinutes, body.MinMinutes)
                .SetProperty(list => list.RepeatMinutes, body.RepeatMinutes)
                .SetProperty(list => list.AutoStart, body.AutoStart));
            // Only when the client sends it, so older calls (rename) keep the current value.
            if (body.LevelHero is bool levelHero)
                context.CowLists.Where(x => x.Id == listId).ExecuteUpdate(x => x.SetProperty(list => list.LevelHero, levelHero));
            if (!body.AutoStart || current.AutoStart) return Ok("Lista guardada.");

            const string message = "Inicio automático activado.";
            var account = new AccountId(accountId);
            var tasks = services.GetRequiredService<TaskManager>();
            var status = tasks.GetStatus(account);
            if (status is not (StatusEnums.Online or StatusEnums.Paused))
                return Ok(message + " Arranca sola al conectar la cuenta.");
            if (tasks.GetTaskList(account).OfType<RaidCowListTask.Task>().Any(x => x.CowListId == listId))
                return Ok(message);

            var queued = tasks.GetTaskList(account).OfType<RaidCowListTask.Task>().Any();
            tasks.Add(new RaidCowListTask.Task(account, listId));
            return Ok(queued
                ? message + " Empieza cuando termine la lista que está en curso."
                : message + " El raid empezó.");
        }

        private static IResult DeleteCowList(IServiceProvider services, int accountId, int listId)
        {
            using var context = Context(services);
            context.CowVillages.Where(x => x.CowListId == listId && x.CowList.AccountId == accountId).ExecuteDelete();
            var deleted = context.CowLists.Where(x => x.Id == listId && x.AccountId == accountId).ExecuteDelete();
            if (deleted == 0) return Fail("No se encontró la lista.");
            StopListRaid(services, accountId, listId);
            return Ok("Lista borrada.");
        }

        private static async Task<IResult> AddCowVillage(IServiceProvider services, int accountId, int listId, CowVillageBody body)
        {
            if (!TryParseCow(body.Url, body.Troops, out var url, out var plan, out var count, out var error)) return Fail(error);
            using (var context = Context(services))
            {
                if (!context.CowLists.Any(x => x.Id == listId && x.AccountId == accountId)) return Fail("No se encontró la lista.");
            }

            var (originX, originY) = ListOrigin(services, listId);
            var details = await ReadTileDetails(services, accountId, url);
            var row = new CowVillage { CowListId = listId, Name = CowOasisParser.MissingName, Url = url, TroopPlan = plan, TroopCount = count };
            CowOasisParser.CopyOnto(row, details, url, originX, originY, replaceName: true);
            using (var context = Context(services))
            {
                if (!context.CowLists.Any(x => x.Id == listId && x.AccountId == accountId)) return Fail("No se encontró la lista.");
                context.CowVillages.Add(row);
                context.SaveChanges();
            }

            return Ok(row.Name == CowOasisParser.MissingName ? "Aldea agregada sin nombre." : $"Aldea agregada: {row.Name}.");
        }

        private static IResult BulkEditCowList(IServiceProvider services, int accountId, int listId, string text)
        {
            if (!TryParseCowLines(text, out var lines, out var error)) return Fail(error);

            using var context = Context(services);
            if (!context.CowLists.Any(x => x.Id == listId && x.AccountId == accountId)) return Fail("No se encontró la lista.");

            var existing = context.CowVillages.Where(x => x.CowListId == listId).ToList();
            var byUrl = new Dictionary<string, CowVillage>(StringComparer.OrdinalIgnoreCase);
            foreach (var village in existing)
                byUrl[village.Url] = village;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var updated = 0;
            var added = 0;
            foreach (var line in lines)
            {
                seen.Add(line.Url);
                if (!byUrl.TryGetValue(line.Url, out var village))
                {
                    context.CowVillages.Add(new CowVillage
                    {
                        CowListId = listId,
                        Name = line.HasName ? line.Name : CowOasisParser.MissingName,
                        Url = line.Url,
                        TroopPlan = line.Plan,
                        TroopCount = line.Count,
                    });
                    added++;
                    continue;
                }

                var name = line.HasName ? line.Name : village.Name;
                if (village.Name == name && village.TroopPlan == line.Plan && village.TroopCount == line.Count) continue;
                village.Name = name;
                village.TroopPlan = line.Plan;
                village.TroopCount = line.Count;
                updated++;
            }

            var removed = 0;
            foreach (var village in existing)
            {
                if (seen.Contains(village.Url)) continue;
                context.CowVillages.Remove(village);
                removed++;
            }

            context.SaveChanges();
            if (updated == 0 && added == 0 && removed == 0) return Ok("La lista ya estaba así.");
            return Ok($"Se actualizó la lista. Modificadas: {updated}. Nuevas: {added}. Borradas: {removed}.");
        }

        private static IResult UpdateCowVillage(IServiceProvider services, int accountId, int cowId, CowVillageBody body)
        {
            if (!TryParseCow(body.Url, body.Troops, out var url, out var plan, out var count, out var error)) return Fail(error);
            var name = VillageLabel(body.Name);
            using var context = Context(services);
            var updated = context.CowVillages.Where(x => x.Id == cowId && x.CowList.AccountId == accountId)
                .ExecuteUpdate(x => x
                    .SetProperty(village => village.Name, name)
                    .SetProperty(village => village.Url, url)
                    .SetProperty(village => village.TroopPlan, plan)
                    .SetProperty(village => village.TroopCount, count));
            if (updated == 0) return Fail("No se encontró la aldea.");
            return Ok("Aldea guardada.");
        }

        private static (int? X, int? Y) ListOrigin(IServiceProvider services, int listId)
        {
            using var context = Context(services);
            var origin = context.CowLists.Where(x => x.Id == listId)
                .Join(context.Villages, list => list.VillageId, village => village.Id, (list, village) => new { village.X, village.Y })
                .FirstOrDefault();
            return origin is null ? (null, null) : (origin.X, origin.Y);
        }

        private static async Task<CowOasisParser.TileDetails> ReadTileDetails(IServiceProvider services, int accountId, string url, CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var browser = services.GetRequiredService<ChromeManager>().Get(new AccountId(accountId));
                if (!browser.IsInitialized) return new CowOasisParser.TileDetails("", "", "", "", "", -1, -1, false);
                var details = await browser.InNewTab(async page =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await page.GotoAsync(url, new() { Timeout = 20_000 });
                    cancellationToken.ThrowIfCancellationRequested();
                    await CowOasisParser.ReadTile(page);
                    return await CowOasisParser.ReadDetails(page);
                });
                if (browser.IsInitialized) await browser.CurrentPage.BringToFrontAsync();
                return details;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return new CowOasisParser.TileDetails("", "", "", "", "", -1, -1, false);
            }
        }

        private static string VillageLabel(string name)
        {
            var text = (name ?? "").Trim();
            return text.Length == 0 ? CowOasisParser.MissingName : text;
        }

        private static async Task<IResult> SyncVillageNames(IServiceProvider services, int accountId, int listId, CancellationToken cancellationToken)
        {
            List<(int Id, string Url)> villages;
            using (var context = Context(services))
            {
                if (!context.CowLists.Any(x => x.Id == listId && x.AccountId == accountId)) return Fail("No se encontró la lista.");
                villages = context.CowVillages
                    .Where(x => x.CowListId == listId && x.CowList.AccountId == accountId)
                    .OrderBy(x => x.Id)
                    .Select(x => new { x.Id, x.Url })
                    .AsEnumerable()
                    .Select(x => (x.Id, x.Url))
                    .ToList();
            }

            if (villages.Count == 0) return Fail("La lista no tiene aldeas.");
            var account = new AccountId(accountId);
            if (services.GetRequiredService<TaskManager>().IsExist<RaidCowListTask.Task>(account)) return Fail("Detené el raid antes de sincronizar los nombres.");
            var browser = services.GetRequiredService<ChromeManager>().Get(account);
            if (!browser.IsInitialized) return Fail("La cuenta tiene que estar conectada para abrir el navegador.");

            var named = 0;
            var visited = 0;
            var (originX, originY) = ListOrigin(services, listId);
            try
            {
                for (var index = 0; index < villages.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (index > 0) await Task.Delay(NextNavigationDelay(), cancellationToken);
                    var (id, url) = villages[index];
                    var details = await ReadTileDetails(services, accountId, url, cancellationToken);
                    visited++;
                    using var context = Context(services);
                    var row = context.CowVillages.FirstOrDefault(x => x.Id == id && x.CowList.AccountId == accountId);
                    if (row is null) continue;
                    CowOasisParser.CopyOnto(row, details, url, originX, originY, replaceName: true);
                    if (row.Name != CowOasisParser.MissingName) named++;
                    context.SaveChanges();
                }
            }
            catch (OperationCanceledException)
            {
                return Ok(visited == 0 ? "Sync detenido." : $"Sync detenido. Se actualizaron {visited} aldeas.");
            }

            return Ok(named == villages.Count
                ? $"Se actualizaron los nombres de {villages.Count} aldeas."
                : $"Se recorrieron {villages.Count} aldeas. {named} tienen nombre y {villages.Count - named} quedaron sin nombre.");
        }

        private static TimeSpan NextNavigationDelay()
        {
            const double minimum = 1.5;
            const double maximum = 2.5;
            const double mean = (minimum + maximum) / 2.0;
            const double stdDev = (maximum - minimum) / 6.0;

            var u1 = 1.0 - Random.Shared.NextDouble();
            var u2 = 1.0 - Random.Shared.NextDouble();
            var standardNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            var seconds = Math.Clamp(mean + (stdDev * standardNormal), minimum, maximum);
            return TimeSpan.FromSeconds(seconds);
        }

        private static IResult DeleteCowVillage(IServiceProvider services, int accountId, int cowId)
        {
            using var context = Context(services);
            var deleted = context.CowVillages.Where(x => x.Id == cowId && x.CowList.AccountId == accountId).ExecuteDelete();
            if (deleted == 0) return Fail("No se encontró la aldea.");
            return Ok("Aldea borrada.");
        }

        private static IResult RefreshGameFarms(IServiceProvider services, int accountId)
        {
            if (!IsGold(services, accountId)) return Fail("Esta cuenta no está marcada como Gold.");
            services.GetRequiredService<TaskManager>().AddOrUpdate(new UpdateFarmListTask.Task(new AccountId(accountId)));
            return Ok("Se van a leer las listas de vacas del juego.");
        }

        private static IResult StartGameFarms(IServiceProvider services, int accountId)
        {
            if (!IsGold(services, accountId)) return Fail("Esta cuenta no está marcada como Gold.");
            var account = new AccountId(accountId);
            using var context = Context(services);
            if (!context.BooleanByName(account, AccountSettingEnums.UseStartAllButton) && !context.FarmLists.Any(x => x.AccountId == accountId && x.IsActive))
                return Fail("No hay ninguna lista activa. Activa una, o encendé Iniciar todas.");
            services.GetRequiredService<TaskManager>().AddOrUpdate(new StartFarmListTask.Task(account));
            return Ok("Empieza a iniciar las listas del juego.");
        }

        private static IResult StopGameFarms(IServiceProvider services, int accountId)
        {
            var account = new AccountId(accountId);
            var tasks = services.GetRequiredService<TaskManager>();
            if (tasks.GetCurrentTask(account) is StartFarmListTask.Task)
                tasks.CancelIfCurrent<StartFarmListTask.Task>(account);
            tasks.Remove<StartFarmListTask.Task>(account);
            return Ok("Se detuvo el inicio de las listas del juego.");
        }

        private static IResult ToggleGameFarm(IServiceProvider services, int accountId, int farmId)
        {
            using var context = Context(services);
            var updated = context.FarmLists.Where(x => x.Id == farmId && x.AccountId == accountId)
                .ExecuteUpdate(x => x.SetProperty(farm => farm.IsActive, farm => !farm.IsActive));
            if (updated == 0) return Fail("No se encontró la lista del juego.");
            return Ok("Lista actualizada.");
        }

        private static IResult SaveGameFarmOptions(IServiceProvider services, int accountId, GameFarmBody body)
        {
            if (body.IntervalMin < 0 || body.IntervalMax < body.IntervalMin) return Fail("El intervalo mínimo tiene que ser menor o igual que el máximo.");
            using var context = Context(services);
            if (!context.Accounts.Any(x => x.Id == accountId)) return Fail("No se encontró la cuenta.");
            WriteSetting(context, accountId, AccountSettingEnums.UseStartAllButton, body.UseStartAll ? 1 : 0);
            WriteSetting(context, accountId, AccountSettingEnums.FarmIntervalMin, body.IntervalMin);
            WriteSetting(context, accountId, AccountSettingEnums.FarmIntervalMax, body.IntervalMax);
            return Ok("Opciones de las listas del juego guardadas.");
        }

        private static bool IsGold(IServiceProvider services, int accountId)
        {
            using var context = Context(services);
            return context.BooleanByName(new AccountId(accountId), AccountSettingEnums.Gold);
        }

        private static void SwitchFarmMode(IServiceProvider services, int accountId, bool gold)
        {
            var account = new AccountId(accountId);
            var tasks = services.GetRequiredService<TaskManager>();
            if (gold)
            {
                foreach (var raid in tasks.GetTaskList(account).OfType<RaidCowListTask.Task>().ToList())
                {
                    raid.KeepRepeating = false;
                    if (ReferenceEquals(tasks.GetCurrentTask(account), raid))
                        tasks.CancelIfCurrent<RaidCowListTask.Task>(account);
                    else
                        tasks.Remove(account, raid);
                }
                return;
            }

            if (tasks.GetCurrentTask(account) is StartFarmListTask.Task)
                tasks.CancelIfCurrent<StartFarmListTask.Task>(account);
            tasks.Remove<StartFarmListTask.Task>(account);
        }

        private static void WriteSetting(AppDbContext context, int accountId, AccountSettingEnums setting, int value)
        {
            var updated = context.AccountsSetting.Where(x => x.AccountId == accountId && x.Setting == setting)
                .ExecuteUpdate(x => x.SetProperty(row => row.Value, value));
            if (updated > 0) return;
            context.AccountsSetting.Add(new AccountSetting { AccountId = accountId, Setting = setting, Value = value });
            context.SaveChanges();
        }

        private static IResult StartCowList(IServiceProvider services, int accountId, int listId, CowListBody body)
        {
            if (IsGold(services, accountId)) return Fail("Esta cuenta es Gold. Farming usa las listas del juego.");
            if (body.MinMinutes < 1 || body.RepeatMinutes < 1) return Fail("Para iniciar, el mínimo por aldea y la revisión de la lista tienen que ser de 1 minuto o más.");
            var account = new AccountId(accountId);
            var tasks = services.GetRequiredService<TaskManager>();
            if (tasks.GetTaskList(account).OfType<RaidCowListTask.Task>().Any(x => x.CowListId == listId))
                return Fail("Esa lista ya está en curso.");

            using var context = Context(services);
            var updated = context.CowLists.Where(x => x.Id == listId && x.AccountId == accountId)
                .ExecuteUpdate(x => x
                    .SetProperty(list => list.MinAttackMinutes, body.MinMinutes)
                    .SetProperty(list => list.RepeatMinutes, body.RepeatMinutes));
            if (updated == 0) return Fail("No se encontró la lista.");
            if (!context.CowVillages.Any(x => x.CowListId == listId)) return Fail("La lista no tiene aldeas.");

            var queued = tasks.GetTaskList(account).OfType<RaidCowListTask.Task>().Any();
            tasks.Add(new RaidCowListTask.Task(account, listId));
            var when = queued
                ? "Empieza cuando termine la lista que está en curso."
                : "Empieza la revisión ahora.";
            return Ok($"{when} Mínimo por aldea: {body.MinMinutes} minutos. La próxima revisión es {body.RepeatMinutes} minutos después de que termine esta.");
        }

        private static IResult StopCowList(IServiceProvider services, int accountId, int listId)
        {
            if (!StopListRaid(services, accountId, listId)) return Fail("Esa lista no está en curso.");
            return Ok("Se detuvo esta lista. Las otras siguen.");
        }

        private static bool StopListRaid(IServiceProvider services, int accountId, int listId)
        {
            var account = new AccountId(accountId);
            var tasks = services.GetRequiredService<TaskManager>();
            var running = tasks.GetTaskList(account).OfType<RaidCowListTask.Task>().FirstOrDefault(x => x.CowListId == listId);
            if (running is null) return false;

            running.KeepRepeating = false;
            if (ReferenceEquals(tasks.GetCurrentTask(account), running))
                tasks.CancelIfCurrent<RaidCowListTask.Task>(account);
            else
                tasks.Remove(account, running);

            using var context = Context(services);
            context.CowVillages
                .Where(x => x.CowListId == listId && x.CowList.AccountId == accountId)
                .Where(x => x.LastSkipReason == "Analizando" || x.LastSkipReason == "Atacando")
                .ExecuteUpdate(x => x.SetProperty(village => village.LastSkipReason, ""));
            return true;
        }

        private static IResult GetHero(IServiceProvider services, int accountId)
        {
            var account = new AccountId(accountId);
            using var context = Context(services);
            var row = HeroLevelingStore.Get(context, account);
            var heroLists = context.CowLists.Where(x => x.AccountId == accountId && x.LevelHero).OrderBy(x => x.Name).Select(x => x.Name).ToList();

            return Ok("", new
            {
                state = row?.State ?? "",
                message = row?.Message ?? "",
                level = row?.Level,
                hpPercent = row?.HpPercent,
                experience = row?.Experience,
                updatedAt = row?.UpdatedAt?.ToString("yyyy-MM-dd HH:mm"),
                statsAt = row?.StatsAt?.ToString("yyyy-MM-dd HH:mm"),
                heroLists,
                minHpPercent = HeroLevelingRules.MinHpPercent,
                minHealthLeft = HeroLevelingRules.MinHealthLeftAfterFight,
            });
        }

        private static async Task<IResult> SearchOasis(IServiceProvider services, int accountId, int listId, SearchBody body)
        {
            if (!CowTroopPlan.TryParse(body.Troops ?? "", out var slots, out var troopError)) return Fail(troopError);
            if (!TryParseDistance(body.MinDistance, out var minDistance, allowZero: true)) return Fail("La distancia mínima tiene que ser un número de 0 o más.");
            if (!TryParseDistance(body.Distance ?? "", out var maxDistance)) return Fail("La distancia máxima tiene que ser un número mayor que 0.");
            if (minDistance > maxDistance) return Fail("La distancia mínima no puede ser mayor que la máxima.");

            string server;
            int originX;
            int originY;
            string villageName;
            using (var context = Context(services))
            {
                var list = context.CowLists.Where(x => x.Id == listId && x.AccountId == accountId).Select(x => new { x.VillageId }).FirstOrDefault();
                if (list is null) return Fail("No se encontró la lista.");
                var village = context.Villages.Where(x => x.Id == list.VillageId && x.AccountId == accountId).Select(x => new { x.Name, x.X, x.Y }).FirstOrDefault();
                if (village is null) return Fail("La lista no tiene una aldea asociada. Creala de nuevo con una aldea seleccionada.");
                server = context.Accounts.Where(x => x.Id == accountId).Select(x => x.Server).First();
                originX = village.X;
                originY = village.Y;
                villageName = village.Name;
            }

            var browser = services.GetRequiredService<ChromeManager>().Get(new AccountId(accountId));
            if (!browser.IsInitialized) return Fail("La cuenta tiene que estar conectada para abrir el navegador.");

            List<OasisHit> found;
            try
            {
                var catalogUrl = OasisCatalog.BuildCatalogUrl(server, originX, originY);
                found = await browser.InNewTab(page => OasisCatalog.Collect(page, catalogUrl, originX, originY, minDistance, maxDistance));
            }
            catch (Exception ex)
            {
                return Fail("No se pudo leer la lista de oasis. " + ex.Message);
            }

            var range = minDistance > 0
                ? $"entre {minDistance.ToString("0.##", CultureInfo.CurrentCulture)} y {maxDistance.ToString("0.##", CultureInfo.CurrentCulture)} casillas"
                : $"a {maxDistance.ToString("0.##", CultureInfo.CurrentCulture)} casillas o menos";
            return Ok(found.Count == 0 ? $"No se encontraron oasis {range}." : $"Se encontraron {found.Count} oasis {range}.", new
            {
                count = found.Count,
                villageName,
                originX,
                originY,
                troopPlan = CowTroopPlan.Format(slots),
                hits = found.Select(x => new { x = x.X, y = x.Y, distance = x.Distance }),
            });
        }

        private static IResult ApplyOasis(IServiceProvider services, int accountId, int listId, ApplySearchBody body)
        {
            if (!CowTroopPlan.TryParse(body.TroopPlan ?? "", out var slots, out var error)) return Fail(error);
            var plan = CowTroopPlan.Format(slots);
            var count = slots.Sum(slot => slot.Amount);
            using var context = Context(services);
            var server = context.Accounts.Where(x => x.Id == accountId).Select(x => x.Server).FirstOrDefault();
            if (server is null || !context.CowLists.Any(x => x.Id == listId && x.AccountId == accountId)) return Fail("No se encontró la lista.");

            var origin = context.CowLists.Where(x => x.Id == listId)
                .Join(context.Villages, list => list.VillageId, village => village.Id, (list, village) => new { village.X, village.Y })
                .FirstOrDefault();
            var existing = context.CowVillages.Where(x => x.CowListId == listId).Select(x => x.Url).ToList();
            var urls = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
            var added = 0;
            foreach (var hit in body.Hits ?? [])
            {
                var url = OasisCatalog.BuildOasisUrl(server, hit.X, hit.Y);
                if (!urls.Add(url)) continue;
                context.CowVillages.Add(new CowVillage
                {
                    CowListId = listId,
                    Name = CowOasisParser.MissingName,
                    Url = url,
                    TroopPlan = plan,
                    TroopCount = count,
                    Kind = "oasis",
                    CoordX = hit.X,
                    CoordY = hit.Y,
                    Distance = origin is null ? -1 : CowOasisParser.TravelDistance(origin.X, origin.Y, hit.X, hit.Y),
                });
                added++;
            }

            if (added > 0) context.SaveChanges();
            var total = body.Hits?.Count ?? 0;
            return Ok(added == total ? $"Se agregaron {added} oasis." : $"Se agregaron {added} oasis. {total - added} ya estaban en la lista.");
        }

        private static IResult GetDebug(IServiceProvider services, int id)
        {
            var accountId = new AccountId(id);
            var tasks = services.GetRequiredService<TaskManager>().GetTaskList(accountId)
                .OrderBy(x => x.ExecuteAt)
                .Select(x => new { task = x.Description, executeAt = x.ExecuteAt, stage = x.Stage.ToString() })
                .ToList();
            var lines = new List<string>();
            var logs = services.GetRequiredService<LogSink>().GetLogs(accountId);
            try
            {
                for (var node = logs.Last; node is not null; node = node.Previous)
                {
                    lines.Add($"[{node.Value.Timestamp.LocalDateTime:HH:mm:ss} {node.Value.Level}] {node.Value.RenderMessage()}");
                }
            }
            catch (InvalidOperationException)
            {
                // The log list moved while it was being read.
            }

            return Ok("", new { tasks, logs = string.Join(Environment.NewLine, lines) });
        }

        private static async Task<IResult> BringToFront(IServiceProvider services, int id)
        {
            var browser = services.GetRequiredService<ChromeManager>().Get(new AccountId(id));
            if (!browser.IsInitialized || browser.CurrentPage.IsClosed) return Fail("La cuenta no está conectada.");
            if (browser.IsHeadless) return Fail("El navegador está en modo oculto.");
            await browser.CurrentPage.BringToFrontAsync();
            return Ok("Navegador al frente.");
        }

        private static IResult OpenLogs()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            return Ok("Carpeta de logs abierta.");
        }

        private static IResult SetTemplate(IServiceProvider services, string kind, string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return Fail("No se encontró el archivo.");
            var store = services.GetRequiredService<DefaultTemplatePathStore>();
            if (kind == "account") store.SetAccountSettingsPath(path);
            else if (kind == "village") store.SetVillageSettingsPath(path);
            else return Fail("Plantilla desconocida.");
            return Ok("Plantilla guardada.");
        }

        private static IResult ClearTemplate(IServiceProvider services, string kind)
        {
            var store = services.GetRequiredService<DefaultTemplatePathStore>();
            if (kind == "account") store.ClearAccountSettingsPath();
            else if (kind == "village") store.ClearVillageSettingsPath();
            else return Fail("Plantilla desconocida.");
            return Ok("Plantilla borrada.");
        }

        private static async Task<IResult> UploadTemplate(IServiceProvider services, string kind, HttpRequest request)
        {
            if (kind is not ("account" or "village")) return Fail("Plantilla desconocida.");
            using var reader = new StreamReader(request.Body);
            var json = await reader.ReadToEndAsync();
            try
            {
                JsonDocument.Parse(json);
            }
            catch
            {
                return Fail("El archivo no es un JSON válido.");
            }

            var directory = Path.Combine(AppContext.BaseDirectory, "Data", "templates");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, kind + "-default.json");
            await File.WriteAllTextAsync(path, json);
            var store = services.GetRequiredService<DefaultTemplatePathStore>();
            if (kind == "account") store.SetAccountSettingsPath(path);
            else store.SetVillageSettingsPath(path);
            return Ok("Plantilla guardada.");
        }

        private static void InsertAccounts(IServiceProvider services, AppDbContext context, List<AccountDto> dtos)
        {
            var settings = LoadAccountDefaults(services);
            foreach (var dto in dtos)
            {
                var account = dto.ToEntity();
                account.Settings = settings.Select(setting => new AccountSetting { Setting = setting.Key, Value = setting.Value }).ToList();
                context.Add(account);
            }

            context.SaveChanges();
        }

        private static Dictionary<AccountSettingEnums, int> LoadAccountDefaults(IServiceProvider services)
        {
            var settings = AppDbContext.AccountDefaultSettings.ToDictionary(x => x.Key, x => x.Value);
            var path = services.GetRequiredService<DefaultTemplatePathStore>().Get().AccountSettingsPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return settings;
            try
            {
                var imported = JsonSerializer.Deserialize<Dictionary<AccountSettingEnums, int>>(File.ReadAllText(path));
                if (imported is null) return settings;
                foreach (var (setting, value) in imported)
                {
                    if (setting == AccountSettingEnums.Tribe) continue;
                    settings[setting] = value;
                }
            }
            catch
            {
                return settings;
            }

            return settings;
        }

        private static AccountDetailDto ParseLine(string input)
        {
            var parts = input.Trim().Split(' ');
            if (parts.Length is < 3 or > 7) return null;
            if (string.IsNullOrWhiteSpace(parts[0]) || !Uri.TryCreate(parts[0], UriKind.Absolute, out var uri)) return null;
            if (parts.Length > 4 && !int.TryParse(parts[4], out _)) return null;
            var server = $"{uri.Scheme}://{uri.Host}";
            return parts.Length switch
            {
                3 => AccountDetailDto.Create(parts[1], server, parts[2]),
                5 => AccountDetailDto.Create(parts[1], server, parts[2], parts[3], int.Parse(parts[4])),
                7 => AccountDetailDto.Create(parts[1], server, parts[2], parts[3], int.Parse(parts[4]), parts[5], parts[6]),
                _ => null,
            };
        }

        private static AccountInput ToInput(AccountBody body)
        {
            var input = new AccountInput { Username = body.Username ?? "", Server = body.Server ?? "" };
            foreach (var access in body.Accesses ?? [])
            {
                input.Accesses.Add(new AccessInput
                {
                    Id = new AccessId(access.Id),
                    Username = access.Username ?? "",
                    Password = access.Password ?? "",
                    ProxyHost = access.ProxyHost ?? "",
                    ProxyPort = access.ProxyPort,
                    ProxyUsername = access.ProxyUsername ?? "",
                    ProxyPassword = access.ProxyPassword ?? "",
                    Useragent = access.Useragent ?? "",
                });
            }

            return input;
        }

        private static string ValidateAccount(IServiceProvider services, AccountInput input)
        {
            var account = services.GetRequiredService<IValidator<AccountInput>>().Validate(input);
            if (!account.IsValid) return string.Join(Environment.NewLine, account.Errors.Select(x => x.ErrorMessage));
            var accessValidator = services.GetRequiredService<IValidator<AccessInput>>();
            foreach (var access in input.Accesses)
            {
                var result = accessValidator.Validate(access);
                if (!result.IsValid) return string.Join(Environment.NewLine, result.Errors.Select(x => x.ErrorMessage));
            }

            return null;
        }

        private static AccountSettingInput ReadAccountSettings(AppDbContext context, int accountId)
        {
            var settings = AppDbContext.AccountDefaultSettings.ToDictionary(x => x.Key, x => x.Value);
            foreach (var row in context.AccountsSetting.Where(x => x.AccountId == accountId)) settings[row.Setting] = row.Value;
            return ApplyAccountSettings(settings);
        }

        private static AccountSettingInput ApplyAccountSettings(Dictionary<AccountSettingEnums, int> settings)
        {
            var input = new AccountSettingInput();
            var tribe = (TribeEnums)settings.GetValueOrDefault(AccountSettingEnums.Tribe);
            if (!input.Tribe.Items.Any(x => x.Tribe == tribe)) settings[AccountSettingEnums.Tribe] = (int)input.Tribe.Get();
            input.Set(settings);
            return input;
        }

        private static VillageSettingInput ReadVillageSettings(AppDbContext context, int villageId)
        {
            var settings = AppDbContext.VillageDefaultSettings.ToDictionary(x => x.Key, x => x.Value);
            foreach (var row in context.VillagesSetting.Where(x => x.VillageId == villageId)) settings[row.Setting] = row.Value;
            return ApplyVillageSettings(settings);
        }

        private static VillageSettingInput ApplyVillageSettings(Dictionary<VillageSettingEnums, int> settings)
        {
            var input = new VillageSettingInput();
            var tribe = (TribeEnums)settings.GetValueOrDefault(VillageSettingEnums.Tribe);
            if (!input.Tribe.Items.Any(x => x.Tribe == tribe)) settings[VillageSettingEnums.Tribe] = (int)input.Tribe.Get();
            input.Set(settings);
            return input;
        }

        private static Dictionary<TEnum, int> ParseSettings<TEnum>(Dictionary<string, int> body) where TEnum : struct, Enum
        {
            var settings = new Dictionary<TEnum, int>();
            foreach (var (key, value) in body ?? new Dictionary<string, int>())
            {
                if (Enum.TryParse<TEnum>(key, true, out var setting)) settings[setting] = value;
            }

            return settings;
        }

        private static bool NeedsConnection(PathString path, out int accountId)
        {
            accountId = 0;
            var parts = (path.Value ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4 || parts[0] != "api" || parts[1] != "accounts") return false;
            if (!int.TryParse(parts[2], out accountId)) return false;
            return parts[3] is "settings" or "villages" or "farming" or "debug";
        }

        private static IResult RequireConnected(IServiceProvider services, int accountId)
        {
            var status = services.GetRequiredService<TaskManager>().GetStatus(new AccountId(accountId));
            if (status is StatusEnums.Online or StatusEnums.Paused) return null;
            return Fail("Conectá la cuenta para ver sus aldeas, el farming y el resto.");
        }

        private static bool OwnsVillage(IServiceProvider services, int accountId, int villageId)
        {
            using var context = Context(services);
            return context.Villages.Any(x => x.Id == villageId && x.AccountId == accountId);
        }

        private static IResult EnsurePaused(IServiceProvider services, int accountId)
        {
            var status = services.GetRequiredService<TaskManager>().GetStatus(new AccountId(accountId));
            return status == StatusEnums.Online ? Fail("Pausá la cuenta antes de cambiar la cola de construcción.") : null;
        }

        private static void ScheduleUpgrade(IServiceProvider services, int accountId, int villageId)
        {
            services.GetRequiredService<TaskManager>().AddOrUpdate(new UpgradeBuildingTask.Task(new AccountId(accountId), new VillageId(villageId)));
        }

        private static bool TryParseCow(string urlInput, string troopInput, out string url, out string plan, out int count, out string error)
        {
            url = (urlInput ?? "").Trim();
            plan = "";
            count = 0;
            error = "";
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                error = "El enlace tiene que ser una URL http o https.";
                return false;
            }

            if (!CowTroopPlan.TryParse(troopInput ?? "", out var slots, out error)) return false;
            plan = CowTroopPlan.Format(slots);
            count = slots.Sum(slot => slot.Amount);
            return true;
        }

        private readonly record struct CowBulkLine(bool HasName, string Name, string Url, string Plan, int Count);

        private static bool TryParseCowLines(string text, out List<CowBulkLine> lines, out string error)
        {
            lines = [];
            error = "";
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var number = 0;
            foreach (var rawLine in (text ?? "").Replace("\r", "").Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0) continue;
                number++;
                if (!TryParseCowLine(line, out var parsed, out var parseError))
                {
                    error = $"Línea {number}: {parseError}";
                    return false;
                }

                if (!seen.Add(parsed.Url))
                {
                    error = $"Línea {number}: el enlace está repetido.";
                    return false;
                }

                lines.Add(parsed);
            }

            if (lines.Count == 0)
            {
                error = "No hay aldeas. Cada línea es: enlace | tropas.";
                return false;
            }

            return true;
        }

        private static bool TryParseCowLine(string line, out CowBulkLine parsed, out string error)
        {
            parsed = default;
            var urlStart = IndexOfUrl(line);
            if (urlStart < 0)
            {
                error = "usá enlace | tropas, o nombre | enlace | tropas.";
                return false;
            }

            var urlEnd = urlStart;
            while (urlEnd < line.Length && !char.IsWhiteSpace(line[urlEnd]) && line[urlEnd] != '|') urlEnd++;
            var urlInput = line[urlStart..urlEnd];
            var name = line[..urlStart].Trim().Trim('|').Trim();
            var troops = line[urlEnd..].Trim().Trim('|').Trim();
            if (!TryParseCow(urlInput, troops, out var url, out var plan, out var count, out error)) return false;
            parsed = new CowBulkLine(name.Length > 0, name.Length > 0 ? VillageLabel(name) : "", url, plan, count);
            return true;
        }

        private static int IndexOfUrl(string line)
        {
            var http = line.IndexOf("http://", StringComparison.OrdinalIgnoreCase);
            var https = line.IndexOf("https://", StringComparison.OrdinalIgnoreCase);
            if (http < 0) return https;
            if (https < 0) return http;
            return Math.Min(http, https);
        }

        private static bool TryParseDistance(string input, out double distance, bool allowZero = false)
        {
            var text = (input ?? "").Trim();
            if (allowZero && text.Length == 0)
            {
                distance = 0;
                return true;
            }

            var minimum = allowZero ? 0 : double.Epsilon;
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out distance) && distance >= minimum && (allowZero || distance > 0)) return true;
            if (double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out distance) && distance >= minimum && (allowZero || distance > 0)) return true;
            distance = 0;
            return false;
        }

        private static string FormatStatus(DateTime? lastAttackedAt, string skipReason)
        {
            if (skipReason is "Analizando" or "Atacando") return skipReason;
            // Hero statuses do not touch LastAttackedAt, so show them even after an earlier troop raid.
            if (!string.IsNullOrEmpty(skipReason) && HeroLevelingRules.HeroStatuses.Contains(skipReason)) return skipReason;
            if (lastAttackedAt is not null)
            {
                var minutes = (int)Math.Floor((DateTime.Now - lastAttackedAt.Value).TotalMinutes);
                if (minutes < 0) minutes = 0;
                return $"hace {minutes} min";
            }

            return string.IsNullOrWhiteSpace(skipReason) ? "sin atacar" : skipReason;
        }

        private static string FormatCountdown(BaseTask task)
        {
            if (task is null) return "";
            if (task.Stage == StageEnums.Executing) return "Atacando";
            var remaining = task.ExecuteAt - DateTime.Now;
            if (remaining <= TimeSpan.Zero) return "En espera";
            var text = remaining.TotalHours >= 1
                ? $"{(int)remaining.TotalHours}:{remaining.Minutes:00}:{remaining.Seconds:00}"
                : $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";
            return $"Próximo ataque en {text}";
        }

        private static string PauseLabel(StatusEnums status)
        {
            return status switch
            {
                StatusEnums.Online => "Pausar",
                StatusEnums.Paused => "Reanudar",
                _ => "Esperando",
            };
        }

        private static string SafeHost(string server)
        {
            return Uri.TryCreate(server, UriKind.Absolute, out var uri) ? uri.Host : server;
        }

        private static AppDbContext Context(IServiceProvider services)
        {
            return services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
        }

        private static IResult Ok(string message, object data = null)
        {
            return Results.Ok(new { ok = true, message, data });
        }

        private static IResult Fail(string message)
        {
            return Results.BadRequest(new { ok = false, message });
        }

        private static readonly BuildingEnums[] MultipleBuildings = [BuildingEnums.Warehouse, BuildingEnums.Granary, BuildingEnums.Trapper, BuildingEnums.Cranny];

        private static readonly BuildingEnums[] AvailableBuildings = Enum.GetValues<BuildingEnums>()
            .Where(x => x is not (BuildingEnums.Site or BuildingEnums.Blacksmith or BuildingEnums.CityWall or BuildingEnums.EarthWall or BuildingEnums.Palisade or BuildingEnums.WW or BuildingEnums.StoneWall or BuildingEnums.MakeshiftWall or BuildingEnums.Unknown))
            .ToArray();
    }

    public sealed class AccessBody
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string ProxyHost { get; set; }
        public int ProxyPort { get; set; }
        public string ProxyUsername { get; set; }
        public string ProxyPassword { get; set; }
        public string Useragent { get; set; }
    }

    public sealed class AccountBody
    {
        public string Username { get; set; }
        public string Server { get; set; }
        public List<AccessBody> Accesses { get; set; }
    }

    public sealed class BulkBody
    {
        public string Text { get; set; }
    }

    public sealed class TemplateBody
    {
        public string Path { get; set; }
    }

    public sealed class NormalJobBody
    {
        public int Location { get; set; }
        public string Type { get; set; }
        public int Level { get; set; }
    }

    public sealed class ResourceJobBody
    {
        public string Plan { get; set; }
        public int Level { get; set; }
    }

    public sealed class UpgradeBody
    {
        public int Location { get; set; }
        public bool Max { get; set; }
    }

    public sealed class MoveBody
    {
        public string Direction { get; set; }
    }

    public sealed class ImportJobsBody
    {
        public List<JobIn> Jobs { get; set; }
        public bool Shuffle { get; set; }
    }

    public sealed class JobIn
    {
        public string Type { get; set; }
        public string Content { get; set; }
    }

    public sealed class CowListBody
    {
        public int VillageId { get; set; }
        public string Name { get; set; }
        public int MinMinutes { get; set; }
        public int RepeatMinutes { get; set; }
        public bool? LevelHero { get; set; }
        public bool AutoStart { get; set; }
    }

    public sealed class CowVillageBody
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public string Troops { get; set; }
    }

    public sealed class GameFarmBody
    {
        public bool UseStartAll { get; set; }
        public int IntervalMin { get; set; }
        public int IntervalMax { get; set; }
    }

    public sealed class SearchBody
    {
        public string Troops { get; set; }
        public string MinDistance { get; set; }
        public string Distance { get; set; }
    }

    public sealed class ApplySearchBody
    {
        public string TroopPlan { get; set; }
        public List<HitBody> Hits { get; set; }
    }

    public sealed class HitBody
    {
        public int X { get; set; }
        public int Y { get; set; }
    }
}
