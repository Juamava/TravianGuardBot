import { useEffect, useRef, useState, type ReactNode } from 'react'
import { Box, Button, Checkbox, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, MenuItem, Stack, Switch, TextField, Tooltip, Typography } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import InfoOutlinedIcon from '@mui/icons-material/InfoOutlined'
import PlayArrowIcon from '@mui/icons-material/PlayArrow'
import SearchIcon from '@mui/icons-material/Search'
import StopIcon from '@mui/icons-material/Stop'
import SyncIcon from '@mui/icons-material/Sync'
import { api, del, post, put } from '../api'
import { Empty, panel, useUi } from '../ui'

type Village = { id: number; name: string; x: number; y: number }
type CowList = { id: number; name: string; minMinutes: number; repeatMinutes: number; levelHero: boolean; autoStart: boolean; running: boolean; countdown: string }
type Cow = { id: number; listId: number; name: string; url: string; troops: string; status: string; kind: string; owner: string; tribe: string; alliance: string; population: number; distance: number; x: number | null; y: number | null }
type GameFarm = { id: number; name: string; active: boolean }
type FarmingData = { villages: Village[]; lists: CowList[]; cows: Cow[]; countdown: string; gold: boolean; gameLists: GameFarm[]; useStartAll: boolean; farmIntervalMin: number; farmIntervalMax: number; gameRunning: boolean; gameCountdown: string }
type SearchResult = { count: number; villageName: string; originX: number; originY: number; troopPlan: string; hits: { x: number; y: number; distance: number }[] }

const cowColumns = 'minmax(160px, 1.6fr) 72px minmax(100px, 1fr) 88px 88px 110px minmax(120px, 1fr)'

function listTitle(list: CowList) {
  const marks = [list.running ? 'en curso' : '', list.autoStart ? 'auto' : ''].filter(Boolean)
  return marks.length === 0 ? list.name : `${list.name} · ${marks.join(' · ')}`
}

function kindLabel(kind: string) {
  if (kind === 'oasis') return 'Oasis'
  if (kind === 'aldea') return 'Aldea'
  return '—'
}

function bound(raw: string) {
  const text = raw.trim().replace(',', '.')
  if (text === '') return null
  const value = Number(text)
  return Number.isFinite(value) ? value : null
}

function cowMatches(cow: Cow, query: string, kind: string, popFrom: string, popTo: string, distFrom: string, distTo: string) {
  const text = query.trim().toLowerCase()
  if (text) {
    const hay = `${cow.name} ${cow.owner} ${cow.tribe} ${cow.alliance}`.toLowerCase()
    if (!hay.includes(text)) return false
  }
  if (kind !== 'todos' && cow.kind !== kind) return false
  const fromPop = bound(popFrom)
  const toPop = bound(popTo)
  if ((fromPop != null || toPop != null) && cow.population < 0) return false
  if (fromPop != null && cow.population < fromPop) return false
  if (toPop != null && cow.population > toPop) return false
  const fromDist = bound(distFrom)
  const toDist = bound(distTo)
  if ((fromDist != null || toDist != null) && cow.distance < 0) return false
  if (fromDist != null && cow.distance < fromDist) return false
  if (toDist != null && cow.distance > toDist) return false
  return true
}

const tile = {
  minWidth: 0,
  height: 64,
  px: 0.5,
  display: 'flex',
  flexDirection: 'column',
  alignItems: 'center',
  justifyContent: 'center',
  gap: 0.25,
  fontSize: 12,
  lineHeight: 1.2,
  whiteSpace: 'nowrap',
  color: '#dbdee1',
  bgcolor: '#313338',
  '&:hover': { bgcolor: '#35373c' },
  '&.Mui-disabled': { color: '#6d6f78' },
  '&.Mui-disabled .MuiSvgIcon-root': { color: '#6d6f78' },
} as const

function Hint({ text }: { text: string }) {
  return (
    <Tooltip title={text} placement="top" arrow enterDelay={200} slotProps={{ tooltip: { sx: { maxWidth: 280, fontSize: 13, lineHeight: 1.45 } } }}>
      <InfoOutlinedIcon sx={{ fontSize: 16, color: '#949ba4', cursor: 'help' }} />
    </Tooltip>
  )
}

function GameFarming({ accountId, data, notify, reload }: { accountId: number; data: FarmingData; notify: (message: string) => void; reload: () => Promise<FarmingData | undefined> }) {
  const [min, setMin] = useState(String(data.farmIntervalMin))
  const [max, setMax] = useState(String(data.farmIntervalMax))
  const [all, setAll] = useState(data.useStartAll)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    setMin(String(data.farmIntervalMin))
    setMax(String(data.farmIntervalMax))
    setAll(data.useStartAll)
  }, [data.farmIntervalMin, data.farmIntervalMax, data.useStartAll])

  async function act(action: () => Promise<{ message: string }>) {
    setBusy(true)
    try {
      notify((await action()).message)
      await reload()
    } catch (error) {
      notify(error instanceof Error ? error.message : 'No se pudo completar')
    } finally {
      setBusy(false)
    }
  }

  async function saveOptions(useStartAll = all, intervalMin = Number(min), intervalMax = Number(max)) {
    if (!Number.isFinite(intervalMin) || !Number.isFinite(intervalMax) || intervalMin < 0 || intervalMax < intervalMin) {
      notify('El intervalo mínimo tiene que ser menor o igual que el máximo.')
      return
    }
    await act(() => put(`/api/accounts/${accountId}/farming/game/options`, { useStartAll, intervalMin, intervalMax }))
  }

  return (
    <Stack spacing={2} sx={{ minWidth: 0 }}>
      <Typography variant="h4">Listas del juego</Typography>
      <Typography color="text.secondary">El bot inicia las listas de vacas que ya tiene Travian. Si también hay una lista propia en curso, esta espera a que el navegador se libere.</Typography>
      <Box sx={{ ...panel }}>
        <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(3, minmax(0, 1fr))', gap: 1 }}>
          <Tile color="#5865f2" icon={<SyncIcon />} label="Actualizar" disabled={busy} onClick={() => act(() => post(`/api/accounts/${accountId}/farming/game/refresh`))} />
          <Tile color="#23a559" icon={<PlayArrowIcon />} label="Iniciar" disabled={busy || data.gameRunning} onClick={() => act(() => post(`/api/accounts/${accountId}/farming/game/start`))} />
          <Tile color="#ed4245" icon={<StopIcon />} label="Detener" disabled={busy || !data.gameRunning} onClick={() => act(() => post(`/api/accounts/${accountId}/farming/game/stop`))} />
        </Box>
        {data.gameCountdown && <Typography color="error" fontWeight={800} sx={{ mt: 1.5 }}>{data.gameCountdown}</Typography>}
      </Box>
      <Stack direction="row" spacing={1} alignItems="center" sx={{ ...panel, py: 1 }}>
        <Switch checked={all} disabled={busy} onChange={event => { const next = event.target.checked; setAll(next); void saveOptions(next) }} />
        <Typography fontWeight={600}>Iniciar todas</Typography>
        <Hint text="Si está apagado, solo inicia las listas marcadas como activas. Si está encendido, aprieta el botón de iniciar todas en el juego." />
      </Stack>
      <Box sx={{ ...panel, display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 1 }}>
        <TextField type="number" label="Repetir desde" value={min} onChange={event => setMin(event.target.value)} onBlur={() => saveOptions()} slotProps={{ input: { endAdornment: <Typography color="text.secondary" sx={{ ml: 1 }}>seg</Typography> } }} />
        <TextField type="number" label="Repetir hasta" value={max} onChange={event => setMax(event.target.value)} onBlur={() => saveOptions()} slotProps={{ input: { endAdornment: <Typography color="text.secondary" sx={{ ml: 1 }}>seg</Typography> } }} />
      </Box>
      <Stack spacing={1} sx={{ ...panel, p: 1 }}>
        {data.gameLists.map(list => (
          <Stack key={list.id} direction="row" spacing={1} alignItems="center" sx={{ px: 1, py: 0.5 }}>
            <Switch checked={list.active} disabled={busy} onChange={() => act(() => post(`/api/accounts/${accountId}/farming/game/${list.id}/active`))} />
            <Typography sx={{ fontWeight: 600 }}>{list.name}</Typography>
            <Typography color="text.secondary">{list.active ? 'Activa' : 'Inactiva'}</Typography>
          </Stack>
        ))}
        {data.gameLists.length === 0 && <Empty title="Sin listas del juego" text="Apretá Actualizar para entrar al punto de reunión y leer las listas de Travian." />}
      </Stack>
    </Stack>
  )
}

function Tile({ icon, label, color, disabled, onClick }: { icon: ReactNode; label: string; color: string; disabled?: boolean; onClick: () => void }) {
  return (
    <Button onClick={onClick} disabled={disabled} sx={tile}>
      <Box sx={{ color: disabled ? '#6d6f78' : color, display: 'flex', '& .MuiSvgIcon-root': { fontSize: 18 } }}>{icon}</Box>
      {label}
    </Button>
  )
}

export function Farming({ accountId, notify, mode }: { accountId: number; notify: (message: string) => void; mode: 'bot' | 'game' }) {
  const ui = useUi()
  const [data, setData] = useState<FarmingData | null>(null)
  const [villageId, setVillageId] = useState(0)
  const [listId, setListId] = useState(0)
  const [name, setName] = useState('')
  const [minMinutes, setMinMinutes] = useState(0)
  const [repeatMinutes, setRepeatMinutes] = useState(0)
  const [cowId, setCowId] = useState(0)
  const [cowName, setCowName] = useState('')
  const [url, setUrl] = useState('')
  const [troops, setTroops] = useState('')
  const [adding, setAdding] = useState(false)
  const [saving, setSaving] = useState(false)
  const [syncing, setSyncing] = useState(false)
  const [bulkOpen, setBulkOpen] = useState(false)
  const [bulkText, setBulkText] = useState('')
  const [oasisOpen, setOasisOpen] = useState(false)
  const [oasisTroops, setOasisTroops] = useState('2')
  const [oasisFrom, setOasisFrom] = useState('0')
  const [oasisTo, setOasisTo] = useState('20')
  const [query, setQuery] = useState('')
  const [kindFilter, setKindFilter] = useState('todos')
  const [popFrom, setPopFrom] = useState('')
  const [popTo, setPopTo] = useState('')
  const [distFrom, setDistFrom] = useState('')
  const [distTo, setDistTo] = useState('')
  const syncAbort = useRef<AbortController | null>(null)
  const minutesTimer = useRef<number | null>(null)
  const listDraft = useRef({ listId: 0, name: '', min: 0, repeat: 0, auto: false })
  const bulkBaseline = useRef('')

  async function load(nextVillage = villageId) {
    const result = await api<FarmingData>(`/api/accounts/${accountId}/farming?villageId=${nextVillage}`)
    setData(result.data)
    if (!nextVillage && result.data.villages[0]) setVillageId(result.data.villages[0].id)
    return result.data
  }

  useEffect(() => {
    load().catch((error: Error) => notify(error.message))
    const timer = window.setInterval(() => load().catch(() => undefined), 1000)
    return () => window.clearInterval(timer)
  }, [accountId, villageId])

  function selectList(list: CowList) {
    if (minutesTimer.current) {
      window.clearTimeout(minutesTimer.current)
      minutesTimer.current = null
    }
    setListId(list.id)
    setName(list.name)
    setMinMinutes(list.minMinutes)
    setRepeatMinutes(list.repeatMinutes)
    setCowId(0)
    setAdding(false)
    setCowName('')
    setUrl('')
    setTroops('')
    setBulkOpen(false)
    setBulkText('')
    setQuery('')
    setKindFilter('todos')
    setPopFrom('')
    setPopTo('')
    setDistFrom('')
    setDistTo('')
  }

  function openBulk() {
    const text = cows.map(cow => cow.name && cow.name !== 'Aldea sin nombre'
      ? `${cow.name} | ${cow.url} | ${cow.troops}`
      : `${cow.url} | ${cow.troops}`).join('\n')
    setAdding(false)
    setCowId(0)
    bulkBaseline.current = text
    setBulkText(text)
    setBulkOpen(true)
  }

  async function closeBulk() {
    if (bulkText === bulkBaseline.current) {
      setBulkOpen(false)
      return
    }
    if (!await ui.confirm('Las aldeas que no estén en el texto se borran de la lista. ¿Aplicar los cambios?')) return
    const saved = await run(() => post(`/api/accounts/${accountId}/farming/lists/${listId}/bulk`, { text: bulkText }))
    if (saved) setBulkOpen(false)
  }

  function selectCow(cow: Cow) {
    setCowId(cow.id)
    setCowName(cow.name)
    setUrl(cow.url)
    setTroops(cow.troops)
    setAdding(false)
  }

  async function run(action: () => Promise<{ message: string }>) {
    setSaving(true)
    try {
      notify((await action()).message)
      await load()
      return true
    } catch (error) {
      notify(error instanceof Error ? error.message : 'No se pudo completar')
      return false
    } finally {
      setSaving(false)
    }
  }

  async function syncNames() {
    const controller = new AbortController()
    syncAbort.current = controller
    setSyncing(true)
    try {
      const result = await api(`/api/accounts/${accountId}/farming/lists/${listId}/sync`, { method: 'POST', signal: controller.signal })
      notify(result.message)
      await load()
    } catch (error) {
      if (controller.signal.aborted) return
      notify(error instanceof Error ? error.message : 'No se pudo sincronizar')
    } finally {
      if (syncAbort.current === controller) syncAbort.current = null
      setSyncing(false)
    }
  }

  async function stopAll() {
    const wasSyncing = syncAbort.current !== null
    syncAbort.current?.abort()
    try {
      const result = await post(`/api/accounts/${accountId}/farming/lists/${listId}/stop`)
      notify(wasSyncing ? 'Sync detenido.' : result.message)
      await load()
    } catch (error) {
      notify(wasSyncing ? 'Sync detenido.' : error instanceof Error ? error.message : 'No se pudo detener')
    }
  }

  async function setAutoStart(enabled: boolean) {
    if (!listId) return
    if (enabled && (minMinutes < 1 || repeatMinutes < 1)) {
      notify('Para el inicio automático, el mínimo por aldea y la revisión tienen que ser de 1 minuto o más.')
      return
    }
    if (enabled && cows.length === 0) {
      notify('La lista no tiene aldeas.')
      return
    }
    await run(() => put(`/api/accounts/${accountId}/farming/lists/${listId}`, { name, minMinutes, repeatMinutes, autoStart: enabled }))
  }

  async function search() {
    const from = Number(oasisFrom.replace(',', '.'))
    const to = Number(oasisTo.replace(',', '.'))
    if (!Number.isFinite(from) || from < 0) {
      notify('La distancia desde tiene que ser un número de 0 o más.')
      return
    }
    if (!Number.isFinite(to) || to <= 0) {
      notify('La distancia hasta tiene que ser un número mayor que 0.')
      return
    }
    if (from > to) {
      notify('La distancia desde no puede ser mayor que la hasta.')
      return
    }
    setOasisOpen(false)
    try {
      const found = await post<SearchResult>(`/api/accounts/${accountId}/farming/lists/${listId}/search`, { troops: oasisTroops, minDistance: oasisFrom, distance: oasisTo })
      notify(found.message)
      if (!found.data.count) return
      const accepted = await ui.confirm(`¿Agregar los ${found.data.count} oasis con ${found.data.troopPlan}?`)
      if (!accepted) return
      const applied = await post(`/api/accounts/${accountId}/farming/lists/${listId}/search/apply`, { troopPlan: found.data.troopPlan, hits: found.data.hits })
      notify(applied.message)
      await load()
    } catch (error) {
      notify(error instanceof Error ? error.message : 'No se pudo buscar')
    }
  }

  const cows = data?.cows.filter(cow => cow.listId === listId) ?? []
  const shown = cows.filter(cow => cowMatches(cow, query, kindFilter, popFrom, popTo, distFrom, distTo))
  const activeId = cows.find(cow => cow.status === 'Analizando' || cow.status === 'Atacando')?.id ?? 0
  const scrolledId = useRef(0)
  useEffect(() => {
    if (!activeId || scrolledId.current === activeId) return
    scrolledId.current = activeId
    document.getElementById(`cow-${activeId}`)?.scrollIntoView({ block: 'nearest' })
  }, [activeId])
  const timesReady = minMinutes >= 1 && repeatMinutes >= 1
  const selectedList = data?.lists.find(list => list.id === listId)
  const selectedRunning = !!selectedList?.running
  const anyRunning = data?.lists.some(list => list.running) ?? false
  const countdown = selectedList?.countdown ?? ''
  listDraft.current = { listId, name: selectedList?.name ?? name, min: minMinutes, repeat: repeatMinutes, auto: !!selectedList?.autoStart }

  function changeMinutes(kind: 'min' | 'repeat', raw: string) {
    const value = raw === '' ? 0 : Number(raw)
    if (!Number.isFinite(value)) return
    if (kind === 'min') setMinMinutes(value)
    else setRepeatMinutes(value)
    listDraft.current = { ...listDraft.current, [kind === 'min' ? 'min' : 'repeat']: value }
    if (!listDraft.current.listId) return
    if (minutesTimer.current) window.clearTimeout(minutesTimer.current)
    minutesTimer.current = window.setTimeout(() => {
      minutesTimer.current = null
      const draft = listDraft.current
      if (!draft.listId) return
      void run(() => put(`/api/accounts/${accountId}/farming/lists/${draft.listId}`, { name: draft.name, minMinutes: draft.min, repeatMinutes: draft.repeat, autoStart: draft.auto }))
    }, 500)
  }

  async function saveCow() {
    if (adding || !cowId) return
    const cow = data?.cows.find(item => item.id === cowId)
    if (!cow || (cow.name === cowName && cow.url === url && cow.troops === troops)) return
    await run(() => put(`/api/accounts/${accountId}/farming/villages/${cowId}`, { name: cowName, url, troops }))
  }
  if (mode === 'game') {
    if (!data) return null
    if (!data.gold) return <Empty title="Esta cuenta no es Gold" text="Marcá Cuenta Gold en Ajustes para usar las listas del juego." />
    return <GameFarming accountId={accountId} data={data} notify={notify} reload={() => load()} />
  }

  if (data && data.villages.length === 0) {
    return <Empty title="Esta cuenta no tiene aldeas" text="Conectá la cuenta para que cargue las aldeas. Después vas a poder armar las listas acá." />
  }

  return (
    <Stack spacing={2} sx={{ minWidth: 0 }}>
      <Stack direction="row" spacing={2} alignItems="center" justifyContent="space-between">
        <Typography variant="h4">Listas de vacas</Typography>
        <TextField select label="Aldea" value={villageId || ''} onChange={event => { setVillageId(Number(event.target.value)); setListId(0) }} sx={{ width: 360 }}>
          {(data?.villages ?? []).map(village => <MenuItem key={village.id} value={village.id}>{village.name} ({village.x}|{village.y})</MenuItem>)}
        </TextField>
      </Stack>
      <Box sx={{ display: 'grid', gridTemplateColumns: '260px minmax(0, 1fr)', gap: 2, alignItems: 'start' }}>
        <Stack spacing={1} sx={{ minWidth: 0 }}>
          {(data?.lists ?? []).map(list => (
            <Button key={list.id} variant={list.id === listId ? 'contained' : 'outlined'} onClick={() => selectList(list)} sx={{ justifyContent: 'flex-start', minHeight: 40, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{listTitle(list)}</Button>
          ))}
          {(data?.lists.length ?? 0) === 0 && <Typography color="text.secondary">Todavía no hay listas en esta aldea.</Typography>}
          <TextField label="Nombre de la lista" value={name} onChange={event => setName(event.target.value)} />
          <Button variant="contained" onClick={() => run(() => post(`/api/accounts/${accountId}/farming/lists`, { villageId, name }))}>Crear</Button>
          <Button disabled={!listId || saving} onClick={() => run(() => put(`/api/accounts/${accountId}/farming/lists/${listId}`, { name, minMinutes, repeatMinutes, autoStart: selectedList?.autoStart ?? false }))}>Renombrar</Button>
          <Button color="error" disabled={!listId || saving} onClick={async () => { if (await ui.confirm('¿Borrar esta lista y todas sus aldeas?')) run(() => del(`/api/accounts/${accountId}/farming/lists/${listId}`)) }}>Borrar</Button>
        </Stack>

        <Stack spacing={1.5} sx={{ minWidth: 0 }}>
          <Box sx={{ ...panel }}>
            <Typography fontWeight={700} sx={{ mb: 1.5 }}>{selectedList ? selectedList.name : 'Elegí una lista'}</Typography>
            <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(5, minmax(0, 1fr))', gap: 1 }}>
              <Tooltip title={anyRunning ? 'Hay una lista en curso. Detenela para poder sincronizar.' : ''}>
                <Box sx={{ minWidth: 0, display: 'flex', '& > .MuiButton-root': { flex: 1 } }}>
                  <Tile color="#5865f2" icon={<SyncIcon />} label={syncing ? 'Sync...' : 'Sync'} disabled={!listId || cows.length === 0 || saving || syncing || anyRunning} onClick={syncNames} />
                </Box>
              </Tooltip>
              <Tile color="#23a559" icon={<AddIcon />} label="Agregar" disabled={!listId || saving || syncing} onClick={() => { setAdding(true); setCowId(0); setCowName(''); setUrl(''); setTroops('') }} />
              <Tile color="#f0b232" icon={<SearchIcon />} label="Oasis" disabled={!listId || saving || syncing} onClick={() => setOasisOpen(true)} />
              <Tooltip title={selectedRunning ? 'Esta lista ya está en curso.' : timesReady ? '' : 'Completá el mínimo por aldea y cada cuánto se revisa la lista. Los dos tienen que ser de 1 minuto o más.'}>
                <Box sx={{ minWidth: 0, display: 'flex', '& > .MuiButton-root': { flex: 1 } }}>
                  <Tile color="#23a559" icon={<PlayArrowIcon />} label="Iniciar" disabled={!listId || saving || syncing || !timesReady || selectedRunning} onClick={() => run(() => post(`/api/accounts/${accountId}/farming/lists/${listId}/start`, { minMinutes, repeatMinutes }))} />
                </Box>
              </Tooltip>
              <Tooltip title={selectedRunning ? 'Detiene solo esta lista.' : listId > 0 && !syncing ? 'Se habilita cuando esta lista está en curso.' : ''}>
                <Box sx={{ minWidth: 0, display: 'flex', '& > .MuiButton-root': { flex: 1 } }}>
                  <Tile color="#ed4245" icon={<StopIcon />} label="Detener" disabled={!listId || (saving && !syncing) || (!selectedRunning && !syncing)} onClick={stopAll} />
                </Box>
              </Tooltip>
            </Box>
            {countdown && <Typography color="error" fontWeight={800} sx={{ mt: 1.5 }}>{countdown}</Typography>}
          </Box>

          <Box sx={{ ...panel, display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 1, alignItems: 'center' }}>
            <Stack direction="row" spacing={0.75} alignItems="center">
              <TextField fullWidth required type="number" label="Mínimo por aldea" value={minMinutes || ''} error={listId > 0 && minMinutes < 1} onChange={event => changeMinutes('min', event.target.value)} slotProps={{ input: { endAdornment: <Typography color="text.secondary" sx={{ ml: 1 }}>min</Typography> } }} />
              <Hint text="Minutos que tienen que pasar para volver a atacar la misma aldea. Si todavía no se cumplieron, esa aldea se saltea." />
            </Stack>
            <Stack direction="row" spacing={0.75} alignItems="center">
              <TextField fullWidth required type="number" label="Revisar la lista cada" value={repeatMinutes || ''} error={listId > 0 && repeatMinutes < 1} onChange={event => changeMinutes('repeat', event.target.value)} slotProps={{ input: { endAdornment: <Typography color="text.secondary" sx={{ ml: 1 }}>min</Typography> } }} />
              <Hint text="Minutos que espera el bot después de recorrer la lista para empezar otra vez y atacar las aldeas que ya cumplieron el mínimo." />
            </Stack>
          </Box>

          <Stack direction="row" spacing={1} alignItems="center" sx={{ ...panel, py: 1 }}>
            <Switch checked={!!selectedList?.autoStart} disabled={!listId || saving || syncing} onChange={event => setAutoStart(event.target.checked)} />
            <Typography fontWeight={600}>Inicio automático</Typography>
            <Hint text="Al activarlo se presiona Iniciar en esta lista. También arranca sola la próxima vez que conectes la cuenta. Cada lista es independiente: si hay otra en curso, esta espera a que termine el recorrido." />
          </Stack>
          <Stack direction="row" spacing={0.75} alignItems="center" sx={{ ...panel, py: 1 }}>
            <FormControlLabel
              label="Levear héroe"
              control={
                <Checkbox
                  checked={selectedList?.levelHero ?? false}
                  disabled={!selectedList || saving}
                  onChange={event => {
                    if (!selectedList) return
                    const levelHero = event.target.checked
                    run(() => put(`/api/accounts/${accountId}/farming/lists/${listId}`, { name: selectedList.name, minMinutes: selectedList.minMinutes, repeatMinutes: selectedList.repeatMinutes, autoStart: selectedList.autoStart, levelHero }))
                  }}
                />
              }
            />
            <Hint text={'En los oasis con animales, el bot simula el atraco con el héroe solo y lo manda únicamente si sobrevive con al menos 25% de vida y mata animales. Si el héroe tiene menos de 50% de vida, primero cobra tareas. Los oasis vacíos se siguen atacando con tropas como siempre.'} />
          </Stack>

          {(cowId > 0 || adding) && (
            <Stack spacing={1.5} sx={{ ...panel }}>
              <Typography fontWeight={700}>{adding ? 'Nueva aldea' : 'Editar aldea'}</Typography>
              <Typography color="text.secondary">Letras iguales en todas las razas: A D G / B E H / C F. Un número solo usa A. Al agregar, el navegador lee el nombre.</Typography>
              {!adding && <TextField label="Nombre" value={cowName} onChange={event => setCowName(event.target.value)} onBlur={saveCow} />}
              <TextField label="Enlace" value={url} onChange={event => setUrl(event.target.value)} onBlur={saveCow} />
              <TextField label="Tropas" placeholder="2 o A:2 D:1" value={troops} onChange={event => setTroops(event.target.value)} onBlur={saveCow} />
              <Stack direction="row" spacing={1}>
                {adding && <Button variant="contained" disabled={saving} onClick={async () => {
                  const saved = await run(() => post(`/api/accounts/${accountId}/farming/lists/${listId}/villages`, { url, troops }))
                  if (saved) {
                    setAdding(false)
                    setUrl('')
                    setTroops('')
                  }
                }}>{saving ? 'Leyendo el nombre...' : 'Agregar'}</Button>}
                {!adding && <Button color="error" onClick={async () => { if (await ui.confirm('¿Sacar esta aldea de la lista?')) { if (await run(() => del(`/api/accounts/${accountId}/farming/villages/${cowId}`))) setCowId(0) } }}>Borrar</Button>}
                <Button onClick={() => { setAdding(false); setCowId(0) }}>Cerrar</Button>
              </Stack>
            </Stack>
          )}

          {bulkOpen && (
            <Stack spacing={1.5} sx={{ ...panel }}>
              <Stack direction="row" spacing={0.75} alignItems="center">
                <Typography fontWeight={700}>Edición masiva</Typography>
                <Hint text="Una aldea por línea: enlace | tropas. Para el nombre: nombre | enlace | tropas. También podés separar el enlace y las tropas con un espacio. Al cerrar se guardan los cambios: las líneas que saques se borran. Las nuevas quedan como Aldea sin nombre hasta que uses Sync." />
              </Stack>
              <TextField multiline minRows={8} value={bulkText} onChange={event => setBulkText(event.target.value)} placeholder={'https://servidor/karte.php?d=1 | 2\nOasis norte | https://servidor/karte.php?d=2 | A:2 D:1'} />
              <Stack direction="row" spacing={1}>
                <Button onClick={closeBulk}>Cerrar</Button>
              </Stack>
            </Stack>
          )}

          <Stack spacing={1} sx={{ ...panel, p: 1 }}>
            <Stack direction="row" alignItems="center" justifyContent="space-between" sx={{ px: 0.5 }}>
              <Typography variant="caption" color="text.secondary">Aldeas de la lista</Typography>
              <Button size="small" disabled={!listId || saving || syncing} onClick={openBulk}>Edición masiva</Button>
            </Stack>
            <Box sx={{ display: 'grid', gridTemplateColumns: 'minmax(140px, 1.4fr) 110px 90px 90px 90px 90px', gap: 1, px: 0.5 }}>
              <TextField size="small" label="Buscar" placeholder="Nombre, dueño, tribu" value={query} onChange={event => setQuery(event.target.value)} />
              <TextField size="small" select label="Tipo" value={kindFilter} onChange={event => setKindFilter(event.target.value)}>
                <MenuItem value="todos">Todos</MenuItem>
                <MenuItem value="aldea">Aldeas</MenuItem>
                <MenuItem value="oasis">Oasis</MenuItem>
              </TextField>
              <TextField size="small" label="Pob. desde" value={popFrom} onChange={event => setPopFrom(event.target.value)} />
              <TextField size="small" label="Pob. hasta" value={popTo} onChange={event => setPopTo(event.target.value)} />
              <TextField size="small" label="Dist. desde" value={distFrom} onChange={event => setDistFrom(event.target.value)} />
              <TextField size="small" label="Dist. hasta" value={distTo} onChange={event => setDistTo(event.target.value)} />
            </Box>
            <Box sx={{ overflowX: 'auto' }}>
              <Box sx={{ minWidth: 860 }}>
                <Box sx={{ display: 'grid', gridTemplateColumns: cowColumns, gap: 1.5, px: 1.5, py: 0.5 }}>
                  {['Aldea', 'Tipo', 'Dueño', 'Población', 'Distancia', 'Tropas', 'Estado'].map(label => (
                    <Typography key={label} variant="caption" color="text.secondary">{label}</Typography>
                  ))}
                </Box>
                {shown.map(cow => {
                  const live = cow.status === 'Analizando' || cow.status === 'Atacando'
                  return (
                    <Box id={`cow-${cow.id}`} key={cow.id} onClick={() => selectCow(cow)} sx={{ display: 'grid', gridTemplateColumns: cowColumns, gap: 1.5, alignItems: 'center', px: 1.5, py: 1.25, borderRadius: 1, cursor: 'pointer', bgcolor: live ? '#3c4280' : cow.id === cowId ? '#404249' : 'transparent', boxShadow: live ? 'inset 3px 0 0 #5865f2' : 'none', '&:hover': { bgcolor: live ? '#3c4280' : cow.id === cowId ? '#404249' : '#35373c' } }}>
                      <Box sx={{ minWidth: 0 }}>
                        <Typography sx={{ fontWeight: 600, fontSize: 15, lineHeight: 1.35, overflowWrap: 'anywhere' }}>{cow.name}</Typography>
                        {cow.x != null && cow.y != null && <Typography variant="caption" color="text.secondary">({cow.x}|{cow.y}){cow.tribe ? ` · ${cow.tribe}` : ''}{cow.alliance ? ` · ${cow.alliance}` : ''}</Typography>}
                      </Box>
                      <Typography sx={{ color: '#dbdee1' }}>{kindLabel(cow.kind)}</Typography>
                      <Typography sx={{ color: '#dbdee1', overflowWrap: 'anywhere' }}>{cow.owner || '—'}</Typography>
                      <Typography sx={{ color: '#dbdee1' }}>{cow.population >= 0 ? cow.population : '—'}</Typography>
                      <Typography sx={{ color: '#dbdee1' }}>{cow.distance >= 0 ? cow.distance.toLocaleString('es', { maximumFractionDigits: 2 }) : '—'}</Typography>
                      <Typography sx={{ color: '#dbdee1' }}>{cow.troops}</Typography>
                      <Typography sx={{ color: cow.status === 'Atacando' ? '#23a559' : cow.status === 'Analizando' ? '#f0b232' : '#b5bac1', fontSize: 13, fontWeight: live ? 700 : 400 }}>{cow.status}</Typography>
                    </Box>
                  )
                })}
              </Box>
            </Box>
            {listId > 0 && cows.length === 0 && <Empty title="Lista vacía" text="Agregá una aldea a mano o buscá oasis." />}
            {listId > 0 && cows.length > 0 && shown.length === 0 && <Empty title="Ninguna coincide" text="Probá otro dueño, otra población o sacá un filtro." />}
            {listId === 0 && <Empty title="Elegí una lista" text="Las aldeas de esa lista se ven acá, con el nombre completo." />}
          </Stack>
        </Stack>
      </Box>
      <Dialog open={oasisOpen} onClose={() => setOasisOpen(false)} fullWidth maxWidth="xs">
        <DialogTitle>Buscar oasis</DialogTitle>
        <DialogContent>
          <Stack spacing={2} sx={{ pt: 1 }}>
            <TextField label="Tropas" value={oasisTroops} onChange={event => setOasisTroops(event.target.value)} placeholder="2 o A:2 D:1" />
            <TextField label="Desde (casillas)" value={oasisFrom} onChange={event => setOasisFrom(event.target.value)} />
            <TextField label="Hasta (casillas)" value={oasisTo} onChange={event => setOasisTo(event.target.value)} />
            <Typography color="text.secondary">Se incluyen los dos límites. Para oasis lejanos, por ejemplo desde 10 hasta 30. Desde 0 incluye los cercanos.</Typography>
          </Stack>
        </DialogContent>
        <DialogActions sx={{ px: 3, pb: 2 }}>
          <Button onClick={() => setOasisOpen(false)}>Cancelar</Button>
          <Button variant="contained" onClick={search}>Buscar</Button>
        </DialogActions>
      </Dialog>
    </Stack>
  )
}
