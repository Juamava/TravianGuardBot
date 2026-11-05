using System.Globalization;

using MainCore.Parsers;
using MainCore.UI.Models.Output;
using MainCore.UI.ViewModels.UserControls;

using Splat;

namespace MainCore.UI.ViewModels.Tabs
{
    using ReactiveUI.Primitives;
    using ReactiveUI.Primitives.Concurrency;
    using ReactiveUI.Primitives.Disposables;
    using ReactiveUI.Primitives.Signals;

    public partial class FarmingViewModel
    {
        public ListBoxItemViewModel CowListVillages { get; } = new();
        public ListBoxItemViewModel CowLists { get; } = new();
        public ListBoxItemViewModel CowVillages { get; } = new();

        [Reactive]
        private string _cowListName = "";

        [Reactive]
        private string _cowVillageUrl = "";

        [Reactive]
        private string _cowTroopCount = "";

        [Reactive]
        private string _cowListMinMinutes = "0";

        [Reactive]
        private string _cowListRepeatMinutes = "0";

        [Reactive]
        private bool _isAddingCow;

        [Reactive]
        private bool _isCowEditor;

        [Reactive]
        private string _nextRaidCountdown = "";

        private void SetupCowLists()
        {
            LoadCowListVillagesCommand
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(items =>
                {
                    var preferredId = CowListVillages.SelectedItem?.Id ?? _selectedItemStore.Village?.Id ?? 0;
                    CowListVillages.Load(items);
                    CowListVillages.SelectedItem = items.FirstOrDefault(item => item.Id == preferredId) ?? items.FirstOrDefault();
                });

            this.WhenAnyValue(x => x.CowListVillages.SelectedItem)
                .Select(_ => AccountId)
                .ObserveOn(RxSchedulers.TaskpoolScheduler)
                .InvokeCommand(LoadCowListsCommand);

            LoadCowListsCommand
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(items =>
                {
                    CowLists.Load(items);
                });

            LoadCowVillagesCommand
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(CowVillages.Load);

            this.WhenAnyValue(x => x.CowLists.SelectedItem)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(item =>
                {
                    CowListName = item?.Content ?? "";
                    var settings = ReadListSettings(item);
                    CowListMinMinutes = settings.MinMinutes;
                    CowListRepeatMinutes = settings.RepeatMinutes;
                    IsAddingCow = false;
                    CowVillages.SelectedItem = null;
                });

            this.WhenAnyValue(x => x.CowVillages.SelectedItem, x => x.IsAddingCow,
                    (item, adding) => adding || item is not null)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(open => IsCowEditor = open);

            this.WhenAnyValue(x => x.CowVillages.SelectedItem)
                .Where(item => item is not null)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(_ => IsAddingCow = false);

            RxSchedulers.MainThreadScheduler.Schedule(
                state: this,
                dueTime: TimeSpan.FromSeconds(1),
                action: static (scheduler, viewModel) => viewModel.TickCountdown(scheduler));

            _rxQueue.GetObservable<CowVillagesModified>()
                .InvokeCommand(CowVillagesModifiedCommand);

            CowVillagesModifiedCommand
                .Where(changed => changed)
                .Select(_ => CowLists.SelectedItem?.Id ?? 0)
                .Throttle(TimeSpan.FromMilliseconds(500), RxSchedulers.TaskpoolScheduler)
                .ObserveOn(RxSchedulers.TaskpoolScheduler)
                .InvokeCommand(LoadCowVillagesCommand);

            this.WhenAnyValue(x => x.CowLists.SelectedItem)
                .Select(item => item?.Id ?? 0)
                .ObserveOn(RxSchedulers.TaskpoolScheduler)
                .InvokeCommand(LoadCowVillagesCommand);

            this.WhenAnyValue(x => x.CowVillages.SelectedItem)
                .ObserveOn(RxSchedulers.TaskpoolScheduler)
                .Select(LoadCowVillageFields)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(fields =>
                {
                    CowVillageUrl = fields.Url;
                    CowTroopCount = fields.TroopPlan;
                });
        }

        [ReactiveCommand(RunInBackground = true)]
        private List<ListBoxItem> LoadCowListVillages(AccountId accountId)
        {
            if (accountId == AccountId.Empty) return [];

            using var context = _contextFactory.CreateDbContext();
            return context.Villages
                .Where(x => x.AccountId == accountId.Value)
                .OrderBy(x => x.Name)
                .Select(x => new ListBoxItem
                {
                    Id = x.Id,
                    Content = x.Name + " (" + x.X + "|" + x.Y + ")",
                    Color = SplatColor.Black,
                })
                .ToList();
        }

        [ReactiveCommand(RunInBackground = true)]
        private List<ListBoxItem> LoadCowLists(AccountId accountId)
        {
            var villageId = CowListVillages.SelectedItem?.Id ?? 0;
            if (accountId == AccountId.Empty || villageId == 0) return [];

            using var context = _contextFactory.CreateDbContext();
            return context.CowLists
                .Where(x => x.AccountId == accountId.Value && x.VillageId == villageId)
                .OrderBy(x => x.Name)
                .Select(x => new ListBoxItem
                {
                    Id = x.Id,
                    Content = x.Name,
                    Color = SplatColor.Black,
                })
                .ToList();
        }

        [ReactiveCommand(RunInBackground = true)]
        private List<ListBoxItem> LoadCowVillages(int cowListId)
        {
            if (cowListId == 0 || AccountId == AccountId.Empty) return [];

            using var context = _contextFactory.CreateDbContext();
            return context.CowVillages
                .Where(x => x.CowListId == cowListId)
                .Where(x => x.CowList.AccountId == AccountId.Value)
                .OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.Name, x.TroopCount, x.TroopPlan, x.Url, x.LastAttackedAt, x.LastSkipReason })
                .AsEnumerable()
                .Select(x => new ListBoxItem
                {
                    Id = x.Id,
                    Content = CowTroopPlan.Describe(x.TroopPlan, x.TroopCount) + "  " + (string.IsNullOrWhiteSpace(x.Name) ? CowOasisParser.MissingName : x.Name),
                    Detail = FormatStatus(x.LastAttackedAt, x.LastSkipReason),
                    Color = SplatColor.Black,
                })
                .ToList();
        }

        [ReactiveCommand(RunInBackground = true)]
        private bool CowVillagesModified(CowVillagesModified notification)
        {
            if (!IsActive) return false;
            if (notification.AccountId != AccountId) return false;
            return true;
        }

        [ReactiveCommand(RunInBackground = true)]
        private async Task SaveCowListInterval()
        {
            if (CowLists.SelectedItem is null)
            {
                await _dialogService.SendMessage("Warning", "Seleccioná una lista.");
                return;
            }

            if (!TryParseListSettings(out var minMinutes, out var repeatMinutes, out var error))
            {
                await _dialogService.SendMessage("Warning", error);
                return;
            }

            if (!SaveListSettings(CowLists.SelectedItem.Id, minMinutes, repeatMinutes))
            {
                await _dialogService.SendMessage("Warning", "No se encontró la lista.");
                return;
            }

            var minimum = minMinutes == 0
                ? "Sin mínimo por aldea: en la próxima revisión se puede atacar de nuevo."
                : $"Una aldea no se vuelve a atacar antes de {minMinutes} minutos.";
            var repeat = repeatMinutes == 0
                ? "La lista se revisa una sola vez."
                : $"La lista se revisa cada {repeatMinutes} minutos y ataca solo las que ya cumplieron el mínimo.";
            await _dialogService.SendMessage("Information", minimum + " " + repeat);
        }

        public void ShowListModule()
        {
            IsAddingCow = false;
            CowVillages.SelectedItem = null;
        }

        [ReactiveCommand]
        private async Task BeginAddCowVillage()
        {
            if (CowLists.SelectedItem is null)
            {
                await _dialogService.SendMessage("Warning", "Seleccioná una lista.");
                return;
            }

            IsAddingCow = true;
            CowVillageUrl = "";
            CowTroopCount = "";
            CowVillages.SelectedItem = null;
        }

        [ReactiveCommand(RunInBackground = true)]
        private async Task CreateCowList()
        {
            if (AccountId == AccountId.Empty)
            {
                await _dialogService.SendMessage("Warning", "No hay una cuenta seleccionada.");
                return;
            }

            var villageId = CowListVillages.SelectedItem?.Id ?? 0;
            if (villageId == 0)
            {
                await _dialogService.SendMessage("Warning", "Seleccioná una aldea. La lista queda asociada a esa aldea.");
                return;
            }

            var name = CowListName.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                await _dialogService.SendMessage("Warning", "Escribí un nombre para la lista.");
                return;
            }

            using var context = _contextFactory.CreateDbContext();
            var villageBelongsToAccount = context.Villages.Any(x => x.Id == villageId && x.AccountId == AccountId.Value);
            if (!villageBelongsToAccount)
            {
                await _dialogService.SendMessage("Warning", "La aldea seleccionada no pertenece a esta cuenta.");
                return;
            }

            var exists = context.CowLists.Any(x => x.AccountId == AccountId.Value && x.VillageId == villageId && x.Name == name);
            if (exists)
            {
                await _dialogService.SendMessage("Warning", "Ya existe una lista con ese nombre en esta aldea.");
                return;
            }

            context.CowLists.Add(new CowList
            {
                AccountId = AccountId.Value,
                VillageId = villageId,
                Name = name,
            });
            context.SaveChanges();
            await LoadCowListsCommand.Execute(AccountId);
            await _dialogService.SendMessage("Information", "Lista creada.");
        }

        [ReactiveCommand(RunInBackground = true)]
        private async Task RenameCowList()
        {
            if (CowLists.SelectedItem is null)
            {
                await _dialogService.SendMessage("Warning", "Seleccioná una lista.");
                return;
            }

            var name = CowListName.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                await _dialogService.SendMessage("Warning", "Escribí un nombre para la lista.");
                return;
            }

            var listId = CowLists.SelectedItem.Id;
            using var context = _contextFactory.CreateDbContext();
            var villageId = context.CowLists
                .Where(x => x.Id == listId && x.AccountId == AccountId.Value)
                .Select(x => x.VillageId)
                .FirstOrDefault();
            var exists = context.CowLists.Any(x => x.AccountId == AccountId.Value && x.VillageId == villageId && x.Id != listId && x.Name == name);
            if (exists)
            {
                await _dialogService.SendMessage("Warning", "Ya existe una lista con ese nombre en esta aldea.");
                return;
            }

            var updated = context.CowLists
                .Where(x => x.Id == listId && x.AccountId == AccountId.Value)
                .ExecuteUpdate(x => x.SetProperty(x => x.Name, name));
            if (updated == 0)
            {
                await _dialogService.SendMessage("Warning", "No se encontró la lista.");
                return;
            }

            await LoadCowListsCommand.Execute(AccountId);
            await _dialogService.SendMessage("Information", "Lista renombrada.");
        }

        [ReactiveCommand(RunInBackground = true)]
        private async Task DeleteCowList()
        {
            if (CowLists.SelectedItem is null)
            {
                await _dialogService.SendMessage("Warning", "Seleccioná una lista.");
                return;
            }

            var confirmed = await _dialogService.SendConfirm("Information", $"¿Borrar la lista \"{CowLists.SelectedItem.Content}\" y sus aldeas?");
            if (!confirmed) return;

            var listId = CowLists.SelectedItem.Id;
            using var context = _contextFactory.CreateDbContext();
            context.CowVillages
                .Where(x => x.CowListId == listId)
                .ExecuteDelete();
            context.CowLists
                .Where(x => x.Id == listId && x.AccountId == AccountId.Value)
                .ExecuteDelete();

            await LoadCowListsCommand.Execute(AccountId);
            await LoadCowVillagesCommand.Execute(0);
            await _dialogService.SendMessage("Information", "Lista borrada.");
        }

        [ReactiveCommand(RunInBackground = true)]
        private async Task AddCowVillage()
        {
            if (CowLists.SelectedItem is null)
            {
                await _dialogService.SendMessage("Warning", "Seleccioná una lista.");
                return;
            }

            if (!TryParseCowVillage(CowVillageUrl, CowTroopCount, out var url, out var troopPlan, out var troopCount, out var error))
            {
                await _dialogService.SendMessage("Warning", error);
                return;
            }

            var listId = CowLists.SelectedItem.Id;
            using var context = _contextFactory.CreateDbContext();
            var listExists = context.CowLists.Any(x => x.Id == listId && x.AccountId == AccountId.Value);
            if (!listExists)
            {
                await _dialogService.SendMessage("Warning", "No se encontró la lista.");
                return;
            }

            context.CowVillages.Add(new CowVillage
            {
                CowListId = listId,
                Name = CowOasisParser.MissingName,
                Url = url,
                TroopCount = troopCount,
                TroopPlan = troopPlan,
            });
            context.SaveChanges();
            await LoadCowVillagesCommand.Execute(listId);
            RxSchedulers.MainThreadScheduler.Schedule(
                state: this,
                action: static (_, viewModel) =>
                {
                    viewModel.IsAddingCow = false;
                    return EmptyDisposable.Instance;
                });
            await _dialogService.SendMessage("Information", "Aldea agregada.");
        }

        [ReactiveCommand(RunInBackground = true)]
        private async Task UpdateCowVillage()
        {
            if (CowVillages.SelectedItem is null)
            {
                await _dialogService.SendMessage("Warning", "Seleccioná una aldea.");
                return;
            }

            if (!TryParseCowVillage(CowVillageUrl, CowTroopCount, out var url, out var troopPlan, out var troopCount, out var error))
            {
                await _dialogService.SendMessage("Warning", error);
                return;
            }

            var villageId = CowVillages.SelectedItem.Id;
            var listId = CowLists.SelectedItem?.Id ?? 0;
            using var context = _contextFactory.CreateDbContext();
            var updated = context.CowVillages
                .Where(x => x.Id == villageId && x.CowList.AccountId == AccountId.Value)
                .ExecuteUpdate(setters => setters
                    .SetProperty(x => x.Url, url)
                    .SetProperty(x => x.TroopCount, troopCount)
                    .SetProperty(x => x.TroopPlan, troopPlan));
            if (updated == 0)
            {
                await _dialogService.SendMessage("Warning", "No se encontró la aldea.");
                return;
            }

            await LoadCowVillagesCommand.Execute(listId);
            await _dialogService.SendMessage("Information", "Aldea actualizada.");
        }

        [ReactiveCommand(RunInBackground = true)]
        private async Task DeleteCowVillage()
        {
            if (CowVillages.SelectedItem is null)
            {
                await _dialogService.SendMessage("Warning", "Seleccioná una aldea.");
                return;
            }

            var confirmed = await _dialogService.SendConfirm("Information", "¿Borrar esta aldea de la lista?");
            if (!confirmed) return;

            var villageId = CowVillages.SelectedItem.Id;
            var listId = CowLists.SelectedItem?.Id ?? 0;
            using var context = _contextFactory.CreateDbContext();
            context.CowVillages
                .Where(x => x.Id == villageId && x.CowList.AccountId == AccountId.Value)
                .ExecuteDelete();

            await LoadCowVillagesCommand.Execute(listId);
            await _dialogService.SendMessage("Information", "Aldea borrada.");
        }

        [ReactiveCommand(RunInBackground = true)]
        private async Task StartCowList()
        {
            if (CowLists.SelectedItem is null)
            {
                await _dialogService.SendMessage("Warning", "Seleccioná una lista.");
                return;
            }

            if (_taskManager.IsExist<RaidCowListTask.Task>(AccountId))
            {
                await _dialogService.SendMessage("Information", "Ya hay un raid de vacas en la cola. Detenelo antes de iniciar otra lista.");
                return;
            }

            if (!TryParseListSettings(out var minMinutes, out var repeatMinutes, out var settingsError))
            {
                await _dialogService.SendMessage("Warning", settingsError);
                return;
            }

            if (minMinutes < 1 || repeatMinutes < 1)
            {
                await _dialogService.SendMessage("Warning", "Para iniciar, el mínimo por aldea y la revisión de la lista tienen que ser de 1 minuto o más.");
                return;
            }

            var listId = CowLists.SelectedItem.Id;
            if (!SaveListSettings(listId, minMinutes, repeatMinutes))
            {
                await _dialogService.SendMessage("Warning", "No se encontró la lista.");
                return;
            }

            using (var context = _contextFactory.CreateDbContext())
            {
                var count = context.CowVillages.Count(x => x.CowListId == listId && x.CowList.AccountId == AccountId.Value);
                if (count == 0)
                {
                    await _dialogService.SendMessage("Information", "La lista no tiene aldeas.");
                    return;
                }
            }

            _taskManager.Add(new RaidCowListTask.Task(AccountId, listId));
            await _dialogService.SendMessage("Information", repeatMinutes == 0
                ? $"Empieza la revisión. Mínimo por aldea: {minMinutes} minutos."
                : $"Empieza la revisión. Mínimo por aldea: {minMinutes} minutos. La próxima revisión es {repeatMinutes} minutos después de que termine esta.");
        }

        [ReactiveCommand(RunInBackground = true)]
        private async Task StopCowList()
        {
            var running = _taskManager.GetTaskList(AccountId).OfType<RaidCowListTask.Task>().FirstOrDefault();
            if (running is not null) running.KeepRepeating = false;
            _taskManager.CancelIfCurrent<RaidCowListTask.Task>(AccountId);
            _taskManager.Remove<RaidCowListTask.Task>(AccountId);
            await _dialogService.SendMessage("Information", "Se detuvo el raid y la repetición.");
        }

        private (string Url, string TroopPlan) LoadCowVillageFields(ListBoxItem? item)
        {
            if (item is null || AccountId == AccountId.Empty) return ("", "");

            using var context = _contextFactory.CreateDbContext();
            var village = context.CowVillages
                .Where(x => x.Id == item.Id && x.CowList.AccountId == AccountId.Value)
                .Select(x => new { x.Url, x.TroopCount, x.TroopPlan })
                .FirstOrDefault();

            if (village is null) return ("", "");
            return (village.Url, CowTroopPlan.Describe(village.TroopPlan, village.TroopCount));
        }

        private static bool TryParseCowVillage(string urlInput, string troopInput, out string url, out string troopPlan, out int troopCount, out string error)
        {
            url = urlInput.Trim();
            troopPlan = "";
            troopCount = 0;
            error = "";

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                error = "El enlace tiene que ser una URL http o https.";
                return false;
            }

            if (!CowTroopPlan.TryParse(troopInput, out var slots, out error)) return false;

            troopPlan = CowTroopPlan.Format(slots);
            troopCount = slots.Sum(slot => slot.Amount);
            return true;
        }

        [ReactiveCommand(RunInBackground = true)]
        private async Task SearchOasis()
        {
            if (CowLists.SelectedItem is null)
            {
                await _dialogService.SendMessage("Warning", "Seleccioná una lista.");
                return;
            }

            var listId = CowLists.SelectedItem.Id;
            string server;
            int originX;
            int originY;
            string villageName;
            using (var context = _contextFactory.CreateDbContext())
            {
                var list = context.CowLists
                    .Where(x => x.Id == listId && x.AccountId == AccountId.Value)
                    .Select(x => new { x.VillageId })
                    .FirstOrDefault();
                if (list is null)
                {
                    await _dialogService.SendMessage("Warning", "No se encontró la lista.");
                    return;
                }

                var village = context.Villages
                    .Where(x => x.Id == list.VillageId && x.AccountId == AccountId.Value)
                    .Select(x => new { x.Name, x.X, x.Y })
                    .FirstOrDefault();
                if (village is null)
                {
                    await _dialogService.SendMessage("Warning", "La lista no tiene una aldea asociada. Creala de nuevo con una aldea seleccionada.");
                    return;
                }

                server = context.Accounts
                    .Where(x => x.Id == AccountId.Value)
                    .Select(x => x.Server)
                    .First();
                originX = village.X;
                originY = village.Y;
                villageName = village.Name;
            }

            var browser = _chromeManager.Get(AccountId);
            if (!browser.IsInitialized)
            {
                await _dialogService.SendMessage("Warning", "La cuenta tiene que estar logueada para abrir el navegador.");
                return;
            }

            var troopText = await _dialogService.RequestInput("Buscar oasis", "Tropas para cada oasis. Un número solo usa A. Ejemplo: 2 o A:2 D:1");
            if (string.IsNullOrWhiteSpace(troopText)) return;
            if (!CowTroopPlan.TryParse(troopText, out var slots, out var troopError))
            {
                await _dialogService.SendMessage("Warning", troopError);
                return;
            }

            var troopPlan = CowTroopPlan.Format(slots);
            var troopCount = slots.Sum(slot => slot.Amount);

            var minText = await _dialogService.RequestInput("Buscar oasis", $"Distancia mínima en casillas desde {villageName} ({originX}|{originY}). Escribí 0 para incluir las cercanas:");
            if (string.IsNullOrWhiteSpace(minText)) return;
            if (!TryParseDistance(minText, out var minDistance, allowZero: true))
            {
                await _dialogService.SendMessage("Warning", "La distancia mínima tiene que ser un número de 0 o más.");
                return;
            }

            var distanceText = await _dialogService.RequestInput("Buscar oasis", $"Distancia máxima en casillas desde {villageName} ({originX}|{originY}):");
            if (string.IsNullOrWhiteSpace(distanceText)) return;
            if (!TryParseDistance(distanceText, out var maxDistance))
            {
                await _dialogService.SendMessage("Warning", "La distancia máxima tiene que ser un número mayor que 0.");
                return;
            }

            if (minDistance > maxDistance)
            {
                await _dialogService.SendMessage("Warning", "La distancia mínima no puede ser mayor que la máxima.");
                return;
            }

            List<OasisHit> found;
            try
            {
                var catalogUrl = OasisCatalog.BuildCatalogUrl(server, originX, originY);
                found = await browser.InNewTab(page => OasisCatalog.Collect(page, catalogUrl, originX, originY, minDistance, maxDistance));
            }
            catch (Exception ex)
            {
                await _dialogService.SendMessage("Error", "No se pudo leer la lista de oasis. " + ex.Message);
                return;
            }

            if (found.Count == 0)
            {
                await _dialogService.SendMessage("Information", "No se encontraron oasis dentro de esa distancia.");
                return;
            }

            var distanceLabel = maxDistance.ToString("0.##", CultureInfo.CurrentCulture);
            var confirmed = await _dialogService.SendOkCancel(
                "Oasis",
                $"Se encontraron {found.Count} oasis a {distanceLabel} casillas o menos de {villageName} ({originX}|{originY}).\nSe van a guardar con {troopPlan} cada uno.\n\n¿Agregarlos a la lista?");
            if (!confirmed) return;

            var added = AddOasisHits(listId, server, troopPlan, troopCount, found);
            await LoadCowVillagesCommand.Execute(listId);
            await _dialogService.SendMessage("Information", added == found.Count
                ? $"Se agregaron {added} oasis."
                : $"Se agregaron {added} oasis. {found.Count - added} ya estaban en la lista.");
        }

        private int AddOasisHits(int listId, string server, string troopPlan, int troopCount, List<OasisHit> found)
        {
            using var context = _contextFactory.CreateDbContext();
            var existing = context.CowVillages
                .Where(x => x.CowListId == listId && x.CowList.AccountId == AccountId.Value)
                .Select(x => x.Url)
                .ToList();
            var existingUrls = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
            var added = 0;

            foreach (var oasis in found)
            {
                var url = OasisCatalog.BuildOasisUrl(server, oasis.X, oasis.Y);
                if (!existingUrls.Add(url)) continue;

                context.CowVillages.Add(new CowVillage
                {
                    CowListId = listId,
                    Name = CowOasisParser.MissingName,
                    Url = url,
                    TroopCount = troopCount,
                    TroopPlan = troopPlan,
                });
                added++;
            }

            if (added > 0) context.SaveChanges();
            return added;
        }

        private (string MinMinutes, string RepeatMinutes) ReadListSettings(ListBoxItem? item)
        {
            if (item is null || AccountId == AccountId.Empty) return ("0", "0");

            using var context = _contextFactory.CreateDbContext();
            var settings = context.CowLists
                .Where(x => x.Id == item.Id && x.AccountId == AccountId.Value)
                .Select(x => new { x.MinAttackMinutes, x.RepeatMinutes })
                .FirstOrDefault();
            if (settings is null) return ("0", "0");
            return (
                settings.MinAttackMinutes.ToString(CultureInfo.InvariantCulture),
                settings.RepeatMinutes.ToString(CultureInfo.InvariantCulture));
        }

        private bool SaveListSettings(int listId, int minMinutes, int repeatMinutes)
        {
            using var context = _contextFactory.CreateDbContext();
            var updated = context.CowLists
                .Where(x => x.Id == listId && x.AccountId == AccountId.Value)
                .ExecuteUpdate(x => x
                    .SetProperty(list => list.MinAttackMinutes, minMinutes)
                    .SetProperty(list => list.RepeatMinutes, repeatMinutes));
            return updated > 0;
        }

        private bool TryParseListSettings(out int minMinutes, out int repeatMinutes, out string error)
        {
            minMinutes = 0;
            repeatMinutes = 0;
            error = "";

            if (!int.TryParse(CowListMinMinutes.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out minMinutes) || minMinutes < 0)
            {
                error = "El mínimo por aldea tiene que ser un número de minutos mayor o igual a 0.";
                return false;
            }

            var repeatText = CowListRepeatMinutes.Trim();
            if (repeatText.Length == 0) repeatText = "0";
            if (!int.TryParse(repeatText, NumberStyles.Integer, CultureInfo.InvariantCulture, out repeatMinutes) || repeatMinutes < 0)
            {
                error = "La revisión de la lista tiene que ser un número de minutos mayor o igual a 0. 0 la recorre una sola vez.";
                return false;
            }

            return true;
        }

        private IDisposable TickCountdown(ISequencer scheduler)
        {
            NextRaidCountdown = FormatNextRaid();
            return scheduler.Schedule(this, TimeSpan.FromSeconds(1), static (next, viewModel) => viewModel.TickCountdown(next));
        }

        private string FormatNextRaid()
        {
            if (CowLists.SelectedItem is null) return "";

            var task = _taskManager.GetTaskList(AccountId).OfType<RaidCowListTask.Task>().FirstOrDefault();
            if (task is null) return "";
            if (task.Stage == StageEnums.Executing) return "Atacando";

            var remaining = task.ExecuteAt - DateTime.Now;
            if (remaining <= TimeSpan.Zero) return "En espera";

            var text = remaining.TotalHours >= 1
                ? $"{(int)remaining.TotalHours}:{remaining.Minutes:00}:{remaining.Seconds:00}"
                : $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";
            return $"Próximo ataque en {text}";
        }

        private static string FormatStatus(DateTime? lastAttackedAt, string? skipReason)
        {
            if (skipReason is "Analizando" or "Atacando") return skipReason;
            if (lastAttackedAt is not null)
            {
                var minutes = (int)Math.Floor((DateTime.Now - lastAttackedAt.Value).TotalMinutes);
                if (minutes < 0) minutes = 0;
                return $"hace {minutes} min";
            }

            if (!string.IsNullOrWhiteSpace(skipReason)) return skipReason;
            return "sin atacar";
        }

        private static bool TryParseDistance(string input, out double distance, bool allowZero = false)
        {
            var text = input.Trim();
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out distance) && distance >= 0 && (allowZero || distance > 0)) return true;
            if (double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out distance) && distance >= 0 && (allowZero || distance > 0)) return true;
            distance = 0;
            return false;
        }
    }
}
