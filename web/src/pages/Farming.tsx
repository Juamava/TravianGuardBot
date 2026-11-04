import { useEffect, useRef, useState, type ReactNode } from 'react'
import { Box, Button, MenuItem, Stack, TextField, Typography } from '@mui/material'
import AddIcon from '@mui/icons-material/Add'
import PlayArrowIcon from '@mui/icons-material/PlayArrow'
import SearchIcon from '@mui/icons-material/Search'
import StopIcon from '@mui/icons-material/Stop'
import SyncIcon from '@mui/icons-material/Sync'
import { api, del, post, put } from '../api'
import { Empty, panel, useUi } from '../ui'

type Village = { id: number; name: string; x: number; y: number }
type CowList = { id: number; name: string; minMinutes: number; repeatMinutes: number }
type Cow = { id: number; listId: number; name: string; url: string; troops: string; status: string }
type FarmingData = { villages: Village[]; lists: CowList[]; cows: Cow[]; countdown: string }
type SearchResult = { count: number; villageName: string; originX: number; originY: number; troopPlan: string; hits: { x: number; y: number; distance: number }[] }

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

function Tile({ icon, label, color, disabled, onClick }: { icon: ReactNode; label: string; color: string; disabled?: boolean; onClick: () => void }) {
  return (
    <Button onClick={onClick} disabled={disabled} sx={tile}>
      <Box sx={{ color: disabled ? '#6d6f78' : color, display: 'flex', '& .MuiSvgIcon-root': { fontSize: 18 } }}>{icon}</Box>
      {label}
    </Button>
  )
}

export function Farming({ accountId, notify }: { accountId: number; notify: (message: string) => void }) {
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
  const syncAbort = useRef<AbortController | null>(null)

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
    setListId(list.id)
    setName(list.name)
    setMinMinutes(list.minMinutes)
    setRepeatMinutes(list.repeatMinutes)
    setCowId(0)
    setAdding(false)
    setCowName('')
    setUrl('')
    setTroops('')
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

  async function search() {
    const troopText = await ui.prompt('Tropas para cada oasis. Ejemplo: 2 o A:2 D:1', '2')
    if (!troopText) return
    const distance = await ui.prompt('Distancia máxima en casillas', '20')
    if (!distance) return
    try {
      const found = await post<SearchResult>(`/api/accounts/${accountId}/farming/lists/${listId}/search`, { troops: troopText, distance })
      notify(found.message)
      if (!found.data.count) return
      const accepted = await ui.confirm(`Se encontraron ${found.data.count} oasis. ¿Agregarlos con ${found.data.troopPlan}?`)
      if (!accepted) return
      const applied = await post(`/api/accounts/${accountId}/farming/lists/${listId}/search/apply`, { troopPlan: found.data.troopPlan, hits: found.data.hits })
      notify(applied.message)
      await load()
    } catch (error) {
      notify(error instanceof Error ? error.message : 'No se pudo buscar')
    }
  }

  const cows = data?.cows.filter(cow => cow.listId === listId) ?? []
  const countdown = data?.countdown ?? ''
  const selectedList = data?.lists.find(list => list.id === listId)
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
            <Button key={list.id} variant={list.id === listId ? 'contained' : 'outlined'} onClick={() => selectList(list)} sx={{ justifyContent: 'flex-start', minHeight: 40, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{list.name}</Button>
          ))}
          {(data?.lists.length ?? 0) === 0 && <Typography color="text.secondary">Todavía no hay listas en esta aldea.</Typography>}
          <TextField label="Nombre de la lista" value={name} onChange={event => setName(event.target.value)} />
          <Button variant="contained" onClick={() => run(() => post(`/api/accounts/${accountId}/farming/lists`, { villageId, name }))}>Crear</Button>
          <Button disabled={!listId || saving} onClick={() => run(() => put(`/api/accounts/${accountId}/farming/lists/${listId}`, { name, minMinutes, repeatMinutes }))}>Renombrar</Button>
          <Button color="error" disabled={!listId || saving} onClick={async () => { if (await ui.confirm('¿Borrar esta lista y todas sus aldeas?')) run(() => del(`/api/accounts/${accountId}/farming/lists/${listId}`)) }}>Borrar</Button>
        </Stack>

        <Stack spacing={1.5} sx={{ minWidth: 0 }}>
          <Box sx={{ ...panel }}>
            <Typography fontWeight={700} sx={{ mb: 1.5 }}>{selectedList ? selectedList.name : 'Elegí una lista'}</Typography>
            <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(5, minmax(0, 1fr))', gap: 1 }}>
              <Tile color="#5865f2" icon={<SyncIcon />} label={syncing ? 'Sync...' : 'Sync'} disabled={!listId || cows.length === 0 || saving || syncing} onClick={syncNames} />
              <Tile color="#23a559" icon={<AddIcon />} label="Agregar" disabled={!listId || saving || syncing} onClick={() => { setAdding(true); setCowId(0); setCowName(''); setUrl(''); setTroops('') }} />
              <Tile color="#f0b232" icon={<SearchIcon />} label="Oasis" disabled={!listId || saving || syncing} onClick={search} />
              <Tile color="#23a559" icon={<PlayArrowIcon />} label="Iniciar" disabled={!listId || saving || syncing} onClick={() => run(() => post(`/api/accounts/${accountId}/farming/lists/${listId}/start`, { minMinutes, repeatMinutes }))} />
              <Tile color="#ed4245" icon={<StopIcon />} label="Detener" disabled={!listId || (saving && !syncing)} onClick={stopAll} />
            </Box>
            {countdown && <Typography color="error" fontWeight={800} sx={{ mt: 1.5 }}>{countdown}</Typography>}
          </Box>

          <Box sx={{ ...panel, display: 'grid', gridTemplateColumns: '1fr 1fr auto', gap: 1, alignItems: 'center' }}>
            <TextField type="number" label="Mínimo por aldea (minutos)" value={minMinutes} onChange={event => setMinMinutes(Number(event.target.value))} />
            <TextField type="number" label="Revisar la lista cada (minutos)" value={repeatMinutes} onChange={event => setRepeatMinutes(Number(event.target.value))} />
            <Button variant="contained" disabled={!listId || saving} onClick={() => run(() => put(`/api/accounts/${accountId}/farming/lists/${listId}`, { name, minMinutes, repeatMinutes }))} sx={{ height: 40 }}>Guardar</Button>
          </Box>

          {(cowId > 0 || adding) && (
            <Stack spacing={1.5} sx={{ ...panel }}>
              <Typography fontWeight={700}>{adding ? 'Nueva aldea' : 'Editar aldea'}</Typography>
              <Typography color="text.secondary">Letras iguales en todas las razas: A D G / B E H / C F. Un número solo usa A. Al agregar, el navegador lee el nombre.</Typography>
              {!adding && <TextField label="Nombre" value={cowName} onChange={event => setCowName(event.target.value)} />}
              <TextField label="Enlace" value={url} onChange={event => setUrl(event.target.value)} />
              <TextField label="Tropas" placeholder="2 o A:2 D:1" value={troops} onChange={event => setTroops(event.target.value)} />
              <Stack direction="row" spacing={1}>
                <Button variant="contained" disabled={saving || (!adding && !cowId)} onClick={async () => {
                  const saved = await run(() => adding
                    ? post(`/api/accounts/${accountId}/farming/lists/${listId}/villages`, { url, troops })
                    : put(`/api/accounts/${accountId}/farming/villages/${cowId}`, { name: cowName, url, troops }))
                  if (saved && adding) {
                    setAdding(false)
                    setUrl('')
                    setTroops('')
                  }
                }}>{saving && adding ? 'Leyendo el nombre...' : adding ? 'Agregar' : 'Guardar cambios'}</Button>
                {!adding && <Button color="error" onClick={async () => { if (await ui.confirm('¿Sacar esta aldea de la lista?')) { if (await run(() => del(`/api/accounts/${accountId}/farming/villages/${cowId}`))) setCowId(0) } }}>Borrar</Button>}
                <Button onClick={() => { setAdding(false); setCowId(0) }}>Cerrar</Button>
              </Stack>
            </Stack>
          )}

          <Stack spacing={0.5} sx={{ ...panel, p: 1 }}>
            <Box sx={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr) 160px 180px', gap: 2, px: 1.5, py: 0.5 }}>
              <Typography variant="caption" color="text.secondary">Aldea</Typography>
              <Typography variant="caption" color="text.secondary">Tropas</Typography>
              <Typography variant="caption" color="text.secondary">Estado</Typography>
            </Box>
            {cows.map(cow => (
              <Box key={cow.id} onClick={() => selectCow(cow)} sx={{ display: 'grid', gridTemplateColumns: 'minmax(0, 1fr) 160px 180px', gap: 2, alignItems: 'center', px: 1.5, py: 1.25, borderRadius: 1, cursor: 'pointer', bgcolor: cow.id === cowId ? '#404249' : 'transparent', '&:hover': { bgcolor: cow.id === cowId ? '#404249' : '#35373c' } }}>
                <Typography sx={{ fontWeight: 600, fontSize: 15, lineHeight: 1.35, overflowWrap: 'anywhere' }}>{cow.name}</Typography>
                <Typography sx={{ color: '#dbdee1' }}>{cow.troops}</Typography>
                <Typography sx={{ color: '#b5bac1', fontSize: 13 }}>{cow.status}</Typography>
              </Box>
            ))}
            {listId > 0 && cows.length === 0 && <Empty title="Lista vacía" text="Agregá una aldea a mano o buscá oasis." />}
            {listId === 0 && <Empty title="Elegí una lista" text="Las aldeas de esa lista se ven acá, con el nombre completo." />}
          </Stack>
        </Stack>
      </Box>
    </Stack>
  )
}
